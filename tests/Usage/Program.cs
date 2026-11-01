using AiGateway.Dtos;
using AiGateway.Infrastructure.Qdrant.Services.LLMs;
using AiGateway.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Portal.AI.Gateway.Application.Interfaces;
using Portal.AI.Gateway.Infrastructure;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

var root = Path.Combine(Path.GetTempPath(), "portal-usage-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["SQLITE_BACKUP_DIRECTORY"] = Path.Combine(root, "backups") }).Build();
PortalDbContext Context(string path) => new(new DbContextOptionsBuilder<PortalDbContext>().UseSqlite($"Data Source={path};Foreign Keys=True").Options);
void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
IHttpContextAccessor Identity(int id, bool admin = false) => new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }.Concat(admin ? [new Claim("perm", PortalAccess.UsageAdmin)] : []), "test")) } };
async Task Denied(Func<Task> action, int status) { try { await action(); throw new Exception("Expected rejection"); } catch (ChatException ex) { Check(ex.Status == status, "Wrong authorization status"); } }
try {
    var path = Path.Combine(root, "usage.sqlite3");
    Guid oldSession;
    await using (var db = Context(path)) {
        await ChatSchemaMigrator.InitializeAsync(db, config);
        db.Users.AddRange(new User { UserName = "Synthetic A", UserEmail = "a@example.test", PasswordHash = "test-only", IsActive = true }, new User { UserName = "Synthetic B", UserEmail = "b@example.test", PasswordHash = "test-only", IsActive = true }); await db.SaveChangesAsync();
        var repo = new ChatRepository(db, Identity(1)); var session = await repo.CreateSessionAsync(default); oldSession = session.Id;
        var request = Guid.NewGuid(); await repo.BeginTurnAsync(oldSession, request, "Synthetic legacy question", "old", default); var answer = await repo.CompleteTurnAsync(oldSession, request, "Synthetic legacy answer", default);
        db.LlmUsages.Add(new() { Id = Guid.NewGuid(), OwnerUserId = 1, SessionId = oldSession, MessageId = answer.Id, Purpose = "title", Model = "deepseek-chat", Provider = "deepseek", Source = "reported", Status = "completed", TotalTokens = 10, CreatedAt = DateTime.UtcNow.AddMonths(-1) }); await db.SaveChangesAsync();
        // Only this disposable fixture is reverted to the v2 shape; no application database is touched.
        await db.Database.ExecuteSqlRawAsync("DROP INDEX IX_LlmUsage_MessageId_Purpose; DROP INDEX IX_LlmUsage_SessionId_CreatedAt; DROP INDEX IX_LlmUsage_Purpose_CreatedAt; DROP INDEX IX_LlmUsage_CreatedAt_Id; ALTER TABLE LlmUsage DROP COLUMN EstimatedCostNanoUsd; ALTER TABLE LlmUsage DROP COLUMN PriceVersion; DELETE FROM SchemaVersions WHERE Version=3; CREATE TABLE Collision(Id INTEGER); CREATE INDEX IX_LlmUsage_MessageId_Purpose ON Collision(Id)");
        try { await ChatSchemaMigrator.InitializeAsync(db, config); throw new Exception("Expected DDL rollback"); } catch (SqliteException) { }
        using var query = db.Database.GetDbConnection().CreateCommand(); query.CommandText = "SELECT COUNT(*) FROM pragma_table_info('LlmUsage') WHERE name='EstimatedCostNanoUsd'";
        Check(Convert.ToInt64(await query.ExecuteScalarAsync()) == 0, "Failed v3 DDL not rolled back");
        query.CommandText = "SELECT COUNT(*) FROM SchemaVersions WHERE Version=3"; Check(Convert.ToInt64(await query.ExecuteScalarAsync()) == 0, "Failed v3 recorded");
        await db.Database.ExecuteSqlRawAsync("DROP TABLE Collision"); await ChatSchemaMigrator.InitializeAsync(db, config); await ChatSchemaMigrator.InitializeAsync(db, config);
        Check(Directory.GetFiles(Path.Combine(root, "backups")).Length == 2, "v3 backup repeated");
        Check(await db.ChatMessages.CountAsync() == 2 && (await db.LlmUsages.AsNoTracking().SingleAsync()).TotalTokens == 10 && (await db.ChatSessions.AsNoTracking().SingleAsync()).OwnerUserId == 1, "Migration lost existing usage/messages/ownership");
    }
    Console.WriteLine("PASS: v2 to v3 backup, transactional rollback/checksum ledger, repeated initialization preserves users/messages/usage/owners.");

    var prices = new ModelPriceCatalog([
        new("fixture-v1", "deepseek", "actual-model", DateTimeOffset.UtcNow.AddYears(-1), 2m, 4m, .5m),
        new("fixture-future", "deepseek", "actual-model", DateTimeOffset.UtcNow.AddYears(1), 20m, 40m, 5m)
    ]); // Synthetic rates for arithmetic tests, not actual provider pricing.
    LlmUsage PriceFixture() => new() { Provider = "deepseek", Model = "actual-model", Source = "reported", CreatedAt = DateTime.UtcNow, InputTokens = 1_000_000, CacheTokens = 250_000, OutputTokens = 500_000 };
    var known = PriceFixture(); prices.Apply(known); Check(known.EstimatedCostNanoUsd == 3_625_000_000 && known.PriceVersion == "fixture-v1", "Cache/pricing/version arithmetic incorrect");
    var missing = PriceFixture(); missing.CacheTokens = null; prices.Apply(missing); Check(missing.EstimatedCostNanoUsd is null, "Unknown cache fabricated cost");
    var unknown = PriceFixture(); unknown.Model = "unknown"; prices.Apply(unknown); Check(unknown.EstimatedCostNanoUsd is null, "Unknown model fabricated price");
    var invalid = PriceFixture(); invalid.CacheTokens = 2_000_000; prices.Apply(invalid); Check(invalid.EstimatedCostNanoUsd is null, "Invalid cache cost accepted");
    var priceFile = Path.Combine(root, "prices.json"); File.WriteAllText(priceFile, "[{\"version\":\"missing-fields\"}]");
    try { ModelPriceCatalog.Load(priceFile); throw new Exception("Missing rates interpreted as zero"); } catch (JsonException) { }
    File.WriteAllText(priceFile, "[]"); Check(ModelPriceCatalog.Load(priceFile) is not null, "Empty price list rejected");
    Console.WriteLine("PASS: configured/versioned price arithmetic uses cache subset, future prices do not apply, unknowns stay null, missing rates cannot silently become zero.");

    await using (var db = Context(path)) {
        var identity = Identity(1); var repo = new ChatRepository(db, identity); var fake = new FakeLlm();
        var executor = new ChatUsageService(db, identity, fake, fake, prices, NullLogger<ChatUsageService>.Instance);
        async Task<(Guid Session, Guid Request, long Message)> Turn() {
            var session = await repo.CreateSessionAsync(default); var request = Guid.NewGuid(); var turn = await repo.BeginTurnAsync(session.Id, request, "Synthetic question", "fp", default); return (session.Id, request, turn.UserMessage.Id);
        }
        var good = await Turn(); await executor.GenerateAsync(good.Session, good.Message, "chat", "requested-alias", "Prompt must not appear in telemetry", default); await repo.CompleteTurnAsync(good.Session, good.Request, "Answer", default);
        var measured = await db.LlmUsages.AsNoTracking().SingleAsync(u => u.SessionId == good.Session);
        Check(measured.Status == "completed" && measured.Model == "actual-model" && measured.TotalTokens == 1_500_000 && measured.EstimatedCostNanoUsd == 3_625_000_000, "Measured chat not persisted/priced");
        Check((await repo.BeginTurnAsync(good.Session, good.Request, "Synthetic question", "fp", default)).Replayed, "Replay not recognized");
        Check(await db.LlmUsages.CountAsync(u => u.SessionId == good.Session) == 1, "Replay duplicated usage");
        var failed = await Turn(); fake.Fail = true;
        try { await executor.GenerateAsync(failed.Session, failed.Message, "document", "selected", "Sensitive document", default); throw new Exception("Expected provider failure"); } catch (HttpRequestException) { }
        var failedRow = await db.LlmUsages.AsNoTracking().SingleAsync(u => u.SessionId == failed.Session);
        Check(failedRow.Status == "failed" && failedRow.TotalTokens is null && failedRow.EstimatedCostNanoUsd is null, "Failure invented usage/cost");
        await repo.FailTurnAsync(failed.Session, failed.Request, "failed", default);
        var timed = await Turn(); fake.Fail = false; using var cancel = new CancellationTokenSource(); fake.Cancel = cancel;
        try { await executor.GenerateAsync(timed.Session, timed.Message, "chat", "selected", "Synthetic timeout", cancel.Token); } catch (OperationCanceledException) { }
        Check((await db.LlmUsages.AsNoTracking().SingleAsync(u => u.SessionId == timed.Session)).Status == "timeout", "Timeout not tracked separately");
        await repo.FailTurnAsync(timed.Session, timed.Request, "cancelled", default); fake.Cancel = null;
        var ownerQuery = new UsageQueryService(db, identity); var period = new UsageFilter(DateTime.UtcNow.AddDays(-90), DateTime.UtcNow.AddDays(1));
        var totals = await ownerQuery.Summary(period, false, default);
        Check(totals.Requests == 4 && totals.Errors == 2 && totals.TotalTokens == 1_500_010 && !totals.TokensComplete && !totals.CostComplete && totals.EstimatedCostUsd == 3.625m, "Partial aggregate null semantics/arithmetic wrong");
        foreach (var by in new[] { "day", "month", "model", "provider", "purpose" }) Check(JsonSerializer.Serialize(await ownerQuery.Groups(period, false, by, 1, 30, default)).Contains("totals"), "Groups not available");
        var firstPage = JsonSerializer.Serialize(await ownerQuery.Calls(period, 1, 1, default)); Check(!firstPage.Contains("Sensitive") && !firstPage.Contains("Prompt"), "Telemetry leaked content");
        await Denied(async () => await ownerQuery.Summary(period with { UserId = 2 }, false, default), 400);
        var foreign = Identity(2); var foreignQuery = new UsageQueryService(db, foreign);
        Check((await foreignQuery.Summary(period, false, default)).Requests == 0, "Foreign usage leaked");
        await Denied(async () => await foreignQuery.Summary(period with { SessionId = good.Session }, false, default), 404);
        await Denied(async () => await foreignQuery.Summary(period, true, default), 403);
        var foreignRepo = new ChatRepository(db, foreign); var bSession = await foreignRepo.CreateSessionAsync(default); var bRequest = Guid.NewGuid(); var bTurn = await foreignRepo.BeginTurnAsync(bSession.Id, bRequest, "B question", "b", default);
        var calls = fake.Calls; _ = Identity(1);
        await Denied(async () => await executor.GenerateAsync(bSession.Id, bTurn.UserMessage.Id, "chat", "selected", "Must not send", default), 404); Check(fake.Calls == calls, "Foreign usage reached provider");
        var admin = Identity(1, true); var adminQuery = new UsageQueryService(db, admin);
        Check((await adminQuery.Summary(period, true, default)).Requests == 4, "Admin summary failed");
        await Denied(async () => await adminQuery.Summary(period with { SessionId = bSession.Id }, true, default), 404);
        await repo.DeleteAsync(good.Session, default);
        Check((await adminQuery.Summary(period, false, default)).Requests == 4, "Soft deletion erased accounting");
        await Denied(async () => await adminQuery.Summary(period with { SessionId = good.Session }, false, default), 404);
    }
    Console.WriteLine("PASS: calls/replay/failures/timeouts, partial totals/costs, SQL grouping, owner isolation/admin policy and soft-delete accounting retention.");

    foreach (var emptyChoices in new[] { true, false }) {
        var usageChunk = JsonSerializer.Serialize(new { model = "resolved-stream-model", choices = emptyChoices ? Array.Empty<object>() : new object[] { new { delta = new { }, finish_reason = "stop" } }, usage = new { prompt_tokens = 17, completion_tokens = 5, total_tokens = 22, prompt_cache_hit_tokens = 7 } });
        var content = "data: {\"choices\":[{\"delta\":{\"content\":\"Answer\"}}]}\n\ndata: " + usageChunk + "\n\ndata: [DONE]\n\n";
        var adapter = new DeepSeekService(new FixtureHttp(content, true), config); var answer = new StringBuilder();
        await foreach (var piece in adapter.GenerateStreamAsync("selected", "Synthetic", default)) answer.Append(piece);
        Check(answer.ToString() == "Answer" && adapter.LastCall.TotalTokens == 22 && adapter.LastCall.CacheTokens == 7 && adapter.LastCall.Model == "resolved-stream-model", "Stream usage/content parsing incorrect");
    }
    var truncated = new DeepSeekService(new FixtureHttp("data: {\"choices\":[{\"delta\":{\"content\":\"Partial\"}}],\"usage\":{\"total_tokens\":9}}\n\n", true), config);
    try { await foreach (var _ in truncated.GenerateStreamAsync("selected", "Synthetic", default)) { } throw new Exception("Truncated stream accepted"); } catch (IOException) { }
    Check(truncated.LastCall.TotalTokens == 9 && truncated.LastCall.Status != "completed", "Truncation lost real partial usage");
    var missingMetrics = new DeepSeekService(new FixtureHttp("{\"choices\":[{\"message\":{\"content\":\"Answer\"}}]}", false), config);
    await missingMetrics.GenerateAsync("selected", "Synthetic", default); Check(missingMetrics.LastCall.TotalTokens is null && missingMetrics.LastCall.Source == "unavailable", "Missing tokens invented");
    Console.WriteLine("PASS: real adapter handles usage with content or empty choices, resolved models, abrupt EOF and unavailable metrics; no external calls.");
    await using (var db = Context(path)) {
        await db.Database.ExecuteSqlRawAsync("UPDATE SchemaVersions SET Checksum='invalid' WHERE Version=3");
        try { await ChatSchemaMigrator.InitializeAsync(db, config); throw new Exception("v3 checksum ignored"); } catch (InvalidOperationException) { }
    }
} finally { SqliteConnection.ClearAllPools(); Directory.Delete(root, true); }

