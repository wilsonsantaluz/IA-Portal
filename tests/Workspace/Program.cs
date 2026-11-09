using System.Security.Cryptography;
using System.Text;
using AiGateway.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Portal.AI.Gateway.Infrastructure;

var root=Path.Combine(Path.GetTempPath(),"portal-workspace-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
var cfg=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
    ["SQLITE_BACKUP_DIRECTORY"]=Path.Combine(root,"backups"),["DEEPSEEK_API_KEY"]="synthetic-only",
    ["DEEPSEEK_MODELS"]="model-a,model-b",["DEEPSEEK_DEFAULT_MODEL"]="model-a"
}).Build();
DbContextOptions<PortalDbContext> Options(string path)=>new DbContextOptionsBuilder<PortalDbContext>().UseSqlite($"Data Source={path};Foreign Keys=True").Options;
void Check(bool ok,string message) {if(!ok)throw new Exception(message);}
async Task Fixture(string path,bool fail=false) {
    await using var db=new V6Context(Options(path));await db.Database.EnsureCreatedAsync();
    db.Organizations.Add(new(){Id=1,Name="Legacy",Slug="default"});
    db.Users.Add(new(){UserName="Legacy admin",UserEmail="legacy@example.test",PasswordHash="unchanged",IsActive=true});
    db.Users.Add(new(){UserName="Second admin",UserEmail="second@example.test",PasswordHash="unchanged-second",IsActive=true});
    db.Roles.Add(new(){RoleName="admin",OrganizationId=1});await db.SaveChangesAsync();
    db.UserRoles.Add(new(){UserId=1,RoleId=1});db.OrganizationMemberships.Add(new(){UserId=1,OrganizationId=1});
    db.UserRoles.Add(new(){UserId=2,RoleId=1});db.OrganizationMemberships.Add(new(){UserId=2,OrganizationId=1});
    db.AiUserPreferences.Add(new(){UserId=1,Provider="deepseek",Model="model-b"});
    db.KnowledgeBaseItems.Add(new(){KeyName="legacy",Content="Human content preserved",Section="Legacy",IsActive=true});await db.SaveChangesAsync();
    await db.Database.ExecuteSqlRawAsync("CREATE TABLE SchemaVersions (Version INTEGER PRIMARY KEY,Checksum TEXT NOT NULL,AppliedAt TEXT NOT NULL)");
    var names=new[]{"001_ChatSessions","002_TitleUsage","003_ChatUsage","004_AiProviders","005_Organizations","006_DataConnectors"};
    for(var i=0;i<names.Length;i++) {
        using var resource=typeof(ChatSchemaMigrator).Assembly.GetManifestResourceStream($"AiGateway.Infrastructure.EF.Migrations.{names[i]}.sql")!;
        var text=(await new StreamReader(resource).ReadToEndAsync()).Replace("\r\n","\n");var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SchemaVersions VALUES ({i+1},{hash},{DateTime.UtcNow})");
    }
    if(fail)await db.Database.ExecuteSqlRawAsync("CREATE TABLE Collision(Id INTEGER); CREATE INDEX IX_OrganizationAiPreferences_UserId ON Collision(Id)");
}
try {
    var path=Path.Combine(root,"v6.sqlite3");await Fixture(path);
    await using(var db=new PortalDbContext(Options(path))) {
        await ChatSchemaMigrator.InitializeAsync(db,cfg);await ChatSchemaMigrator.InitializeAsync(db,cfg);
        Check((await db.OrganizationMemberships.SingleAsync(x=>x.UserId==1)).PolicyRole=="Owner" && (await db.OrganizationMemberships.SingleAsync(x=>x.UserId==2)).PolicyRole=="Admin","Exactly one deterministic Owner, other admins preserved");
        Check((await db.Users.SingleAsync(x=>x.UserId==1)).PasswordHash=="unchanged" && (await db.KnowledgeBaseItems.SingleAsync()).Content=="Human content preserved","Preservation");
        Check((await db.OrganizationAiPreferences.SingleAsync()).OrganizationId==1 && (await db.OrganizationAiPreferences.SingleAsync()).Model=="model-b","Legacy preference belongs only to org 1");
        var catalog=new AiProviderCatalog(db,cfg);var workspace=new OrganizationWorkspaceService(db,catalog,NullLogger<OrganizationWorkspaceService>.Instance);
        await workspace.Create(1,new("Second synthetic","second-test"),default);
        Check(await db.OrganizationMemberships.CountAsync(x=>x.UserId==1)==2,"Multiple organizations");
        Check(await db.Roles.CountAsync(x=>x.OrganizationId==2 && x.RoleName=="admin")==1,"Local admin role initialized");
    }
    var backup=Directory.GetFiles(Path.Combine(root,"backups")).Single();var restored=Path.Combine(root,"restored.sqlite3");
    using(var source=new SqliteConnection($"Data Source={backup}")) using(var target=new SqliteConnection($"Data Source={restored}")) {
        source.Open();target.Open();source.BackupDatabase(target);
        using var c=target.CreateCommand();c.CommandText="SELECT count(*) FROM pragma_table_info('OrganizationMemberships') WHERE name='PolicyRole'";
        Check(Convert.ToInt64(c.ExecuteScalar())==0,"Backup precedes migration");
    }
    await using(var db=new PortalDbContext(Options(restored))) {await ChatSchemaMigrator.InitializeAsync(db,cfg);Check((await db.OrganizationMemberships.SingleAsync(x=>x.UserId==1)).PolicyRole=="Owner","Restore and upgrade");}
    var broken=Path.Combine(root,"broken.sqlite3");await Fixture(broken,true);
    await using(var db=new PortalDbContext(Options(broken))) {
        try {await ChatSchemaMigrator.InitializeAsync(db,cfg);throw new Exception("Injected failure ignored");}catch(SqliteException){}
        using var c=db.Database.GetDbConnection().CreateCommand();c.CommandText="SELECT count(*) FROM pragma_table_info('OrganizationMemberships') WHERE name='PolicyRole'";
        Check(Convert.ToInt64(await c.ExecuteScalarAsync())==0,"Failed DDL rolled back");
        c.CommandText="SELECT count(*) FROM SchemaVersions WHERE Version=7";Check(Convert.ToInt64(await c.ExecuteScalarAsync())==0,"Failed version not recorded");
    }
    var scope=new OrganizationScope();typeof(OrganizationScope).GetProperty("Id")!.SetValue(scope,2);typeof(OrganizationScope).GetProperty("UserId")!.SetValue(scope,1);
    await using(var db=new PortalDbContext(Options(path),scope)) {
        var catalog=new AiProviderCatalog(db,cfg);var workspace=new OrganizationWorkspaceService(db,catalog,NullLogger<OrganizationWorkspaceService>.Instance);
        Check((await catalog.Preference(1,default)).Model=="model-a","No preference leakage from organization 1");
        await workspace.SetDefault(1,new("deepseek","model-b",0),default);
        Check((await catalog.Default(1,default)).Model=="model-b","Organization default applied");
        await catalog.SetPreference(1,new("deepseek","model-a"),default);
        Check((await catalog.Default(1,default)).Model=="model-a","Local personal preference takes precedence");
        Check(await db.OrganizationAiPreferences.CountAsync()==1 && await db.KnowledgeBaseItems.CountAsync()==0,"Query filters");
        var owner=await db.OrganizationMemberships.SingleAsync(x=>x.OrganizationId==2 && x.UserId==1);
        try {await workspace.ChangeMember(1,1,new("Viewer",true,owner.Version),default);throw new Exception("Last owner removed");} catch(ChatException e){Check(e.Status==409,"Last owner status");}
        db.OrganizationAiDefaults.Add(new(){OrganizationId=1,Provider="deepseek",Model="model-a"});
        try {await db.SaveChangesAsync();throw new Exception("Cross-tenant insert allowed");} catch(ChatException e){Check(e.Status==403,"Cross-tenant insert rejected");}
        db.ChangeTracker.Clear();
        Check((await db.OrganizationAiDefaults.SingleAsync()).OrganizationId==2,"Write scope remains local");
        await VectorIsolationChecks.Run(db);
    }
    Console.WriteLine("PASS: v6->v7 preservation/Owner backfill/preference migration/idempotency/backup/restore/DDL rollback; organization creation, tenant defaults/preferences and write filters; last Owner protected.");
} finally {SqliteConnection.ClearAllPools();Directory.Delete(root,true);}

sealed class V6Context(DbContextOptions<PortalDbContext> options):PortalDbContext(options) {
    protected override void OnModelCreating(ModelBuilder model) {
        base.OnModelCreating(model);model.Ignore<AiGateway.Channels.MessageDeliveryEvent>();model.Ignore<AiGateway.Channels.ChannelMessage>();model.Ignore<AiGateway.Channels.ChannelConversation>();model.Ignore<AiGateway.Channels.ChannelContact>();model.Ignore<AiGateway.Channels.ChannelConnection>();model.Ignore<OrganizationInvitation>(); model.Ignore<OrganizationAiDefault>();model.Ignore<OrganizationAiPreference>();
        model.Entity<OrganizationMembership>().Ignore(x=>x.PolicyRole).Ignore(x=>x.Version);
    }
}
