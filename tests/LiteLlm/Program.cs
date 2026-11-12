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
using System.Security.Claims;
using System.Text.Json;

void Check(bool ok, string text) { if (!ok) throw new Exception(text); }
var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
    ["LITELLM_ENABLED"]="true", ["LITELLM_API_KEY"]="synthetic-key", ["LITELLM_BASE_URL"]="http://proxy.test/v1/",
    ["LITELLM_MODELS"]="fast,balanced,reasoning", ["AI_DEFAULT_PROVIDER"]="litellm",
    ["DEEPSEEK_API_KEY"]="synthetic-key", ["DEEPSEEK_BASE_URL"]="http://direct.test/", ["DEEPSEEK_MODELS"]="direct-model"
}).Build();
await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
var scope = new OrganizationScope();
void SetScope(string property, int value) => typeof(OrganizationScope).GetProperty(property)!.SetValue(scope,value);
SetScope("Id",1); SetScope("UserId",1);
await using var db = new PortalDbContext(new DbContextOptionsBuilder<PortalDbContext>().UseSqlite(connection).Options,scope);
await DatabaseInitializer.InitializeAsync(db,cfg);
db.Users.Add(new User {UserName="Synthetic",UserEmail="test@example.test",PasswordHash="synthetic",IsActive=true}); await db.SaveChangesAsync();
var http = new HttpContextAccessor {HttpContext=new DefaultHttpContext {TraceIdentifier="synthetic-correlation",User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"1")],"test"))}};
var catalog = new AiProviderCatalog(db,cfg); var wire = new Wire();
var adapter = new LiteLlmService(wire,cfg,catalog,scope,http);
var router = new AiProviderRouter([adapter,new DeepSeekService(wire,cfg,catalog)],catalog,http);
var info=await catalog.Get("litellm",default);
info=await catalog.Save("litellm",new(true,info.Endpoint,info.Models,info.DefaultModel,info.Version),default);
Check(info.Version==1 && !JsonSerializer.Serialize(info).Contains("synthetic-key"),"Settings save or secret isolation failed");
var repo = new ChatRepository(db,http,catalog);
var usage = new ChatUsageService(db,http,router,router,new ModelPriceCatalog([]),NullLogger<ChatUsageService>.Instance,router);
var session=await repo.CreateSessionAsync(default);
var request=Guid.NewGuid(); var turn=await repo.BeginTurnAsync(session.Id,request,"Synthetic question","fixture",default);
var output=""; await foreach(var piece in usage.GenerateStreamAsync(session.Id,turn.UserMessage.Id,"fast","Synthetic",default)) output+=piece;
Check(output=="Answer","Streaming content lost");
var stored=await db.LlmUsages.SingleAsync();
Check(stored.InputTokens==7 && stored.OutputTokens==3 && stored.TotalTokens==10 && stored.Model=="upstream-model","Winning usage duplicated or upstream model lost");
Check(router.LastCall.UpstreamProvider=="groq" && router.LastCall.Fallbacks==1 && router.LastCall.Retries==1,"Gateway routing metadata lost");
Check(wire.Metadata.GetProperty("organization_id").GetInt32()==1 && wire.Metadata.GetProperty("request_id").GetString()=="synthetic-correlation","Trusted tenant/correlation missing");
Check(stored.EstimatedCostNanoUsd is null,"Unknown gateway cost fabricated");
await repo.CompleteTurnAsync(session.Id,request,output,default,model:usage.ActualModel,provider:usage.ActualProvider);
var titles=new ChatTitleService(db,http,router,NullLogger<ChatTitleService>.Instance,new ModelPriceCatalog([]),router);
await titles.TryGenerateAsync(session.Id,default);
Check(await db.LlmUsages.CountAsync()==2 && wire.Calls==2,"Title duplicated or failed to use requested route");
var prior=wire.Calls;
info=await catalog.Save("litellm",new(false,info.Endpoint,info.Models,info.DefaultModel,info.Version),default);
try { await router.Select("litellm","fast",default); throw new Exception("Disabled proxy accepted"); } catch(ChatException e) { Check(e.Status==409,"Wrong disabled response"); }
Check(wire.Calls==prior,"Disabled proxy sent traffic");
info=await catalog.Save("litellm",new(true,info.Endpoint,info.Models,info.DefaultModel,info.Version),default);
await router.Select("litellm","fast",default); wire.Error=429;
try { await router.GenerateAsync("fast","Synthetic"); throw new Exception("Expected 429"); } catch(AiProviderException e) { Check(e.Code=="rate_limit" && !e.Message.Contains("SECRET"),"Error leaked"); }
Check(wire.Calls==prior+1,"C# retried gateway and risked duplicate billing");
wire.Error=0; wire.Truncated=true;
try { await foreach(var _ in router.GenerateStreamAsync("fast","Synthetic")){} throw new Exception("Truncated stream accepted"); } catch(AiProviderException e) { Check(e.Code=="interrupted","Wrong stream failure"); }
wire.Truncated=false; wire.Wait=true;
using(var cancelled=new CancellationTokenSource()) {
    cancelled.Cancel();
    try { await router.GenerateAsync("fast","Synthetic",cancelled.Token); throw new Exception("Cancellation ignored"); } catch(OperationCanceledException) {}
}
wire.Wait=false; wire.Error=503;
try { await router.GenerateAsync("fast","Synthetic"); throw new Exception("Unavailable proxy accepted"); } catch(AiProviderException e) { Check(e.Code=="unavailable","Wrong unavailable classification"); }
wire.Error=0; await router.Select("deepseek","direct-model",default);
Check(!string.IsNullOrWhiteSpace(await router.GenerateAsync("direct-model","Synthetic")),"Explicit direct adapter depends on proxy");
await router.Select("litellm","fast",default);
SetScope("Id",0);
try { await router.GenerateAsync("fast","Synthetic"); throw new Exception("Missing tenant accepted"); } catch(ChatException e) { Check(e.Status==403,"Wrong tenant rejection"); }
Console.WriteLine("PASS: LiteLLM selection, trusted tenant/correlation, SSE/actual deployment, single usage + separate title, unknown cost, disabled/429/truncated/cancelled behavior without C# retry or secret leakage.");

