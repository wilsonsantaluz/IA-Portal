using AiGateway.Dtos;
using AiGateway.Infrastructure.Qdrant.Services.LLMs;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Portal.AI.Gateway.Application.Interfaces;
using Portal.AI.Gateway.Infrastructure;
using System.Net;
using System.Security.Claims;
using System.Text.Json;

var folder = Path.Combine(Path.GetTempPath(), "portal-title-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["SQLITE_BACKUP_DIRECTORY"] = Path.Combine(folder, "backups") }).Build();
PortalDbContext Context(string path) => new(new DbContextOptionsBuilder<PortalDbContext>().UseSqlite($"Data Source={path};Foreign Keys=True").Options);
void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
IHttpContextAccessor Identity(int id) => new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test")) } };
try {
    // Build a disposable v1 database, then exercise the upgrade including rollback and backup.
    var path = Path.Combine(folder, "v1.sqlite3");
    await using (var old = Context(path)) {
        await ChatSchemaMigrator.InitializeAsync(old, config);
        old.Users.AddRange(new User { UserName = "Synthetic A", UserEmail = "a@example.test", PasswordHash = "test-only", IsActive = true }, new User { UserName = "Synthetic B", UserEmail = "b@example.test", PasswordHash = "test-only", IsActive = true });
        await old.SaveChangesAsync();
        var preserved = new ChatSession { Id = Guid.NewGuid(), OwnerUserId = 1, UserId = "1", Title = "Histórico preservado", TitleSource = "manual", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        preserved.Messages.Add(new ChatMessage { Role = "user", Content = "Pergunta anterior à v2", CreatedAt = DateTime.UtcNow });
        preserved.Messages.Add(new ChatMessage { Role = "assistant", Content = "Resposta anterior à v2", CreatedAt = DateTime.UtcNow });
        old.ChatSessions.Add(preserved); await old.SaveChangesAsync();
        await old.Database.ExecuteSqlRawAsync("DROP TABLE OrganizationInvitations; DROP TABLE OrganizationAiPreferences; DROP TABLE OrganizationAiDefaults; ALTER TABLE OrganizationMemberships DROP COLUMN PolicyRole; ALTER TABLE OrganizationMemberships DROP COLUMN Version; DROP TABLE AiUserPreferences; DROP TABLE AiSystemDefault; DROP TABLE AiProviderSettings; DROP TABLE LlmUsage; DELETE FROM SchemaVersions WHERE Version>=2; CREATE TABLE Collision(Id INTEGER); CREATE INDEX IX_LlmUsage_SessionId_Purpose ON Collision(Id)");
        try { await ChatSchemaMigrator.InitializeAsync(old, config); throw new Exception("DDL should fail"); } catch (SqliteException) { }
        using var query = old.Database.GetDbConnection().CreateCommand(); query.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name='LlmUsage'";
        Check(Convert.ToInt64(await query.ExecuteScalarAsync()) == 0, "v2 DDL rollback failed");
        query.CommandText = "SELECT COUNT(*) FROM SchemaVersions WHERE Version=2";
        Check(Convert.ToInt64(await query.ExecuteScalarAsync()) == 0, "v2 failure recorded as success");
        await old.Database.ExecuteSqlRawAsync("DROP TABLE Collision");
        await ChatSchemaMigrator.InitializeAsync(old, config);
        var backups = Directory.GetFiles(Path.Combine(folder, "backups"));
        await ChatSchemaMigrator.InitializeAsync(old, config);
        Check(Directory.GetFiles(Path.Combine(folder, "backups")).Length == backups.Length && backups.Length == 2, "Backup repeated on initialized DB");
        Check(await old.Users.CountAsync() == 2 && await old.LlmUsages.CountAsync() == 0, "v2 migration lost users or invented usage");
        old.ChangeTracker.Clear();
        Check((await old.ChatSessions.SingleAsync()).Id == preserved.Id && (await old.ChatSessions.SingleAsync()).OwnerUserId == 1 && await old.ChatMessages.CountAsync() == 2 && await old.ChatMessages.AnyAsync(m => m.Content == "Resposta anterior à v2"), "v2 upgrade lost existing conversation ownership/content/IDs");
    }
    Console.WriteLine("PASS: SQLite v1 to v2 backup, rollback, data preservation and repeated initialization.");
    await using (var db = Context(path)) {
        var identity = Identity(1); var repo = new ChatRepository(db, identity);
        var mock = new TitleMock();
        var titles = new ChatTitleService(db, identity, mock, NullLogger<ChatTitleService>.Instance);
        async Task<Guid> Conversation(string question = "Como organizar documentos?", bool complete = true, string model = "selected-model") {
            var session = await repo.CreateSessionAsync(default);
            await db.ChatSessions.Where(s => s.Id == session.Id).ExecuteUpdateAsync(u => u.SetProperty(s => s.Model, model));
            db.ChangeTracker.Clear(); var request = Guid.NewGuid();
            await repo.BeginTurnAsync(session.Id, request, question, "synthetic", default);
            if (complete) await repo.CompleteTurnAsync(session.Id, request, "Resposta sintética", default, model: model);
            else await repo.FailTurnAsync(session.Id, request, "failed", default);
            return session.Id;
        }
        var id = await Conversation(new string('x', 1400));
        mock.Action = (model, subject, _) => { Check(model == "selected-model" && subject.Length == 1000, "Title sent wrong model or too much context"); return Task.FromResult(new TitleGeneration("\"Organização de documentos\"", "resolved-model", 31, 7, 38, 20)); };
        await titles.TryGenerateAsync(id, default); await titles.TryGenerateAsync(id, default);
        var session = await repo.GetSessionAsync(id, default); var usage = await db.LlmUsages.SingleAsync(u => u.SessionId == id);
        Check(session.Title == "Organização de documentos" && session.TitleSource == "automatic" && mock.Calls == 1, "Automatic title not saved exactly once");
        Check(usage.OwnerUserId == 1 && usage.Purpose == "title" && usage.Model == "resolved-model" && usage.InputTokens == 31 && usage.OutputTokens == 7 && usage.TotalTokens == 38 && usage.CacheTokens == 20 && usage.Source == "reported" && usage.DurationMs >= 0, "Title usage not measured separately");
        await repo.RenameAsync(id, new("Manual preservado", session.Version), default);
        await titles.TryGenerateAsync(id, default);
        Check((await repo.GetSessionAsync(id, default)).Title == "Manual preservado" && mock.Calls == 1, "Manual title overwritten");
        var manual = await Conversation(); var before = await repo.GetSessionAsync(manual, default);
        await repo.RenameAsync(manual, new("Manual antes da geração", before.Version), default);
        await titles.TryGenerateAsync(manual, default); Check(mock.Calls == 1, "Manual title consumed credits");
        var noAnswer = await Conversation(complete: false); await titles.TryGenerateAsync(noAnswer, default); Check(mock.Calls == 1, "Failed chat generated title");
        Console.WriteLine("PASS: automatic title once, minimum context/model, real token/cache accounting, manual edits and failed chats.");

        var failure = await Conversation("Primeira pergunta para fallback");
        mock.Action = (_, _, _) => throw new HttpRequestException("Synthetic secret must never be persisted");
        await titles.TryGenerateAsync(failure, default); await titles.TryGenerateAsync(failure, default);
        Check((await repo.GetSessionAsync(failure, default)).Title == "Primeira pergunta para fallback", "Failure fallback lost");
        var failedUsage = await db.LlmUsages.SingleAsync(u => u.SessionId == failure);
        Check(failedUsage.Status == "failed" && failedUsage.Source == "unavailable" && failedUsage.TotalTokens is null, "Failure invented usage");
        var empty = await Conversation(); mock.Action = (_, _, _) => Task.FromResult(new TitleGeneration("", "selected-model", 12, 0, 12));
        await titles.TryGenerateAsync(empty, default);
        Check((await db.LlmUsages.SingleAsync(u => u.SessionId == empty)).Status == "invalid_response" && (await repo.GetSessionAsync(empty, default)).TitleSource == "fallback", "Invalid response lost usage/fallback");
        var cancellation = await Conversation(); mock.Action = (_, _, _) => throw new OperationCanceledException();
        await titles.TryGenerateAsync(cancellation, default);
        Check((await db.LlmUsages.SingleAsync(u => u.SessionId == cancellation)).Status == "timeout", "Timeout not distinguished");
        var multiline = await Conversation(); mock.Action = (_, _, _) => Task.FromResult(new TitleGeneration("Título\nExplicação", "selected-model"));
        await titles.TryGenerateAsync(multiline, default);
        Check((await repo.GetSessionAsync(multiline, default)).TitleSource == "fallback", "Multiline response accepted");
        Check(ChatTitleService.Shorten(string.Concat(Enumerable.Repeat("😀", 100)), 80).Length == 160, "Unicode truncation split a character");
        Console.WriteLine("PASS: provider failure, invalid/multiline response, timeout, Unicode-safe fallback and absent usage stays null.");

        var concurrent = await Conversation();
        mock.Action = async (_, _, _) => { var current = await repo.GetSessionAsync(concurrent, default); await repo.RenameAsync(concurrent, new("Edição durante chamada", current.Version), default); return new("Título automático tardio", "selected-model"); };
        await titles.TryGenerateAsync(concurrent, default);
        Check((await repo.GetSessionAsync(concurrent, default)).Title == "Edição durante chamada", "Concurrent manual edit overwritten");
        Check((await db.LlmUsages.AsNoTracking().SingleAsync(u => u.SessionId == concurrent)).Status == "completed", "Concurrent edit lost measured attempt");
        var deleted = await Conversation(); mock.Action = async (_, _, _) => { await repo.DeleteAsync(deleted, default); return new("Título tardio", "selected-model"); };
        await titles.TryGenerateAsync(deleted, default);
        Check((await db.ChatSessions.AsNoTracking().SingleAsync(s => s.Id == deleted)).TitleSource == "fallback", "Deleted conversation mutated");
        var privateId = await Conversation(); var count = mock.Calls;
        var other = new ChatTitleService(db, Identity(2), mock, NullLogger<ChatTitleService>.Instance);
        await other.TryGenerateAsync(privateId, default); Check(mock.Calls == count, "Other user reached title provider");
        _ = Identity(1); // HttpContextAccessor shares AsyncLocal state across accessor instances.
        // Simulate termination after claim commit but before a provider outcome was known.
        var interrupted = await Conversation();
        var first = await db.ChatMessages.AsNoTracking().SingleAsync(m => m.SessionId == interrupted && m.Role == "assistant");
        await db.ChatSessions.Where(s => s.Id == interrupted).ExecuteUpdateAsync(u => u.SetProperty(s => s.TitleSource, "fallback").SetProperty(s => s.Title, "Fallback após interrupção"));
        db.LlmUsages.Add(new() { Id = Guid.NewGuid(), OwnerUserId = 1, SessionId = interrupted, MessageId = first.Id, Provider = "deepseek", Model = "selected-model", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        // Two independent request scopes contend for the same title; the second must not call the provider.
        var raced = await Conversation();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var racingMock = new TitleMock { Action = async (_, _, token) => { started.SetResult(); await release.Task.WaitAsync(token); return new("Um único título", "selected-model"); } };
        var firstGeneration = new ChatTitleService(db, identity, racingMock, NullLogger<ChatTitleService>.Instance).TryGenerateAsync(raced, default);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await using (var secondDb = Context(path)) await new ChatTitleService(secondDb, identity, racingMock, NullLogger<ChatTitleService>.Instance).TryGenerateAsync(raced, default);
        release.SetResult(); await firstGeneration;
        Check(racingMock.Calls == 1 && (await repo.GetSessionAsync(raced, default)).TitleSource == "automatic", "Concurrent scopes repeated title call");
        Console.WriteLine("PASS: rename/delete during generation and foreign-owner title request never overwrite or access content.");
    }
    // The durable claim prevents repeat charges after a process interruption.
    await using (var db = Context(path)) {
        var identity = Identity(1); var repo = new ChatRepository(db, identity); var mock = new TitleMock();
        foreach (var session in await db.ChatSessions.AsNoTracking().Where(s => s.TitleSource == "fallback" && s.DeletedAt == null).ToListAsync())
            await new ChatTitleService(db, identity, mock, NullLogger<ChatTitleService>.Instance).TryGenerateAsync(session.Id, default);
        Check(mock.Calls == 0, "Restart repeated paid call");
        await db.Database.ExecuteSqlRawAsync("UPDATE SchemaVersions SET Checksum='tampered' WHERE Version=2");
        try { await ChatSchemaMigrator.InitializeAsync(db, config); throw new Exception("v2 checksum ignored"); } catch (InvalidOperationException) { }
    }
    Console.WriteLine("PASS: restart does not repeat title attempts; v2 checksum enforced.");
    foreach (var payload in new[] {
        "{\"model\":\"actual\",\"choices\":[{\"message\":{\"content\":\"Título de teste\"}}],\"usage\":{\"prompt_tokens\":40,\"completion_tokens\":8,\"total_tokens\":48,\"prompt_cache_hit_tokens\":30}}",
        "{\"choices\":[{\"message\":{\"content\":\"Teste\"}}]}",
        "{\"choices\":[],\"usage\":{\"prompt_tokens\":null,\"completion_tokens\":-1,\"total_tokens\":\"invalid\",\"prompt_tokens_details\":{\"cached_tokens\":4}}}" }) {
        var handler = new TitleHandler(payload); var adapter = new DeepSeekService(handler, config);
        var result = await adapter.GenerateTitleAsync("selected", "Synthetic question", default);
        if (payload.Contains("actual")) Check(result.Model == "actual" && result.TotalTokens == 48 && result.CacheTokens == 30, "Provider metrics/model not parsed");
        else Check(result.TotalTokens is null, "Missing/invalid usage invented");
        if (payload.Contains("cached_tokens")) Check(result.CacheTokens == 4, "Cache details not parsed");
    }
    Console.WriteLine("PASS: actual adapter payload is bounded/nonstream, honors model, parses real usage, handles missing/invalid counters without network.");
} finally { SqliteConnection.ClearAllPools(); Directory.Delete(folder, true); }

sealed class TitleMock : IChatTitleGenerator {
    public int Calls;
    public Func<string, string, CancellationToken, Task<TitleGeneration>> Action = (_, _, _) => Task.FromResult(new TitleGeneration("Título de teste", "mock"));
    public Task<TitleGeneration> GenerateTitleAsync(string model, string question, CancellationToken ct) { Calls++; return Action(model, question, ct); }
}
sealed class TitleHandler(string payload) : HttpMessageHandler, IHttpClientFactory {
    public HttpClient CreateClient(string name) => new(this, false);
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)); var root = body.RootElement;
        if (root.GetProperty("model").GetString() != "selected" || root.GetProperty("max_tokens").GetInt32() != 64 || root.GetProperty("stream").GetBoolean() || root.GetProperty("messages").GetArrayLength() != 2) throw new Exception("Unexpected title payload");
        return new(HttpStatusCode.OK) { Content = new StringContent(payload) };
    }
}
