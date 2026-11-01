using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

// Run against a dedicated, disposable Qdrant instance, never an existing index.
var root = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var qdrant = Environment.GetEnvironmentVariable("RAG_TEST_QDRANT")
    ?? throw new InvalidOperationException("Set RAG_TEST_QDRANT to a disposable Qdrant instance.");
using var qdrantHttp = new HttpClient();
var initial = await qdrantHttp.GetFromJsonAsync<JsonElement>($"{qdrant}/collections");
if (initial.GetProperty("result").GetProperty("collections").GetArrayLength() != 0)
    throw new InvalidOperationException("Test requires an empty, disposable Qdrant instance.");

var builder = WebApplication.CreateBuilder();
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
await using var fake = builder.Build();
string? lastPrompt = null;
fake.MapPost("/chat/completions", async (HttpContext context) =>
{
    using var body = await JsonDocument.ParseAsync(context.Request.Body);
    lastPrompt = body.RootElement.GetProperty("messages")[0].GetProperty("content").GetString();
    context.Response.ContentType = "text/event-stream";
    await context.Response.WriteAsync("data: {\"choices\":[{\"delta\":{\"content\":\"Resposta vetorial sintetica.\"}}]}\n\ndata: [DONE]\n\n");
});
await fake.StartAsync();
var fakeUrl = fake.Services.GetRequiredService<IServer>().Features
    .Get<IServerAddressesFeature>()!.Addresses.Single();
