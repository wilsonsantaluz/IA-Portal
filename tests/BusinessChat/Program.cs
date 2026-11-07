using System.Security.Claims;
using AiGateway.Application.Interfaces;
using AiGateway.Dtos;
using AiGateway.Infrastructure.Qdrant.Services.Chats;
using AiGateway.Infrastructure.Sse;
using AiGateway.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Portal.AI.Gateway.Infrastructure;
using Portal.AI.Gateway.Infrastructure.Trino;

void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
foreach (var (question,intent,plan) in new (string,BusinessIntent,string?)[] {
    ("Qual política se aplica a uma fatura com 37 dias de atraso?",BusinessIntent.Documents,null),
    ("Compare os conteúdos seguintes",BusinessIntent.Documents,null),
    ("Mostre o aging por faixa",BusinessIntent.Analytics,"aging"),
    ("Mostre os dez maiores saldos em aberto",BusinessIntent.Analytics,"top_balances"),
    ("Compare faturamento e recebimentos nos últimos seis meses",BusinessIntent.Analytics,"cashflow"),
    ("Mostre o valor vencido e explique a política de cobrança",BusinessIntent.Hybrid,"aging"),
    ("Mostre a inadimplência dos últimos seis meses",BusinessIntent.Analytics,"aging_trend"),
    ("Qual seria a previsão do saldo em aberto?",BusinessIntent.Clarify,null)
}) { Check(BusinessIntentClassifier.Classify(question)==(intent,plan),"Intent classification failed: " + question); }

await using var connection = new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();
await using var db = new PortalDbContext(new DbContextOptionsBuilder<PortalDbContext>().UseSqlite(connection).Options);
var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
    ["DataConnectors:Sources:0:Id"]="enterprise_test",["DataConnectors:Sources:0:OrganizationSlug"]="default",
    ["DataConnectors:Sources:0:Category"]="enterprise",["DataConnectors:Sources:0:Endpoint"]="https://trino.example.test/",
    ["DataConnectors:Sources:0:Catalog"]="finance",["DataConnectors:Sources:0:Schema"]="receivables",
    ["DataConnectors:Sources:0:User"]="reader",["DataConnectors:Sources:0:ReadOnlyAccount"]="true",
    ["DataConnectors:Sources:0:Tables:0"]="invoices",["DataConnectors:Sources:0:Tables:1"]="payments",
    ["DataConnectors:Sources:0:AllowedAddresses:0"]="127.0.0.1"
}).Build();
await DatabaseInitializer.InitializeAsync(db,cfg);
db.Users.Add(new User {UserName="Synthetic",UserEmail="reader@example.test",PasswordHash="synthetic",IsActive=true});await db.SaveChangesAsync();
var role=await db.Roles.SingleAsync();db.UserRoles.Add(new UserRole {UserId=1,RoleId=role.RoleId});await db.SaveChangesAsync();
var http=new HttpContextAccessor { HttpContext=new DefaultHttpContext {User=new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,"1"),new Claim("perm",PortalAccess.DataQuery)],"test"))}};
http.HttpContext.Response.Body=new MemoryStream();
var queries=new QueryDouble();
var connectors=new DataConnectorService(db,new ConnectorProfiles(cfg),queries,new TrinoMetadataService(queries),http);
var id=await connectors.Save(null,new ConnectorUpdate("Enterprise fixture","enterprise_test",true,["invoices","payments"],[role.RoleId],0),default);
using var services=new ServiceCollection().BuildServiceProvider();
var sse=new HttpSseEventEmitter(http.HttpContext.Response);
async Task<(string,ChatGrounding)> Ask(int? source,DateOnly? date) {
    var grounding=new ChatGrounding();
    var service=new BusinessChatContextService(services,cfg,connectors,grounding,http);
    var prompt=await service.AskAsync(new AskSmartRequest {Pergunta="Mostre o aging",SourceId=source,ReferenceDate=date},sse);
    return (prompt,grounding);
}
var reference=new DateOnly(2026,11,1);
var (prompt,g)=await Ask(null,null);Check(g.DirectAnswer is not null && queries.Calls==0,"Missing source called query");
(prompt,g)=await Ask(id,reference);Check(g.DirectAnswer?.Contains("AllowExternalLlm")==true && prompt.Length==0 && g.DirectAnswer.Contains("1234500"),"External disclosure gate bypassed");
cfg["DataConnectors:Sources:0:AllowExternalLlm"]="true";
(prompt,g)=await Ask(id,reference);Check(g.DirectAnswer is null && prompt.Contains("1234500") && g.Appendix.Contains("atlas-chart") && g.Appendix.Contains("2026-11-01"),"Authorized grounding/provenance missing");
queries.Fail=true;(prompt,g)=await Ask(id,reference);Check(g.DirectAnswer?.Contains("Nenhum indicador")==true && !g.DirectAnswer.Contains("1234500"),"Unavailable source invented results");
queries.Fail=false;var before=queries.Calls;(prompt,g)=await Ask(999,reference);Check(g.DirectAnswer is not null && queries.Calls==before,"Unauthorized source executed");
Console.WriteLine("PASS: document/analytics/hybrid/clarification intent, no source no query, explicit external LLM gate, grounded result/chart/provenance, unavailable/unauthorized sources never invent metrics. SQLite temporary and Trino query double.");

sealed class QueryDouble : ITrinoQueryService
{
    public int Calls;public bool Fail;
    public Task<List<Dictionary<string,object>>> QueryAsync(string sql,string schema="financeiro",CancellationToken ct=default) => throw new Exception("Free SQL path used");
    public Task<List<Dictionary<string,object>>> QueryApprovedAsync(ConnectorProfile profile,ApprovedTrinoQuery query,int organizationId,CancellationToken ct=default) {
        Calls++;if(Fail) throw new ChatException(502,"Fonte indisponivel.");
        return Task.FromResult(new List<Dictionary<string,object>> { new() { ["faixa"]="31-60",["saldo_centavos"]=1234500L } });
    }
}
