using AiGateway.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Portal.AI.Gateway.Infrastructure;
using System.Net.Mail;

static void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
static async Task Reject(Func<Task> action,int status) {
    try {await action();throw new Exception("Unexpected success");}catch(ChatException ex){Check(ex.Status==status,"Wrong rejection status");}
}
var folder=Path.Combine(Path.GetTempPath(),"portal-signup-"+Guid.NewGuid());Directory.CreateDirectory(folder);
var path=Path.Combine(folder,"portal.sqlite3");
var cfg=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
    ["Signup:Enabled"]="true",["Signup:PublicUrl"]="https://portal.example.test",
    ["Smtp:Username"]="sender@example.test",["Smtp:AppPassword"]="synthetic-test-only",
    ["SQLITE_BACKUP_DIRECTORY"]=Path.Combine(folder,"backups")
}).Build();
var options=new DbContextOptionsBuilder<PortalDbContext>().UseSqlite($"Data Source={path};Foreign Keys=True").Options;
try {
    await using var db=new PortalDbContext(options);
    await ChatSchemaMigrator.InitializeAsync(db,cfg);
    foreach(var grant in PortalAccess.Catalog)db.Permissions.Add(new(){PermissionKey=grant.Key,Description=grant.Description});
    var atlas=new Organization{Name="Synthetic Atlas",Slug="atlas-demo"};db.Organizations.Add(atlas);
    db.Users.Add(new(){UserName="Existing",UserEmail="existing@example.test",PasswordHash="preserve",IsActive=false});
    await db.SaveChangesAsync();
    var demo=new DataConnector{OrganizationId=atlas.Id,Name="Demo",Category="demo",Enabled=true};
    var business=new DataConnector{OrganizationId=atlas.Id,Name="Private",Category="business",Enabled=true};
    db.DataConnectors.AddRange(demo,business);await db.SaveChangesAsync();
    // Exercise a real v9 -> v10 upgrade using the preserved pre-signup schema.
    await db.Database.ExecuteSqlRawAsync("DROP TABLE ActionAuditEvents; DROP TABLE SignupRequests; DELETE FROM SchemaVersions WHERE Version>=10");
    await ChatSchemaMigrator.InitializeAsync(db,cfg);await ChatSchemaMigrator.InitializeAsync(db,cfg);
    Check(await db.Users.CountAsync()==1 && (await db.Users.SingleAsync()).PasswordHash=="preserve","Migration changed accounts");
    var backups=Directory.GetFiles(Path.Combine(folder,"backups"));Check(backups.Length==1,"Missing single pre-upgrade backup");
    using(var backup=new SqliteConnection($"Data Source={backups[0]}")) {
        backup.Open();using var cmd=backup.CreateCommand();cmd.CommandText="SELECT count(*) FROM sqlite_master WHERE name='SignupRequests'";
        Check(Convert.ToInt32(cmd.ExecuteScalar())==0,"Backup after change");
        cmd.CommandText="PRAGMA integrity_check";Check((string?)cmd.ExecuteScalar()=="ok","Invalid backup");
    }
    var restored=Path.Combine(folder,"restored.sqlite3");File.Copy(backups[0],restored);
    await using(var restoreDb=new PortalDbContext(new DbContextOptionsBuilder<PortalDbContext>().UseSqlite($"Data Source={restored}").Options)) {
        await ChatSchemaMigrator.InitializeAsync(restoreDb,cfg);
        Check(await restoreDb.Users.CountAsync()==1,"Restore lost account");
    }
    var broken=Path.Combine(folder,"broken.sqlite3");File.Copy(backups[0],broken);
    await using(var brokenDb=new PortalDbContext(new DbContextOptionsBuilder<PortalDbContext>().UseSqlite($"Data Source={broken}").Options)) {
        await brokenDb.Database.ExecuteSqlRawAsync("CREATE TABLE Collision(Id INTEGER); CREATE INDEX IX_SignupRequests_Email ON Collision(Id)");
        try {await ChatSchemaMigrator.InitializeAsync(brokenDb,cfg);throw new Exception("DDL collision accepted");}catch(SqliteException){}
        await brokenDb.Database.OpenConnectionAsync();using var command=brokenDb.Database.GetDbConnection().CreateCommand();
        command.CommandText="SELECT count(*) FROM sqlite_master WHERE name='SignupRequests'";
        Check(Convert.ToInt32(await command.ExecuteScalarAsync())==0,"Signup DDL was not rolled back");
        command.CommandText="SELECT count(*) FROM SchemaVersions WHERE Version=10";
        Check(Convert.ToInt32(await command.ExecuteScalarAsync())==0,"Failed migration recorded");
    }
    var clock=new TestClock();var mail=new Mailer();var scope=new OrganizationScope();
    var service=new AtlasSignupService(db,scope,cfg,clock,mail,NullLogger<AtlasSignupService>.Instance);
    var input=new SignupInput("Synthetic Visitor","visitor@example.test","Synthetic-password-2026");
    await service.Register(input,default);
    Check(await db.Users.CountAsync()==1,"Account before verification");
    var pending=await db.SignupRequests.AsNoTracking().SingleAsync();
    var token=mail.Link.Split("#confirm=")[1];Check(pending.TokenHash!=token && BCrypt.Net.BCrypt.Verify(input.Password,pending.PasswordHash),"Unsafe credentials/token persistence");
    await service.Register(input,default);Check(mail.Count==1,"Cooldown ignored");
    await service.Confirm(token,default);db.ChangeTracker.Clear();
    var user=await db.Users.SingleAsync(u=>u.UserEmail==input.Email);
    var membership=await db.OrganizationMemberships.SingleAsync(m=>m.UserId==user.UserId);
    Check(membership.OrganizationId==atlas.Id && membership.PolicyRole is null,"Wrong tenant/policy");
    var access=await PortalAccess.ReadAsync(db,user.UserId,default,atlas.Id);
    Check(access.Permissions.Order().SequenceEqual(AtlasSignupService.Grants.Order()),"Unexpected grants");
    Check(!access.Permissions.Contains(PortalAccess.EditKnowledge) && !access.Permissions.Contains(PortalAccess.Administration),"Privilege escalation");
    Check((await PortalAccess.ReadAsync(db,user.UserId,default,1)).Permissions.Length==0,"Default tenant access");
    Check(await db.ConnectorGrants.CountAsync(g=>g.ConnectorId==demo.Id)==1 && !await db.ConnectorGrants.AnyAsync(g=>g.ConnectorId==business.Id),"Connector leak");
    await Reject(()=>service.Confirm(token,default),400);
    await service.Register(input with {Email="existing@example.test"},default);Check(mail.Count==1,"Existing account delivered/changed");
    await service.Register(input with {Email="expired@example.test"},default);var expired=mail.Link.Split("#confirm=")[1];
    clock.Advance=TimeSpan.FromMinutes(31);await Reject(()=>service.Confirm(expired,default),400);
    await Reject(()=>service.Confirm(new string('0',64),default),400);
    mail.Fail=true;await Reject(()=>service.Register(input with {Email="delivery-failed@example.test"},default),503);
    Check(!await db.SignupRequests.AnyAsync(r=>r.Email=="delivery-failed@example.test"),"Failed delivery not cleaned up");
    cfg["Signup:Enabled"]="false";await Reject(()=>service.Confirm(expired,default),404);
    Console.WriteLine("PASS: v9 migration/preservation/idempotency/backup integrity; verification before account creation; hashed token/password; cooldown; Atlas-only edit-without-management grants; demo-only connector grants; replay, expiry, invalid token, disabled signup, existing disabled account and delivery failure.");
} finally { SqliteConnection.ClearAllPools(); Directory.Delete(folder,true); }

sealed class TestClock : TimeProvider {
    public TimeSpan Advance {get;set;}
    public override DateTimeOffset GetUtcNow()=>new DateTimeOffset(2026,11,11,12,0,0,TimeSpan.Zero)+Advance;
}
sealed class Mailer : ISignupMailer {
    public string Link="";public int Count;public bool Fail;
    public Task Send(string email,string link,CancellationToken ct) {
        if(Fail)throw new SmtpException("synthetic failure");Link=link;Count++;return Task.CompletedTask;
    }
}
