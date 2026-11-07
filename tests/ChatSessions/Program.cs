using AiGateway.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Portal.AI.Gateway.Infrastructure;
using System.Security.Claims;

var folder = Path.Combine(Path.GetTempPath(), "portal-session-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["SQLITE_BACKUP_DIRECTORY"] = Path.Combine(folder, "backups") }).Build();
PortalDbContext Context(string path) => new(new DbContextOptionsBuilder<PortalDbContext>().UseSqlite($"Data Source={path};Foreign Keys=True").Options);
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
async Task Fails(Func<Task> action, int status) { try { await action(); throw new Exception("Expected failure"); } catch (ChatException ex) { Check(ex.Status == status, $"Wrong HTTP status {ex.Status}"); } }
async Task Legacy(string path, bool conflict = false) {
    await using var db = new PreOrganizationContext(new DbContextOptionsBuilder<PortalDbContext>().UseSqlite($"Data Source={path};Foreign Keys=True").Options);
    await db.Database.EnsureCreatedAsync();
    await db.Database.ExecuteSqlRawAsync("DROP TABLE AiUserPreferences; DROP TABLE AiSystemDefault; DROP TABLE AiProviderSettings; DROP TABLE LlmUsage; DROP TABLE ChatMessage; DROP TABLE ChatSession; CREATE TABLE ChatSession (Id TEXT PRIMARY KEY NOT NULL, UserId TEXT NULL, Title TEXT NULL, CreatedAt TEXT NOT NULL); CREATE TABLE ChatMessage (Id INTEGER PRIMARY KEY AUTOINCREMENT, SessionId TEXT NOT NULL REFERENCES ChatSession(Id) ON DELETE CASCADE, Role TEXT NOT NULL, Content TEXT NOT NULL, CreatedAt TEXT NOT NULL);");
    db.Users.AddRange(new User { UserName = "Synthetic A", UserEmail = "a@example.test", PasswordHash = "synthetic-not-a-password", IsActive = true }, new User { UserName = "Synthetic B", UserEmail = "b@example.test", PasswordHash = "synthetic-not-a-password", IsActive = true }); await db.SaveChangesAsync();
    var id = Guid.NewGuid(); var date = DateTime.UtcNow;
    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ChatSession VALUES ({id}, '1', 'Legado preservado', {date})");
    await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ChatMessage (SessionId, Role, Content, CreatedAt) VALUES ({id}, 'user', 'Conteúdo sintético legado', {date})");
    if (conflict) await db.Database.ExecuteSqlRawAsync("CREATE INDEX IX_ChatSession_OwnerUserId_DeletedAt_UpdatedAt_Id ON ChatSession(CreatedAt)");
}
IHttpContextAccessor Identity(int id) => new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test")) } };
try {
    using (var cancellation = new CancellationTokenSource()) {
        var provider = new AiGateway.Infrastructure.Qdrant.Services.LLMs.DeepSeekService(new BlockingFactory(cancellation), config);
        await using var iterator = provider.GenerateStreamAsync("deepseek-chat", "Synthetic cancellation", cancellation.Token).GetAsyncEnumerator();
        try { await iterator.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(3)); throw new Exception("Stalled stream did not cancel"); }
        catch (OperationCanceledException) { }
    }
    Console.WriteLine("PASS: stalled provider stream honors cancellation without synchronous read or network calls.");
    var path = Path.Combine(folder, "legacy.sqlite3"); await Legacy(path);
    await using (var db = Context(path)) {
        await ChatSchemaMigrator.InitializeAsync(db, config); await ChatSchemaMigrator.InitializeAsync(db, config);
        Check(await db.ChatSessions.CountAsync() == 1 && await db.ChatMessages.CountAsync() == 1, "Migration lost data");
        Check((await db.ChatSessions.SingleAsync()).OwnerUserId is null, "Legacy ownership was inferred");
        Check((await db.ChatMessages.SingleAsync()).Content == "Conteúdo sintético legado", "Legacy content changed");
        Check(await db.Users.CountAsync() == 2, "Users lost");
    }
    var backups = Directory.GetFiles(Path.Combine(folder, "backups"), "*.sqlite3"); Check(backups.Length == 1, "Migration repeated backup");
    var restored = Path.Combine(folder, "restored.sqlite3");
    using (var backup = new SqliteConnection($"Data Source={backups[0]}")) using (var restore = new SqliteConnection($"Data Source={restored}")) {
        backup.Open(); restore.Open(); backup.BackupDatabase(restore); using var c = restore.CreateCommand(); c.CommandText = "SELECT Content FROM ChatMessage"; Check((string?)c.ExecuteScalar() == "Conteúdo sintético legado", "Backup restore lost data");
        c.CommandText = "PRAGMA integrity_check"; Check((string?)c.ExecuteScalar() == "ok", "Restore failed integrity check");
    }
    await using (var db = Context(restored)) { await ChatSchemaMigrator.InitializeAsync(db, config); Check(await db.ChatMessages.CountAsync() == 1, "Restored database could not migrate"); }
    Console.WriteLine("PASS: legacy migration preserves users/content/IDs, quarantines uncertain ownership, is idempotent, and backup can be restored/migrated.");
    var broken = Path.Combine(folder, "rollback.sqlite3"); await Legacy(broken, true);
    await using (var db = Context(broken)) {
        try { await ChatSchemaMigrator.InitializeAsync(db, config); throw new Exception("Expected DDL failure"); } catch (SqliteException) { }
        await db.Database.OpenConnectionAsync(); using var c = db.Database.GetDbConnection().CreateCommand(); c.CommandText = "SELECT COUNT(*) FROM pragma_table_info('ChatSession') WHERE name='OwnerUserId'";
        Check(Convert.ToInt64(await c.ExecuteScalarAsync()) == 0, "Failed DDL was not rolled back");
        c.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name='SchemaVersions'"; Check(Convert.ToInt64(await c.ExecuteScalarAsync()) == 0, "Failed migration was recorded");
    }
    Console.WriteLine("PASS: injected DDL failure rolls back columns/version without losing legacy rows.");
    await using (var db = Context(path)) {
        var a = new ChatRepository(db, Identity(1)); var session = await a.CreateSessionAsync(default);
        Check(session.Title == "Nova conversa", "Wrong default title");
        var request = Guid.NewGuid(); var turn = await a.BeginTurnAsync(session.Id, request, "Pergunta sintética", "fingerprint", default);
        await Fails(async () => { await a.BeginTurnAsync(session.Id, Guid.NewGuid(), "Outro envio", "other", default); }, 409);
        await a.CompleteTurnAsync(session.Id, request, "Resposta sintética", default);
        var replay = await a.BeginTurnAsync(session.Id, request, "Pergunta sintética", "fingerprint", default); Check(replay.Replayed && replay.Answer!.Content == "Resposta sintética", "Completed request was not replayed");
        await Fails(async () => { await a.BeginTurnAsync(session.Id, request, "Texto alterado", "different", default); }, 409);
        await db.ChatMessages.Where(m => m.SessionId == session.Id).ExecuteUpdateAsync(u => u.SetProperty(m => m.CreatedAt, turn.UserMessage.CreatedAt));
        var newest = await a.ListMessagesAsync(session.Id, 1, null, default); Check(newest.Items.Count == 1 && newest.NextCursor is not null, "Messages not paginated");
        var older = await a.ListMessagesAsync(session.Id, 1, newest.NextCursor, default); Check(older.Items[0].Id == turn.UserMessage.Id, "Message cursor skipped question");
        await a.CreateSessionAsync(default); var list = await a.ListSessionsAsync(1, null, null, default); Check(list.NextCursor is not null, "Sessions not paginated");
        Check((await a.ListSessionsAsync(1, list.NextCursor, null, default)).Items.Count == 1, "Session cursor failed");
        // Each context has its own identity accessor; do not mutate an accessor shared by A.
        await using var otherDb = Context(path); var b = new ChatRepository(otherDb, Identity(2));
        Check((await b.ListSessionsAsync(30, null, null, default)).Items.Count == 0, "Session leaked in list");
        await Fails(async () => { await b.GetSessionAsync(session.Id, default); }, 404);
        await Fails(async () => { await b.ListMessagesAsync(session.Id, 50, null, default); }, 404);
        await Fails(async () => { await b.RenameAsync(session.Id, new("Intrusão", 0), default); }, 404);
        await Fails(async () => { await b.DeleteAsync(session.Id, default); }, 404);
        await Fails(async () => { await b.BeginTurnAsync(session.Id, Guid.NewGuid(), "Intrusão", "x", default); }, 404);
        await Fails(async () => { await b.AddMessageAsync(new ChatMessage { SessionId = session.Id, Role = "user", Content = "Intrusão" }, default); }, 404);
    }
    // HttpContextAccessor uses AsyncLocal; use a fresh owner context for continuation.
    await using (var db = Context(path)) {
        var a = new ChatRepository(db, Identity(1)); var sessions = await a.ListSessionsAsync(30, null, null, default); var id = sessions.Items.Single(s => s.Title == "Nova conversa" && s.Version > 0).Id;
        var s = await a.GetSessionAsync(id, default); await a.RenameAsync(id, new("Título manual", s.Version), default);
        await Fails(async () => { await a.RenameAsync(id, new("Versão antiga", s.Version), default); }, 409);
        var failed = Guid.NewGuid(); await a.BeginTurnAsync(id, failed, "Falha preservada", "fail", default); await a.FailTurnAsync(id, failed, "failed", default);
        Check((await a.ListMessagesAsync(id, 50, null, default)).Items.Any(m => m.Status == "failed"), "Failed question lost");
        var abandoned = Guid.NewGuid(); await a.BeginTurnAsync(id, abandoned, "Interrompida", "abandoned", default);
        await db.ChatSessions.Where(x => x.Id == id).ExecuteUpdateAsync(u => u.SetProperty(x => x.LeaseUntil, DateTime.UtcNow.AddMinutes(-1)));
        db.ChangeTracker.Clear();
        var retry = Guid.NewGuid(); await a.BeginTurnAsync(id, retry, "Nova tentativa explícita", "retry", default);
        await a.FailTurnAsync(id, retry, "cancelled", default);
        var states = (await a.ListMessagesAsync(id, 50, null, default)).Items;
        Check(states.Any(m => m.Status == "interrupted") && states.Any(m => m.Status == "cancelled"), "Expired lease/cancellation not preserved");
        await a.DeleteAsync(id, default); await Fails(async () => { await a.GetSessionAsync(id, default); }, 404);
        Check(await db.ChatMessages.CountAsync(m => m.SessionId == id) == 5, "Logical delete erased messages");
    }
    await using (var fresh = Context(Path.Combine(folder, "fresh.sqlite3"))) { await ChatSchemaMigrator.InitializeAsync(fresh, config); await ChatSchemaMigrator.InitializeAsync(fresh, config); }
    await using (var db = Context(path)) {
        await db.Database.ExecuteSqlRawAsync("UPDATE SchemaVersions SET Checksum='invalid'");
        try { await ChatSchemaMigrator.InitializeAsync(db, config); throw new Exception("Checksum not enforced"); } catch (InvalidOperationException) { }
    }
    Console.WriteLine("PASS: JWT ownership on every repository operation, pagination, idempotent replay/conflict, failed questions, rename/version, soft deletion, fresh schema and checksum enforcement.");
} finally { SqliteConnection.ClearAllPools(); Directory.Delete(folder, true); }

sealed class BlockingFactory(CancellationTokenSource cancellation) : IHttpClientFactory {
    public HttpClient CreateClient(string name) => new(new BlockingHandler(cancellation));
}
sealed class BlockingHandler(CancellationTokenSource cancellation) : HttpMessageHandler {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StreamContent(new BlockingStream(cancellation)) });
}
sealed class BlockingStream(CancellationTokenSource cancellation) : Stream {
    public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) => throw new Exception("Synchronous provider read must not be used");
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { cancellation.CancelAfter(TimeSpan.FromMilliseconds(50)); await Task.Delay(Timeout.Infinite, cancellationToken); return 0; }
    public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