sealed class FakeLlm : IOllamaService, ILlmMetrics {
    public bool Fail; public int Calls; public CancellationTokenSource? Cancel;
    public LlmCallMetrics LastCall { get; private set; } = new();
    public Task<string> GenerateAsync(string model, string prompt, CancellationToken ct = default) {
        Calls++; LastCall = new() { Model = model };
        if (Fail) throw new HttpRequestException("Synthetic secret must not persist");
        if (Cancel is not null) { Cancel.Cancel(); ct.ThrowIfCancellationRequested(); }
        LastCall = new() { Model = "actual-model", Status = "completed", InputTokens = 1_000_000, OutputTokens = 500_000, TotalTokens = 1_500_000, CacheTokens = 250_000 };
        return Task.FromResult("Answer");
    }
    public async IAsyncEnumerable<string> GenerateStreamAsync(string model, string prompt, [EnumeratorCancellation] CancellationToken ct = default) { yield return await GenerateAsync(model, prompt, ct); }
}
sealed class FixtureHttp(string body, bool stream) : HttpMessageHandler, IHttpClientFactory {
    public HttpClient CreateClient(string name) => new(this, false);
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
        using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
        if (stream && !json.RootElement.GetProperty("stream_options").GetProperty("include_usage").GetBoolean()) throw new Exception("Usage not requested");
        return new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, stream ? "text/event-stream" : "application/json") };
    }
}
