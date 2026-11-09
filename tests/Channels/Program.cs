using AiGateway.Channels;
using AiGateway.Infrastructure.Qdrant.Services.Chats;
using AiGateway.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Portal.AI.Gateway.Infrastructure;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var root=Path.Combine(Path.GetTempPath(),"portal-channels-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"SQLITE_BACKUP_DIRECTORY",Path.Combine(root,"backups")}}).Build();
DbContextOptions<PortalDbContext> Options(string path)=>new DbContextOptionsBuilder<PortalDbContext>().UseSqlite($"Data Source={path};Foreign Keys=True;Pooling=False").Options;
void Check(bool ok,string reason){if(!ok)throw new Exception(reason);}
async Task Denied(Func<Task> action,int status){try{await action();throw new Exception("Expected denial");}catch(ChatException e){Check(e.Status==status,"Unexpected status "+e.Status);}}
PortalDbContext Scoped(string path,int id){var scope=new OrganizationScope();typeof(OrganizationScope).GetProperty("Id")!.SetValue(scope,id);return new(Options(path),scope);}
var clock=new Clock();var sender=new RetrySender();
ChannelService Service(PortalDbContext db)=>new(db,new LocalExtractiveChannelAgent(new LocalKnowledgeRetriever(db)),clock,sender,NullLogger<ChannelService>.Instance,config);
async Task Fixture(string path,bool collision=false){
 await using var db=new V8Context(Options(path));await db.Database.EnsureCreatedAsync();
 db.Organizations.AddRange(new(){Id=1,Name="Synthetic A",Slug="default"},new(){Id=2,Name="Synthetic B",Slug="second"});
 db.KnowledgeBaseItems.AddRange(new(){Id=1,OrganizationId=1,KeyName="support",Title="Garantia",Section="Synthetic",Content="garantia sintética autorizada",IsActive=true},
 new(){Id=2,OrganizationId=2,KeyName="secret",Title="Garantia",Section="Synthetic",Content="FOREIGN_SECRET_MUST_NOT_APPEAR",IsActive=true});await db.SaveChangesAsync();
 await db.Database.ExecuteSqlRawAsync("CREATE TABLE SchemaVersions(Version INTEGER PRIMARY KEY,Checksum TEXT NOT NULL,AppliedAt TEXT NOT NULL)");
 var names=new[]{"001_ChatSessions","002_TitleUsage","003_ChatUsage","004_AiProviders","005_Organizations","006_DataConnectors","007_OrganizationWorkspace","008_OrganizationInvitations"};
 for(var i=0;i<names.Length;i++){using var r=typeof(ChatSchemaMigrator).Assembly.GetManifestResourceStream($"AiGateway.Infrastructure.EF.Migrations.{names[i]}.sql")!;var sql=(await new StreamReader(r).ReadToEndAsync()).Replace("\r\n","\n");var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SchemaVersions VALUES({i+1},{hash},{clock.GetUtcNow().UtcDateTime})");}
 if(collision)await db.Database.ExecuteSqlRawAsync("CREATE TABLE Collision(Id INTEGER);CREATE INDEX IX_ChannelMessages_ConnectionId_ExternalId ON Collision(Id)");
}
try{
 var path=Path.Combine(root,"test.db");await Fixture(path);
 await using(var db=new PortalDbContext(Options(path))){await ChatSchemaMigrator.InitializeAsync(db,config);await ChatSchemaMigrator.InitializeAsync(db,config);Check(await db.KnowledgeBaseItems.CountAsync()==2,"Migration preserved data");}
 var backup=Directory.GetFiles(Path.Combine(root,"backups"),"*.sqlite3").Single();var restored=Path.Combine(root,"restore.db");File.Copy(backup,restored);
 await using(var db=new PortalDbContext(Options(restored)))await ChatSchemaMigrator.InitializeAsync(db,config);
 var fail=Path.Combine(root,"fail.db");await Fixture(fail,true);
 await using(var db=new PortalDbContext(Options(fail))){try{await ChatSchemaMigrator.InitializeAsync(db,config);throw new Exception("Expected rollback");}catch(Microsoft.Data.Sqlite.SqliteException){}
 var conn=db.Database.GetDbConnection();using var c=conn.CreateCommand();c.CommandText="SELECT count(*) FROM sqlite_master WHERE name='ChannelConnections'";Check(Convert.ToInt64(await c.ExecuteScalarAsync())==0,"Late-DDL rollback");}
 await using(var db=Scoped(path,1)){
  var s=Service(db);await Denied(()=>s.Create(new("Unauthorized","simulator","a","bad",[2]),default),400);
  await s.Create(new("Synthetic one","simulator","a","one",[1]),default);await s.Create(new("Synthetic two","simulator","a","two",[1]),default);
  var connections=await db.ChannelConnections.OrderBy(c=>c.ExternalPhoneNumberId).ToArrayAsync();var a=connections[0];var b=connections[1];Check(!a.AutoReply,"Auto reply default off");
  await s.Receive(a.Id,new("off","same-contact","garantia",clock.GetUtcNow().UtcDateTime),default);await s.Process(default);Check(!await db.ChannelMessages.AnyAsync(m=>m.Direction=="outbound"),"Default off");
  await s.Update(a.Id,new(a.Version,true,true,true,30,5),default);
  await s.Update(b.Id,new(b.Version,true,true,true,30,5),default);
  var input=new InboundChannelMessage("incoming","same-contact","garantia",clock.GetUtcNow().UtcDateTime);
  Check(await s.Receive(a.Id,input,default),"First inbound");Check(!await s.Receive(a.Id,input,default),"Deduplication");await s.Receive(b.Id,input,default);
  await s.Process(default);await s.Process(default);Check(await db.ChannelContacts.CountAsync()==2,"Number-scoped contacts");
  var output=await db.ChannelMessages.FirstAsync(m=>m.Direction=="outbound");Check(output.State=="simulated"&&output.Text.Contains("autorizada")&&!output.Text.Contains("FOREIGN_SECRET"),"Grounding and delivery");
  Check(output.SourcesJson.Contains("Garantia"),"Persisted references");Check(await db.MessageDeliveryEvents.CountAsync()==2,"No duplicate deliveries");
  var conv=await db.ChannelConversations.FirstAsync(c=>c.ConnectionId==a.Id);await s.Handoff(conv.Id,new(conv.Version,true),default);
  await s.Receive(a.Id,new("human","same-contact","garantia",clock.GetUtcNow().UtcDateTime),default);await s.Process(default);Check((await db.ChannelMessages.SingleAsync(m=>m.ExternalId=="human")).State=="held","Human handoff");
  await Denied(()=>s.Handoff(conv.Id,new(-1,false),default),409);await s.Handoff(conv.Id,new(conv.Version,false),default);
  await s.Update(a.Id,new(a.Version,true,true,false,30,5),default);await s.Receive(a.Id,new("agent-off","same-contact","garantia",clock.GetUtcNow().UtcDateTime),default);await s.Process(default);Check((await db.ChannelMessages.SingleAsync(m=>m.ExternalId=="agent-off")).State=="held","Disabled agent");
  await s.Update(a.Id,new(a.Version,true,true,true,30,1),default);await s.Receive(a.Id,new("quota","same-contact","garantia",clock.GetUtcNow().UtcDateTime),default);await s.Process(default);Check((await db.ChannelMessages.SingleAsync(m=>m.ExternalId=="quota")).State=="limit","Quota");
  await s.Update(a.Id,new(a.Version,true,true,true,30,10),default);
  config["Channels:OrganizationLimits:1"]="0";
  await s.Receive(a.Id,new("org-quota","same-contact","garantia",clock.GetUtcNow().UtcDateTime),default);await s.Process(default);
  Check((await db.ChannelMessages.SingleAsync(m=>m.ExternalId=="org-quota")).State=="limit","Organization-wide quota");
  config["Channels:OrganizationLimits:1"]="1000";clock.Advance(TimeSpan.FromHours(25));
  await s.Receive(a.Id,new("expired","same-contact","garantia",clock.GetUtcNow().UtcDateTime.AddHours(-25)),default);await s.Process(default);Check((await db.ChannelMessages.SingleAsync(m=>m.ExternalId=="expired")).State=="held","24-hour window");
  sender.Fail=true;await s.Receive(a.Id,new("retry","same-contact","garantia",clock.GetUtcNow().UtcDateTime),default);await s.Process(default);
  var queued=await db.ChannelMessages.SingleAsync(m=>m.Direction=="outbound"&&m.State=="queued");Check(queued.Attempts==1,"Transient failure backoff");await s.Process(default);Check(queued.Attempts==1,"No early retry");
  clock.Advance(TimeSpan.FromSeconds(31));sender.Fail=false;
  await using(var restarted=Scoped(path,1))await Service(restarted).Process(default);
  await db.Entry(queued).ReloadAsync();Check(queued.State=="simulated"&&queued.Attempts==2,"Retry after context restart");
  await s.Receive(a.Id,new("dead-letter","same-contact","garantia",clock.GetUtcNow().UtcDateTime),default);sender.Fail=true;
  for(var i=0;i<3;i++){await s.Process(default);clock.Advance(TimeSpan.FromMinutes(3));}Check(await db.ChannelMessages.AnyAsync(m=>m.State=="failed"&&m.Attempts==3),"Bounded retries");sender.Fail=false;
  sender.Fail=true;await s.Receive(a.Id,new("queued-handoff","same-contact","garantia",clock.GetUtcNow().UtcDateTime),default);await s.Process(default);
  var paused=await db.ChannelMessages.SingleAsync(m=>m.State=="queued");await s.Handoff(conv.Id,new(conv.Version,true),default);
  sender.Fail=false;clock.Advance(TimeSpan.FromMinutes(1));await s.Process(default);Check(paused.State=="held","Handoff checked again before queued delivery");
  await s.Update(a.Id,new(a.Version,false,true,true,30,10),default);await Denied(()=>s.Receive(a.Id,new("disabled-channel","same-contact","garantia",clock.GetUtcNow().UtcDateTime),default),409);
  clock.Advance(TimeSpan.FromDays(31));await s.Process(default);Check(!await db.ChannelMessages.AnyAsync(m=>m.Text!=""),"Content retention");
  await s.Update(a.Id,new(a.Version,true,true,true,30,10),default);
  Check(!await s.Receive(a.Id,input,default),"Tombstone dedup after retention");
 }
 await using(var other=Scoped(path,2)){
  Check(await other.ChannelConnections.CountAsync()==0,"Tenant query filter");
  await using var system=new PortalDbContext(Options(path));var foreign=await system.ChannelConnections.FirstAsync();
  await Denied(()=>Service(other).Connection(foreign.Id,default),404);
  await Service(other).Create(new("Other tenant","simulator","b","one",[2]),default);Check(await other.ChannelConnections.CountAsync()==1,"Second tenant independent");
  var own=await other.ChannelConnections.SingleAsync();await Service(other).Update(own.Id,new(0,true,true,true,30,10),default);
  await Service(other).Receive(own.Id,new("incoming","same-contact","garantia",clock.GetUtcNow().UtcDateTime),default);
  await Service(other).Process(default);Check((await other.ChannelMessages.SingleAsync(m=>m.Direction=="outbound")).Text.Contains("FOREIGN_SECRET"),"Independent authorized second-tenant evidence");
  other.ChannelContacts.Add(new(){Id=Guid.NewGuid(),OrganizationId=1,ConnectionId=foreign.Id,ExternalId="cross-write"});await Denied(()=>other.SaveChangesAsync(),403);
 }
 config["BrowserBridge:Enabled"]="true";
 Guid browserId,manualId,conversationId;
 await using(var db=Scoped(path,1)){
  var s=Service(db);await s.Create(new("Browser test","whatsapp-browser","","",[1]),default);
  var c=await db.ChannelConnections.SingleAsync(x=>x.Provider=="whatsapp-browser");browserId=c.Id;
  Check(c.ExternalAccountId=="org:1"&&c.ExternalPhoneNumberId==c.Id.ToString("N")&&!c.AutoReply,"Browser server IDs/default off");
  await s.Receive(c.Id,new("browser-in","5511999999999@c.us","garantia",clock.GetUtcNow().UtcDateTime),default);
  await s.Process(default);var conv=await db.ChannelConversations.SingleAsync(x=>x.ConnectionId==c.Id);conversationId=conv.Id;
  await Denied(()=>s.ManualReply(conv.Id,new("manual",Guid.NewGuid()),default),409);
  await s.Handoff(conv.Id,new(conv.Version,true),default);manualId=Guid.NewGuid();
  await s.ManualReply(conv.Id,new("manual",manualId),default);await s.ManualReply(conv.Id,new("manual",manualId),default);
  Check(await db.ChannelMessages.CountAsync(x=>x.Id==manualId)==1,"Idempotent manual outbox");
  await Denied(()=>s.ManualReply(conv.Id,new("altered",manualId),default),409);
  await s.Update(c.Id,new(c.Version,true,true,true,30,100),default);await s.Process(default);
  Check((await db.ChannelMessages.SingleAsync(x=>x.Id==manualId)).State=="queued","Browser not sent by simulator");
  await s.Handoff(conv.Id,new(conv.Version,false),default);
  await s.Receive(c.Id,new("browser-auto","5511999999999@c.us","garantia",clock.GetUtcNow().UtcDateTime),default);await s.Process(default);
  Check(await db.ChannelMessages.AnyAsync(x=>x.ConnectionId==c.Id&&x.Category!="manual"&&x.Direction=="outbound"&&x.State=="queued"),"Browser automatic durable outbox");
 }
 await using(var db=Scoped(path,2)){
  await Denied(()=>Service(db).Connection(browserId,default),404);
  await Denied(()=>Service(db).ManualReply(conversationId,new("foreign",Guid.NewGuid()),default),404);
 }
 await using(var inspect=new PortalDbContext(Options(path))){var target=await inspect.ChannelConnections.FirstAsync(c=>c.OrganizationId==2);var concurrent=new InboundChannelMessage("concurrent","same-contact","garantia",clock.GetUtcNow().UtcDateTime);
  var results=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>Task.Run(async()=>{await using var db=Scoped(path,2);return await Service(db).Receive(target.Id,concurrent,default);})));Check(results.Count(x=>x)==1,"Concurrent inbox deduplication");}
 var raw=Encoding.UTF8.GetBytes("{\"synthetic\":true}");var secret="synthetic-app-secret";var signature="sha256="+Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret),raw));
 Check(WhatsAppSignature.Verify(raw,signature,secret),"Valid signature");Check(!WhatsAppSignature.Verify(raw,signature,"wrong")&&!WhatsAppSignature.Verify(raw,"sha256=bad",secret)&&!WhatsAppSignature.Verify(raw,signature,null),"Invalid signature");
 config["WhatsApp:AppId"]="55555";config["WhatsApp:AppSecret"]="synthetic-signing-secret";config["WhatsApp:VerifyToken"]="synthetic-verification-secret";config["WhatsApp:AccessToken"]="synthetic-access-secret";
 config["WhatsApp:PublicBaseUrl"]="https://webhook.example.test/";config["WhatsApp:Routes:12345:AccountId"]="98765";config["WhatsApp:Routes:12345:OrganizationId"]="1";
 var setup=WhatsAppSetup.Read(config,1);Check(setup.ReceiveConfigurationReady&&!setup.LiveSendingAvailable&&setup.AuthorizedNumbers.Count==1,"Explicit receive-only metadata");
 var serialized=JsonSerializer.Serialize(setup);Check(!serialized.Contains("synthetic-signing-secret")&&!serialized.Contains("synthetic-verification-secret")&&!serialized.Contains("synthetic-access-secret"),"No credential response");
 Check(WhatsAppSetup.Read(config,2).AuthorizedNumbers.Count==0&&!WhatsAppSetup.Read(config,2).ReceiveConfigurationReady,"No cross-tenant route discovery");
 foreach(var invalid in new[]{"http://webhook.example.test/","https://localhost/","https://webhook.example.test/?token=hidden","https://user:pass@webhook.example.test/","https://webhook.example.test/private"}){config["WhatsApp:PublicBaseUrl"]=invalid;Check(WhatsAppSetup.Read(config,1).WebhookUrl is null,"Unsafe callback metadata rejected");}
 Console.WriteLine("PASS: v8->v9 migration/idempotency/backup/restore/late-DDL rollback; tenant read/write isolation, authorized retrieval, duplicate/number-scoped contacts, opt-in, handoff, disabled agent, 24h, quota, bounded backoff/tombstones/signatures; credential-free scoped setup metadata. No external calls.");
}finally{Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();Directory.Delete(root,true);}
sealed class V8Context(DbContextOptions<PortalDbContext> options):PortalDbContext(options){protected override void OnModelCreating(ModelBuilder m){base.OnModelCreating(m);m.Ignore<MessageDeliveryEvent>();m.Ignore<ChannelMessage>();m.Ignore<ChannelConversation>();m.Ignore<ChannelContact>();m.Ignore<ChannelConnection>();}}
sealed class Clock:TimeProvider{DateTimeOffset now=new(2026,11,8,12,0,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>now;public void Advance(TimeSpan value)=>now+=value;}
sealed class RetrySender:IOutboundMessageSender{public bool Fail;public string Name=>"simulator";public Task<ChannelSendResult> Send(ChannelConnection c,ChannelMessage m,CancellationToken ct)=>Fail?throw new ChannelTransientException():Task.FromResult(new ChannelSendResult("simulated:"+m.Id.ToString("N")));}
