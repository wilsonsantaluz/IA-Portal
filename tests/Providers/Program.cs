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
using System.Text;
using System.Text.Json;

var folder = Path.Combine(Path.GetTempPath(), "portal-provider-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
async Task Denied(Func<Task> call, int status) { try { await call(); throw new Exception("Expected rejection"); } catch (ChatException e) { Check(e.Status == status, "Unexpected status"); } }
var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
    ["SQLITE_BACKUP_DIRECTORY"] = Path.Combine(folder, "backups"),
    ["DEEPSEEK_API_KEY"] = "synthetic-key", ["DEEPSEEK_BASE_URL"] = "http://deepseek.test/", ["DEEPSEEK_MODELS"] = "dk-a,dk-b", ["DEEPSEEK_DEFAULT_MODEL"] = "dk-a",
    ["OPENAI_API_KEY"] = "synthetic-key", ["OPENAI_BASE_URL"] = "http://openai.test/v1/", ["OPENAI_ENABLED"] = "true", ["OPENAI_MODELS"] = "oa-a", ["OPENAI_DEFAULT_MODEL"] = "oa-a",
    ["OLLAMA_BASE_URL"] = "http://ollama.test:11434/", ["OLLAMA_ALLOWED_ORIGINS"] = "http://ollama.test:11434", ["OLLAMA_LOCAL_ORIGINS"] = "http://ollama.test:11434", ["OLLAMA_ENABLED"] = "true", ["OLLAMA_MODELS"] = "local-a"
}).Build();
var path = Path.Combine(folder, "portal.db");
PortalDbContext Context() => new(new DbContextOptionsBuilder<PortalDbContext>().UseSqlite($"Data Source={path};Foreign Keys=True").Options);
IHttpContextAccessor Identity(int id) => new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test")) } };
try {
    await using (var db = Context()) {
        await ChatSchemaMigrator.InitializeAsync(db, cfg);
        db.Users.AddRange(new User { UserName = "Synthetic A", UserEmail = "a@example.test", PasswordHash = "test-only", IsActive = true }, new User { UserName = "Synthetic B", UserEmail = "b@example.test", PasswordHash = "test-only", IsActive = true }); await db.SaveChangesAsync();
        var identity = Identity(1); var catalog = new AiProviderCatalog(db, cfg); var repo = new ChatRepository(db, identity, catalog);
        var wire = new Wire(); IAiProviderAdapter[] adapters = [new DeepSeekService(wire, cfg, catalog), new OpenAiService(wire, cfg, catalog), new OllamaService(wire, cfg, catalog)];
        var router = new AiProviderRouter(adapters, catalog, identity); var prices = new ModelPriceCatalog([]);
        var usage = new ChatUsageService(db, identity, router, router, prices, NullLogger<ChatUsageService>.Instance, router);
        var title = new ChatTitleService(db, identity, router, NullLogger<ChatTitleService>.Instance, prices, router);
        Check((await catalog.List(false, default)).Count == 3, "Configured providers not listed");
        foreach (var (provider, model, tokens) in new[] { ("deepseek", "dk-a", 22L), ("openai", "oa-a", 17L), ("ollama", "local-a", 20L) }) {
            await catalog.SetPreference(1, new(provider, model), default);
            var session = await repo.CreateSessionAsync(default); Check(session.Provider == provider && session.Model == model, "Preference not persisted/applied");
            var request = Guid.NewGuid(); var turn = await repo.BeginTurnAsync(session.Id, request, "Synthetic subject", "fp", default); var text = new StringBuilder();
            await foreach (var part in usage.GenerateStreamAsync(session.Id, turn.UserMessage.Id, model, "Synthetic prompt", default)) text.Append(part);
            Check(!text.ToString().Contains("SECRET_THINKING") && text.Length > 0, "Internal reasoning leaked");
            var answer = await repo.CompleteTurnAsync(session.Id, request, text.ToString(), default, model: usage.ActualModel, provider: usage.ActualProvider);
            Check(answer.Provider == provider && answer.Model == model + "-resolved" && (await repo.GetSessionAsync(session.Id, default)).Model == model, "Actual answer model overwrote selection or provider incorrect");
            await title.TryGenerateAsync(session.Id, default);
            var rows = await db.LlmUsages.AsNoTracking().Where(u => u.SessionId == session.Id).ToListAsync();
            Check(rows.Count == 2 && rows.All(u => u.Provider == provider && u.TotalTokens == tokens), "Chat/title provider usage wrong");
            Check((await repo.GetSessionAsync(session.Id, default)).TitleSource == "automatic", "Title did not use selected adapter/model");
            Check(rows.All(u => provider == "ollama" ? u.ApiBilling == "local" && u.EstimatedCostNanoUsd == 0 : u.EstimatedCostNanoUsd is null), "Local API billing confused with external unknown prices");
            var version = (await repo.GetSessionAsync(session.Id, default)).Version;
            await repo.ChangeModelAsync(session.Id, new("deepseek", "dk-b", version), default);
            Check((await repo.ListMessagesAsync(session.Id, 50, null, default)).Items.Count == 2, "Switch discarded history");
            var next = Guid.NewGuid(); await repo.BeginTurnAsync(session.Id, next, "Continue", "next", default);
            await Denied(async () => await repo.ChangeModelAsync(session.Id, new("openai", "oa-a", version + 2), default), 409);
            await repo.FailTurnAsync(session.Id, next, "cancelled", default);
            _ = Identity(2); await Denied(async () => await repo.ChangeModelAsync(session.Id, new("openai", "oa-a", 0), default), 404); _ = Identity(1);
        }
        Console.WriteLine("PASS: all three existing adapters honor selected models, preserve history on explicit switch, block active/foreign switches, generate titles and persist actual usage/models without exposing reasoning.");
        await catalog.SetPreference(1, new("openai", "oa-a"), default);
        var info = await catalog.Get("openai", default); await catalog.Save("openai", new(false, info.Endpoint, info.Models, info.DefaultModel, info.Version), default);
        var before = wire.Calls; await Denied(async () => await catalog.Default(1, default), 409); Check(wire.Calls == before, "Disabled preferred provider silently fell back");
        await Denied(async () => await catalog.Save("deepseek", new(true, "http://untrusted.invalid/", ["dk-a"], "dk-a", 0), default), 400);
        await Denied(async () => await catalog.Save("deepseek", new(true, "http://user:secret@deepseek.test/", ["dk-a"], "dk-a", 0), default), 400);
        Check(catalog.Billing("ollama", "https://remote.example/") == "unknown", "Remote Ollama labeled infrastructure-free");
        Check(!JsonSerializer.Serialize(await catalog.List(true, default)).Contains("synthetic-key"), "Provider listing leaked key");
        foreach (var adapter in adapters) {
            wire.Error = 429;
            try { await adapter.GenerateAsync(adapter.ProviderId == "openai" ? "oa-a" : adapter.ProviderId == "ollama" ? "local-a" : "dk-a", "Synthetic", default); throw new Exception("Expected rate limit"); }
            catch (AiProviderException e) { Check(e.Code == "rate_limit" && !e.Message.Contains("SECRET_PROVIDER_BODY"), "Provider error leaked/misclassified"); }
            catch (ChatException e) { Check(adapter.ProviderId == "openai" && e.Status == 409, "Unexpected disabled adapter error"); }
        }
        wire.Error = 0;
        // Destination and API billing are frozen together when a call is accepted.
        await router.Select("ollama", "local-a", default);
        var localInfo = await catalog.Get("ollama", default);
        cfg["OLLAMA_ALLOWED_ORIGINS"] = "http://ollama.test:11434,http://remote.test:11434";
        await catalog.Save("ollama", new(true, "http://remote.test:11434/", localInfo.Models, localInfo.DefaultModel, localInfo.Version), default);
        await router.GenerateAsync("local-a", "Snapshot fixture", default);
        Check(wire.LastHost == "ollama.test" && router.ApiBilling == "local", "Administrative edit redirected an accepted local call");
        await router.Select("ollama", "local-a", default); await router.GenerateAsync("local-a", "Next selection", default);
        Check(wire.LastHost == "remote.test" && router.ApiBilling == "unknown", "New selection reused stale billing/destination");
        var remote = await catalog.Get("ollama", default);
        await catalog.Save("ollama", new(true, localInfo.Endpoint, localInfo.Models, localInfo.DefaultModel, remote.Version), default);
        Console.WriteLine("PASS: unavailable preferences fail explicitly, endpoint allowlist/userinfo checks, remote Ollama costs unknown, no secret responses, consistent safe provider errors.");
        // Build v3 fixture from this disposable database and inject v4 DDL failure.
        await db.Database.ExecuteSqlRawAsync("DROP TABLE AiUserPreferences; DROP TABLE AiSystemDefault; DROP TABLE AiProviderSettings; ALTER TABLE LlmUsage DROP COLUMN ApiBilling; DELETE FROM SchemaVersions WHERE Version=4; CREATE TABLE AiUserPreferences(Id INTEGER)");
        try { await ChatSchemaMigrator.InitializeAsync(db, cfg); throw new Exception("Expected rollback"); } catch (SqliteException) { }
        using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name='AiProviderSettings'"; Check(Convert.ToInt64(await command.ExecuteScalarAsync()) == 0, "v4 DDL not rolled back");
        await db.Database.ExecuteSqlRawAsync("DROP TABLE AiUserPreferences"); await ChatSchemaMigrator.InitializeAsync(db, cfg); await ChatSchemaMigrator.InitializeAsync(db, cfg);
        Check(await db.ChatMessages.CountAsync() == 9 && await db.LlmUsages.CountAsync() == 6 && await db.Users.CountAsync() == 2, "v4 migration lost existing data");
        Check(Directory.GetFiles(Path.Combine(folder, "backups")).Length == 2, "v4 backup/idempotence wrong");
        await db.Database.ExecuteSqlRawAsync("UPDATE SchemaVersions SET Checksum='tampered' WHERE Version=4");
        try { await ChatSchemaMigrator.InitializeAsync(db, cfg); throw new Exception("Checksum ignored"); } catch (InvalidOperationException) { }
        Console.WriteLine("PASS: SQLite v3 to v4 backup/rollback/checksum/idempotence preserves users, messages, ownership and consumption.");
    }
} finally { SqliteConnection.ClearAllPools(); Directory.Delete(folder, true); }