sealed class Wire : HttpMessageHandler,IHttpClientFactory {
    public int Calls,Error; public bool Truncated,Wait; public JsonElement Metadata;
    public HttpClient CreateClient(string name)=>new(this,false);
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
        Calls++; if(Wait) await Task.Delay(Timeout.Infinite,ct);
        if(Error>0)return new((HttpStatusCode)Error){Content=new StringContent("SECRET")};
        if(request.Headers.Authorization?.Parameter!="synthetic-key")throw new Exception("Missing proxy credential");
        using var json=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)); var body=json.RootElement;
        if(body.TryGetProperty("metadata",out var metadata)) Metadata=metadata.Clone(); var stream=body.GetProperty("stream").GetBoolean();
        var message="{\"model\":\"fast\",\"choices\":[{\"message\":{\"content\":\"Titulo sintetico\"}}],\"usage\":{\"prompt_tokens\":7,\"completion_tokens\":3,\"total_tokens\":10}}";
        var text=stream ? "data: {\"choices\":[{\"delta\":{\"content\":\"Answer\"}}]}\n\ndata: {\"model\":\"fast\",\"choices\":[],\"usage\":{\"prompt_tokens\":7,\"completion_tokens\":3,\"total_tokens\":10}}\n\n"+(Truncated?"":"data: [DONE]\n\n") : message;
        var response=new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(text)};
        response.Headers.Add("x-litellm-model-id","groq|upstream-model|fast-fallback-1");
        response.Headers.Add("x-litellm-attempted-retries","1"); response.Headers.Add("x-litellm-attempted-fallbacks","1");
        return response;
    }
}
