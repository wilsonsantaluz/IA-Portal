using AiGateway.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Portal.AI.Gateway.Infrastructure;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var root=Path.Combine(Path.GetTempPath(),"portal-invitations-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"SQLITE_BACKUP_DIRECTORY",Path.Combine(root,"backups")}}).Build();
DbContextOptions<PortalDbContext> Options(string path)=>new DbContextOptionsBuilder<PortalDbContext>().UseSqlite($"Data Source={path};Foreign Keys=True").Options;
void Check(bool ok,string message){if(!ok)throw new Exception(message);}
async Task Denied(Func<Task> action,int status){try{await action();throw new Exception("Expected denial");}catch(ChatException e){Check(e.Status==status,"Unexpected rejection status");}}
async Task Fixture(string path,bool fail=false){
    await using var db=new V7Context(Options(path));await db.Database.EnsureCreatedAsync();
    db.Organizations.AddRange(new(){Id=1,Name="Synthetic A",Slug="default"},new(){Id=2,Name="Synthetic B",Slug="other"});
    for(var id=1;id<=4;id++)db.Users.Add(new(){UserId=id,UserName=$"Synthetic {id}",UserEmail=$"user{id}@example.test",PasswordHash="unchanged",IsActive=true});
    await db.SaveChangesAsync();
    db.OrganizationMemberships.AddRange(new(){OrganizationId=1,UserId=1,PolicyRole="Owner"},new(){OrganizationId=1,UserId=3,PolicyRole="Admin"},new(){OrganizationId=2,UserId=2,PolicyRole="Member"},new(){OrganizationId=2,UserId=4,PolicyRole="Owner"});
    db.KnowledgeBaseItems.Add(new(){OrganizationId=1,KeyName="preserve",Section="Synthetic",Content="Human data survives",IsActive=true});
    await db.SaveChangesAsync();
    await db.Database.ExecuteSqlRawAsync("CREATE TABLE SchemaVersions(Version INTEGER PRIMARY KEY,Checksum TEXT NOT NULL,AppliedAt TEXT NOT NULL)");
    var names=new[]{"001_ChatSessions","002_TitleUsage","003_ChatUsage","004_AiProviders","005_Organizations","006_DataConnectors","007_OrganizationWorkspace"};
    for(var i=0;i<names.Length;i++){
        using var r=typeof(ChatSchemaMigrator).Assembly.GetManifestResourceStream($"AiGateway.Infrastructure.EF.Migrations.{names[i]}.sql")!;
        var text=(await new StreamReader(r).ReadToEndAsync()).Replace("\r\n","\n");var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SchemaVersions VALUES({i+1},{hash},{DateTime.UtcNow})");
    }
    if(fail)await db.Database.ExecuteSqlRawAsync("CREATE TABLE Collision(Id INTEGER); CREATE UNIQUE INDEX IX_OrganizationInvitations_TokenHash ON Collision(Id)");
}
var path=Path.Combine(root,"v7.sqlite3");var clock=new TestClock();
PortalDbContext Scoped(int org,int actor){var scope=new OrganizationScope();typeof(OrganizationScope).GetProperty("Id")!.SetValue(scope,org);typeof(OrganizationScope).GetProperty("UserId")!.SetValue(scope,actor);return new(Options(path),scope);}
OrganizationInvitationService Invitations(PortalDbContext db)=>new(db,clock,NullLogger<OrganizationInvitationService>.Instance);
try{
    await Fixture(path);
    await using(var db=new PortalDbContext(Options(path))){
        await ChatSchemaMigrator.InitializeAsync(db,config);await ChatSchemaMigrator.InitializeAsync(db,config);
        Check(await db.OrganizationInvitations.CountAsync()==0 && (await db.KnowledgeBaseItems.SingleAsync()).Content=="Human data survives" && await db.OrganizationMemberships.CountAsync()==4,"Migration preservation/idempotency");
    }
    var backup=Directory.GetFiles(Path.Combine(root,"backups")).Single();var restored=Path.Combine(root,"restored.sqlite3");
    using(var a=new SqliteConnection($"Data Source={backup}"))using(var b=new SqliteConnection($"Data Source={restored}")){
        a.Open();b.Open();a.BackupDatabase(b);using var c=b.CreateCommand();c.CommandText="SELECT count(*) FROM sqlite_master WHERE name='OrganizationInvitations'";Check(Convert.ToInt64(c.ExecuteScalar())==0,"Pre-upgrade backup");
    }
    await using(var db=new PortalDbContext(Options(restored))){await ChatSchemaMigrator.InitializeAsync(db,config);Check(await db.OrganizationMemberships.CountAsync()==4,"Restored upgrade");}
    var broken=Path.Combine(root,"broken.sqlite3");await Fixture(broken,true);
    await using(var db=new PortalDbContext(Options(broken))){
        try{await ChatSchemaMigrator.InitializeAsync(db,config);throw new Exception("DDL failure ignored");}catch(SqliteException){}
        using var c=db.Database.GetDbConnection().CreateCommand();c.CommandText="SELECT count(*) FROM sqlite_master WHERE name='OrganizationInvitations'";Check(Convert.ToInt64(await c.ExecuteScalarAsync())==0,"DDL rollback");
        c.CommandText="SELECT count(*) FROM SchemaVersions WHERE Version=8";Check(Convert.ToInt64(await c.ExecuteScalarAsync())==0,"Failed ledger rollback");
    }
    InvitationCreated created;
    await using(var db=Scoped(1,1)){
        var service=Invitations(db);created=await service.Create(1,"Member",default);
        Check(created.Token.Length==64 && created.ExpiresAt==clock.GetUtcNow().UtcDateTime.AddHours(24),"Random bounded lifetime");
        var stored=await db.OrganizationInvitations.SingleAsync();Check(stored.TokenHash.Length==64 && stored.TokenHash!=created.Token,"Only hash stored");
        var json=JsonSerializer.Serialize(await service.List(1,default));Check(!json.Contains(created.Token) && !json.Contains("TokenHash"),"No code/hash in list");
        await Denied(()=>service.Create(3,"Member",default),403);await Denied(()=>service.Create(1,"Owner",default),400);
        await Denied(()=>service.Preview(1,"bad-code",default),404);
    }
    await using(var db=Scoped(2,2)){
        var service=Invitations(db);await service.Preview(2,created.Token,default);
        Check(!await db.OrganizationMemberships.AnyAsync(m=>m.OrganizationId==1 && m.UserId==2),"Preview grants nothing");
        await service.Accept(2,created.Token,default);
        Check((await db.OrganizationMemberships.SingleAsync(m=>m.OrganizationId==1 && m.UserId==2)).PolicyRole=="Member","Capability-derived membership");
        Check(await db.UserRoles.CountAsync()==0 && await db.KnowledgeBaseItems.CountAsync()==0,"No groups granted or foreign KB exposed");
        await Denied(()=>service.Accept(2,created.Token,default),404);
        db.OrganizationInvitations.Add(new(){Id=Guid.NewGuid(),OrganizationId=1,TokenHash=new string('0',64),CreatedByUserId=2,CreatedAt=clock.GetUtcNow().UtcDateTime,ExpiresAt=clock.GetUtcNow().UtcDateTime.AddHours(1)});
        await Denied(()=>db.SaveChangesAsync(),403);db.ChangeTracker.Clear();
    }
    await using(var db=Scoped(1,1)){
        var service=Invitations(db);var keep=await service.Create(1,"Viewer",default);
        await service.Accept(1,keep.Token,default);
        Check((await db.OrganizationMemberships.SingleAsync(m=>m.OrganizationId==1 && m.UserId==1)).PolicyRole=="Owner","Existing role never downgraded");
        var revoked=await service.Create(1,"Viewer",default);await service.Revoke(1,revoked.Id,0,default);
        await Denied(()=>service.Preview(1,revoked.Token,default),404);
        await Denied(()=>service.Revoke(1,revoked.Id,0,default),409);
        await using(var other=Scoped(2,4))await Denied(()=>Invitations(other).Revoke(4,revoked.Id,1,default),404);
        var inactive=await service.Create(1,"Viewer",default);
        await db.Database.ExecuteSqlRawAsync("UPDATE OrganizationMemberships SET IsActive=0 WHERE OrganizationId=1 AND UserId=2");
        await Denied(()=>service.Accept(2,inactive.Token,default),404);
        await db.Database.ExecuteSqlRawAsync("UPDATE OrganizationMemberships SET IsActive=1 WHERE OrganizationId=1 AND UserId=2");
    }
    InvitationCreated race;
    await using(var db=Scoped(1,1))race=await Invitations(db).Create(1,"Viewer",default);
    async Task<int> Race(int actor){await using var db=Scoped(2,actor);try{await Invitations(db).Accept(actor,race.Token,default);return 200;}catch(ChatException e){return e.Status;}}
    var results=await Task.WhenAll(Task.Run(()=>Race(2)),Task.Run(()=>Race(4)));
    Check(results.Count(r=>r==200)==1 && results.Count(r=>r==404)==1,"Concurrent capability consumed once");
    await using(var db=Scoped(1,1)){
        var service=Invitations(db);var pending=await service.Create(1,"Member",default);
        var workspace=new OrganizationWorkspaceService(db,new AiProviderCatalog(db,config),NullLogger<OrganizationWorkspaceService>.Instance);
        await Denied(()=>workspace.TransferOwnership(1,new(1,0,0),default),400);
        await Denied(()=>workspace.TransferOwnership(1,new(2,0,99),default),409);
        await Denied(()=>workspace.ChangeMember(1,2,new("Owner",true,0),default),400);
        await workspace.TransferOwnership(1,new(2,0,0),default);
        Check((await db.OrganizationMemberships.SingleAsync(m=>m.OrganizationId==1 && m.UserId==1)).PolicyRole=="Admin" && (await db.OrganizationMemberships.SingleAsync(m=>m.OrganizationId==1 && m.UserId==2)).PolicyRole=="Owner","Atomic transfer");
        await Denied(()=>service.Preview(1,pending.Token,default),404);
        await Denied(()=>service.Create(1,"Viewer",default),403);
        // Re-promotion does not revive old capabilities: issuer version is bound.
        await db.Database.ExecuteSqlRawAsync("UPDATE OrganizationMemberships SET PolicyRole='Owner',Version=Version+1 WHERE OrganizationId=1 AND UserId=1");
        db.ChangeTracker.Clear();await Denied(()=>service.Preview(1,pending.Token,default),404);
        var active=await db.OrganizationInvitations.Where(i=>i.AcceptedAt==null && i.RevokedAt==null && i.CreatedByUserId==1).ToListAsync();
        foreach(var item in active){item.RevokedAt=clock.GetUtcNow().UtcDateTime;item.Version++;}await db.SaveChangesAsync();
        for(var n=0;n<20;n++)await service.Create(1,"Member",default);
        await Denied(()=>service.Create(1,"Member",default),409);
        var expiring=await db.OrganizationInvitations.Where(i=>i.RevokedAt==null && i.AcceptedAt==null).FirstAsync();
        clock.Advance(TimeSpan.FromHours(24));
        // After expiry the bounded pending quota is released.
        var after=await service.Create(1,"Viewer",default);clock.Advance(TimeSpan.FromHours(24));
        await Denied(()=>service.Preview(1,after.Token,default),404);
    }
    Console.WriteLine("PASS: real v7->v8 preservation/idempotency/backup/restore/late DDL rollback; hashed one-use/expiry/revocation/issuer-version capabilities, owner-only operations, consent, no reactivation/downgrade/group grants, concurrent consumption and atomic ownership transfer. No external services.");
}finally{SqliteConnection.ClearAllPools();Directory.Delete(root,true);}

sealed class V7Context(DbContextOptions<PortalDbContext> options):PortalDbContext(options){protected override void OnModelCreating(ModelBuilder model){base.OnModelCreating(model);model.Ignore<AiGateway.Channels.MessageDeliveryEvent>();model.Ignore<AiGateway.Channels.ChannelMessage>();model.Ignore<AiGateway.Channels.ChannelConversation>();model.Ignore<AiGateway.Channels.ChannelContact>();model.Ignore<AiGateway.Channels.ChannelConnection>();model.Ignore<OrganizationInvitation>();}}
sealed class TestClock:TimeProvider{
    private DateTimeOffset current=new(2026,11,8,12,0,0,TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow()=>current;
    public void Advance(TimeSpan duration)=>current+=duration;
}