sealed class Wire : HttpMessageHandler, IHttpClientFactory {
    public int Calls; public int Error;
    public string? LastHost;
    public HttpClient CreateClient(string name) => new(this, false);
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
        Calls++; LastHost = request.RequestUri!.Host; if (Error != 0) return new((HttpStatusCode)Error) { Content = new StringContent("SECRET_PROVIDER_BODY") };
        using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)); var root = json.RootElement;
        var model = root.GetProperty("model").GetString()!; var stream = root.GetProperty("stream").GetBoolean();
        var ollama = request.RequestUri!.AbsolutePath == "/api/generate"; var openai = request.RequestUri.Host == "openai.test";
        if (model.EndsWith("-resolved")) throw new Exception("Actual model alias was wrongly sent instead of configured selection");
        if (!ollama && request.Headers.Authorization?.Parameter != "synthetic-key") throw new Exception("Missing environment credential");
        var title = !stream && (ollama ? root.TryGetProperty("system", out _) : root.GetProperty("messages").GetArrayLength() == 2);
        if (title && !ollama && root.GetProperty(openai ? "max_completion_tokens" : "max_tokens").GetInt32() != 64) throw new Exception("Wrong title capability parameter");
        var content = title ? "Titulo de teste" : "Answer";
        string body;
        if (ollama) {
            var last = JsonSerializer.Serialize(new { model = model + "-resolved", response = stream ? "B" : content, done = true, prompt_eval_count = 11, eval_count = 9 });
            body = stream ? "{\"response\":\"A\",\"thinking\":\"SECRET_THINKING\",\"done\":false}\n" + last + "\n" : last;
        } else {
            var usage = openai ? new { prompt_tokens = 13, completion_tokens = 4, total_tokens = 17 } : new { prompt_tokens = 17, completion_tokens = 5, total_tokens = 22 };
            body = stream ? "data: {\"choices\":[{\"delta\":{\"content\":\"Answer\"}}]}\n\ndata: " + JsonSerializer.Serialize(new { model = model + "-resolved", choices = Array.Empty<object>(), usage }) + "\n\ndata: [DONE]\n\n" : JsonSerializer.Serialize(new { model = model + "-resolved", choices = new[] { new { message = new { content } } }, usage });
        }
        return new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, stream ? "text/plain" : "application/json") };
    }
}
