using AiGateway.Controllers.Audit;
using AiGateway.Infrastructure.Audit;
using AiGateway.Infrastructure.Sse;
using AiGateway.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Portal.AI.Gateway.Infrastructure;
using System.Security.Claims;
using System.Text.Json;

static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
static OrganizationScope Scope(int org,int user=1){var s=new OrganizationScope();typeof(OrganizationScope).GetProperty("Id")!.SetValue(s,org);typeof(OrganizationScope).GetProperty("UserId")!.SetValue(s,user);return s;}
var folder=Path.Combine(Path.GetTempPath(),"portal-audit-"+Guid.NewGuid());Directory.CreateDirectory(folder);
var path=Path.Combine(folder,"portal.sqlite3");
var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"SQLITE_BACKUP_DIRECTORY",Path.Combine(folder,"backups")}}).Build();
DbContextOptions<PortalDbContext> Options(string file)=>new DbContextOptionsBuilder<PortalDbContext>().UseSqlite($"Data Source={file};Foreign Keys=True").Options;
var clock=TimeProvider.System;
try {
    await using(var db=new PortalDbContext(Options(path))) {
        await ChatSchemaMigrator.InitializeAsync(db,config);
        db.Users.Add(new(){UserName="Preserve",UserEmail="preserve@example.test",PasswordHash="unchanged-synthetic",IsActive=true});await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("DROP TABLE ActionAuditEvents; DELETE FROM SchemaVersions WHERE Version=11");
        await ChatSchemaMigrator.InitializeAsync(db,config);await ChatSchemaMigrator.InitializeAsync(db,config);
        Check((await db.Users.SingleAsync()).PasswordHash=="unchanged-synthetic","Migration changed credentials");
    }
    var backup=Directory.GetFiles(Path.Combine(folder,"backups")).Single();var restored=Path.Combine(folder,"restored.sqlite3");File.Copy(backup,restored);
    await using(var db=new PortalDbContext(Options(restored))) {
        await db.Database.OpenConnectionAsync();using var c=db.Database.GetDbConnection().CreateCommand();
        c.CommandText="PRAGMA integrity_check";Check((string?)await c.ExecuteScalarAsync()=="ok","Backup integrity");
        c.CommandText="SELECT count(*) FROM sqlite_master WHERE name='ActionAuditEvents'";Check(Convert.ToInt32(await c.ExecuteScalarAsync())==0,"Backup taken after change");
        await ChatSchemaMigrator.InitializeAsync(db,config);Check(await db.Users.CountAsync()==1,"Restore lost account");
    }
    var broken=Path.Combine(folder,"broken.sqlite3");File.Copy(backup,broken);
    await using(var db=new PortalDbContext(Options(broken))) {
        await db.Database.ExecuteSqlRawAsync("CREATE TABLE Collision(Id INTEGER); CREATE INDEX IX_ActionAuditEvents_CreatedAt ON Collision(Id)");
        try{await ChatSchemaMigrator.InitializeAsync(db,config);throw new Exception("DDL collision accepted");}catch(SqliteException){}
        await db.Database.OpenConnectionAsync();using var c=db.Database.GetDbConnection().CreateCommand();
        c.CommandText="SELECT count(*) FROM sqlite_master WHERE name='ActionAuditEvents'";Check(Convert.ToInt32(await c.ExecuteScalarAsync())==0,"Audit DDL not rolled back");
        c.CommandText="SELECT count(*) FROM SchemaVersions WHERE Version=11";Check(Convert.ToInt32(await c.ExecuteScalarAsync())==0,"Failed version recorded");
    }
    var services=new ServiceCollection().AddLogging().AddScoped<OrganizationScope>().AddDbContext<PortalDbContext>(o=>o.UseSqlite($"Data Source={path}"));
    services.AddSingleton<ActionAuditWriter>();await using var provider=services.BuildServiceProvider();var writer=provider.GetRequiredService<ActionAuditWriter>();
    async Task Record(string controller,string action,string method,int status,int? org=null,bool login=false,bool streamedFailure=false,bool unhandled=false) {
        var scope=org.HasValue ? Scope(org.Value) : Scope(0,0);
        var context=new DefaultHttpContext();context.Request.Path="/api/SECRET_RAW_PATH";context.Request.QueryString=new("?token=SECRET_QUERY");
        context.Request.Headers.Authorization="Bearer SECRET_HEADER";context.Request.Method=method;context.Response.Body=new MemoryStream();
        context.Request.Body=new MemoryStream(System.Text.Encoding.UTF8.GetBytes("SECRET_BODY_PASSWORD"));
        var descriptor=new ControllerActionDescriptor{ControllerName=controller,ActionName=action};
        context.SetEndpoint(new RouteEndpoint(_=>Task.CompletedTask,RoutePatternFactory.Parse("api/test/{id}"),0,new EndpointMetadataCollection(descriptor),"test"));
        context.Request.RouteValues["id"]="42";
        if(org.HasValue)context.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("name","Synthetic Operator"),new Claim(ClaimTypes.NameIdentifier,"1")},"Bearer"));
        var middleware=new ActionAuditMiddleware(async c=>{
            if(login)ActionAuditContext.Login(c,2,2,"Synthetic Login");
            c.Response.StatusCode=status;
            if(streamedFailure)await new HttpSseEventEmitter(c.Response).EmitAsync("error","SECRET_STREAM_CONTENT");
            if(unhandled)throw new Exception("SECRET_EXCEPTION");
        });
        try{await middleware.InvokeAsync(context,writer,clock,scope);}catch(Exception) when(unhandled){}
    }
    await Record("Auth","Login","POST",200,login:true);
    await Record("Auth","Login","POST",401);
    await Record("KnowledgeBase","Save","POST",201,2);
    await Record("Roles","Delete","DELETE",403,3);
    await Record("SmartAsk","AskStream","POST",200,2,streamedFailure:true);
    await Record("Test","Boom","POST",200,2,unhandled:true);
    await using(var db=new PortalDbContext(Options(path))) {
        var entries=await db.ActionAuditEvents.OrderBy(x=>x.Id).ToArrayAsync();Check(entries.Length==6,"Missing audit entries");
        Check(entries[0].ActorUserId==2 && entries[0].OrganizationId==2,"Login identity missing");
        Check(entries[1].ActorUserId is null && entries[1].Outcome=="failure","Failed login attributed incorrectly");
        Check(entries[2].ActorUserId==1 && entries[2].TargetId=="id:42","Authenticated actor/resource missing");
        Check(entries[3].Outcome=="denied" && entries[3].StatusCode==403,"Denial lost");
        Check(entries[4].Outcome=="failure" && entries[4].StatusCode==200,"Stream failure reported as success");
        Check(entries[5].Outcome=="failure" && entries[5].StatusCode==500,"Unhandled exception reported as success");
        Check(!JsonSerializer.Serialize(entries).Contains("SECRET_"),"Sensitive request/response/exception material recorded");
        try{await db.Database.ExecuteSqlRawAsync("UPDATE ActionAuditEvents SET Outcome='success'");throw new Exception("Audit update allowed");}catch(SqliteException){}
        try{await db.Database.ExecuteSqlRawAsync("DELETE FROM ActionAuditEvents");throw new Exception("Audit delete allowed");}catch(SqliteException){}
    }
    async Task<JsonElement> Search(int org,AuditSearch? search=null) {
        await using var db=new PortalDbContext(Options(path),Scope(org));
        var response=await new ActionAuditController(db,clock).Search(search ?? new(),default);
        Check(response is OkObjectResult,"Search rejected");return JsonSerializer.SerializeToElement(((OkObjectResult)response).Value);
    }
    var all=await Search(1);Check(all.GetProperty("items").GetArrayLength()==6,"Installation scope incomplete");
    var tenant=await Search(2);Check(tenant.GetProperty("items").GetArrayLength()==4 && tenant.GetProperty("scope").GetString()=="organization","Tenant scope leak");
    var filtered=await Search(1,new(Q:"LOGIN"));Check(filtered.GetProperty("items").GetArrayLength()==2,"Search case handling");
    var page=await Search(1,new(Limit:2));var cursor=page.GetProperty("nextBeforeId").GetInt64();
    var next=await Search(1,new(BeforeId:cursor,Limit:2));Check(next.GetProperty("items")[0].GetProperty("Id").GetInt64()<cursor,"Cursor overlap");
    await using(var db=new PortalDbContext(Options(path),Scope(1)))Check(await new ActionAuditController(db,clock).Search(new(Limit:101),default) is BadRequestObjectResult,"Unbounded query allowed");
    Console.WriteLine("PASS: v10->v11 migration/preservation/idempotency/pre-change backup/restore/late-DDL rollback; append-only triggers; independent writer; login success/failure, actor/tenant, denied/streamed/unhandled outcomes; secrets redacted; tenant/global scope, filters, bounds and cursor.");
} finally {SqliteConnection.ClearAllPools();Directory.Delete(folder,true);}