using var probe = new TcpListener(IPAddress.Loopback, 0);
probe.Start();
var port = ((IPEndPoint)probe.LocalEndpoint).Port;
probe.Stop();
var temp = Path.Combine(Path.GetTempPath(), "portal-rag-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
var secret = Guid.NewGuid().ToString("N");
var start = new ProcessStartInfo("dotnet")
{
    WorkingDirectory = Path.Combine(root, "ai-gateway"),
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    CreateNoWindow = true
};
start.ArgumentList.Add(Path.Combine(root, "ai-gateway/bin/Debug/net8.0/AiGateway.dll"));
var settings = new Dictionary<string, string>
{
    ["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}",
    ["ConnectionStrings__PortalConnection"] = $"Data Source={Path.Combine(temp, "portal.db")};Foreign Keys=True",
    ["Bootstrap__AdminEmail"] = "rag@example.test",
    ["Bootstrap__AdminPassword"] = secret,
    ["Jwt__Key"] = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
    ["Chat__UseAdvancedRag"] = "true",
    ["DEEPSEEK_BASE_URL"] = fakeUrl,
    ["DEEPSEEK_API_KEY"] = "synthetic-only",
    ["Qdrant__Endpoint"] = qdrant,
    ["Embedding__ModelPath"] = Path.Combine(root, "models/embeddings/nomic-v15/model.onnx"),
    ["Embedding__TokenizerPath"] = Path.Combine(root, "models/embeddings/nomic-v15/tokenizer.json"),
    ["Reranker__ModelPath"] = Path.Combine(root, "models/reranking/model.int8.onnx"),
    ["Reranker__TokenizerPath"] = Path.Combine(root, "models/reranking/tokenizer.json"),
    ["RAG__MinScore"] = "-1",
    // Any accidental Trino call must fail rather than reaching a real service.
    ["TRINO_URL"] = "http://127.0.0.1:1"
};
foreach (var setting in settings) start.Environment[setting.Key] = setting.Value;
using var process = Process.Start(start)!;
var output = process.StandardOutput.ReadToEndAsync();
var errors = process.StandardError.ReadToEndAsync();
try
{
    using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromMinutes(3) };
    var ready = false;
    for (var attempt = 0; attempt < 120; attempt++)
    {
        if (process.HasExited) throw new Exception("Gateway exited during startup.");
        try { using var r = await http.GetAsync("/swagger/v1/swagger.json"); ready = r.IsSuccessStatusCode; }
        catch (HttpRequestException) { }
        if (ready) break;
        await Task.Delay(500);
    }
    if (!ready) throw new Exception("Gateway startup timeout.");
    using var login = await http.PostAsJsonAsync("/api/auth/login", new { email = "rag@example.test", password = secret });
    login.EnsureSuccessStatusCode();
    var auth = await login.Content.ReadFromJsonAsync<JsonElement>();
    http.DefaultRequestHeaders.Authorization = new("Bearer", auth.GetProperty("token").GetString());
    async Task<Guid> NewSession() { using var created = await http.PostAsJsonAsync("/api/chatBot/sessions", new { }); created.EnsureSuccessStatusCode(); var body = await created.Content.ReadFromJsonAsync<JsonElement>(); return body.GetProperty("id").GetGuid(); }
    var emptySession = await NewSession();
    using var empty = await http.PostAsJsonAsync("/api/ia/ask/stream", new
    {
        pergunta = "Verificacao com indice vazio", sessionId = emptySession.ToString()
    });
    empty.EnsureSuccessStatusCode();
    var emptyText = await empty.Content.ReadAsStringAsync();
    if (emptyText.Contains("event: error") || !emptyText.Contains("[*ENDSTREAN*]"))
        throw new Exception("Empty vector index caused a chat failure: " + emptyText);
    using var saved = await http.PostAsJsonAsync("/api/kb/upsert", new
    {
        keyName = "synthetic_rag", section = "Teste", title = "Protocolo sintetico",
        content = "O protocolo TEST-RAG-MARKER realiza conciliacao sintetica de recebiveis.", isActive = true
    });
    if (!saved.IsSuccessStatusCode) throw new Exception("KB upsert failed: " + await saved.Content.ReadAsStringAsync());
    var streamSession = await NewSession();
    using var stream = await http.PostAsJsonAsync("/api/ia/ask/stream", new
    {
        pergunta = "Explique o protocolo de conciliacao de recebiveis", sessionId = streamSession.ToString(), userId = "spoofed-user"
    });
    stream.EnsureSuccessStatusCode();
    var text = await stream.Content.ReadAsStringAsync();
    if (text.Contains("event: error") || !text.Contains("[*ENDSTREAN*]"))
        throw new Exception("Chat stream failed: " + text);
    if (lastPrompt?.Contains("TEST-RAG-MARKER") != true)
        throw new Exception("Retrieved synthetic knowledge did not reach the LLM prompt.");
    var history = await http.GetFromJsonAsync<JsonElement>($"/api/chatBot/history/{streamSession}");
    if (history.GetArrayLength() != 2) throw new Exception("Owner could not retrieve persisted messages.");
    using var groupResponse = await http.PostAsJsonAsync("/api/roles", new { name = "Chat sintético", permissions = new[] { "chat.access" } });
    groupResponse.EnsureSuccessStatusCode();
    var group = await groupResponse.Content.ReadFromJsonAsync<JsonElement>();
    using var memberResponse = await http.PostAsJsonAsync("/api/users", new { userName = "Outra conta sintética", userEmail = "other@example.test", password = secret, groupIds = new[] { group.GetProperty("id").GetInt32() } });
    memberResponse.EnsureSuccessStatusCode();
    using var otherLogin = await http.PostAsJsonAsync("/api/auth/login", new { email = "other@example.test", password = secret });
    var other = await otherLogin.Content.ReadFromJsonAsync<JsonElement>();
    http.DefaultRequestHeaders.Authorization = new("Bearer", other.GetProperty("token").GetString());
    using var hiddenHistory = await http.GetAsync($"/api/chatBot/history/{streamSession}");
    if (hiddenHistory.StatusCode != HttpStatusCode.NotFound) throw new Exception("Another user could identify private conversation history.");
    Console.WriteLine("PASS: conversation ownership is derived from JWT; another authenticated account cannot read history.");
    Console.WriteLine("PASS: synthetic login, empty index, real ONNX embedding, Qdrant index/search, real reranking and chat streaming; SQLite isolated, LLM simulated, no Trino.");
}
finally
{
    if (!process.HasExited) process.Kill(entireProcessTree: true);
    await process.WaitForExitAsync();
    // Logs contain only synthetic test data; no real credentials or documents.
    Console.WriteLine(await output);
    Console.WriteLine(await errors);
    Directory.Delete(temp, recursive: true);
    await fake.StopAsync();
}
