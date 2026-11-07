using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Portal.AI.Gateway.Infrastructure;

var folder = Path.Combine(Path.GetTempPath(), "portal-organizations-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["SQLITE_BACKUP_DIRECTORY"] = Path.Combine(folder,"backups") }).Build();
DbContextOptions<PortalDbContext> Options(string path) => new DbContextOptionsBuilder<PortalDbContext>().UseSqlite($"Data Source={path};Foreign Keys=True").Options;
void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
async Task Fixture(string path, bool conflict = false)
{
    await using var db = new PreOrganizationContext(Options(path));
    await db.Database.EnsureCreatedAsync();
    db.Users.Add(new User { UserName = "Legado sintetico", UserEmail = "legacy@example.test", PasswordHash = "unchanged-synthetic", IsActive = true });
    db.Roles.Add(new Role { RoleName = "admin" });
    db.KnowledgeBaseItems.Add(new KnowledgeBaseItem { KeyName = "legacy", Content = "Preservar integralmente", Section = "Legado", IsActive = true });
    await db.SaveChangesAsync();
    db.UserRoles.Add(new UserRole { RoleId = 1, UserId = 1 });
    db.ChatSessions.Add(new ChatSession { Id = Guid.NewGuid(), OwnerUserId = 1, UserId = "1", Title = "Anterior a organizacoes", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
    await db.SaveChangesAsync();
    await db.Database.ExecuteSqlRawAsync("CREATE TABLE SchemaVersions (Version INTEGER PRIMARY KEY, Checksum TEXT NOT NULL, AppliedAt TEXT NOT NULL)");
    for (var version = 1; version <= 4; version++)
    {
        var names = new[] { "", "001_ChatSessions", "002_TitleUsage", "003_ChatUsage", "004_AiProviders" };
        using var resource = typeof(ChatSchemaMigrator).Assembly.GetManifestResourceStream($"AiGateway.Infrastructure.EF.Migrations.{names[version]}.sql")!;
        var script = (await new StreamReader(resource).ReadToEndAsync()).Replace("\r\n", "\n");
        var checksum = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(script)));
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SchemaVersions VALUES ({version},{checksum},{DateTime.UtcNow})");
    }
    if (conflict) await db.Database.ExecuteSqlRawAsync("CREATE TABLE Collision(Id INTEGER); CREATE INDEX IX_KnowledgeBaseItem_OrganizationId_KeyName ON Collision(Id)");
}
try
{
    var path = Path.Combine(folder,"v4.sqlite3");
    await Fixture(path);
    await using (var db = new PortalDbContext(Options(path)))
    {
        await ChatSchemaMigrator.InitializeAsync(db,cfg);
        await ChatSchemaMigrator.InitializeAsync(db,cfg);
        Check(await db.Organizations.CountAsync() == 1, "Default organization missing/duplicated");
        Check((await db.Users.SingleAsync()).PasswordHash == "unchanged-synthetic", "Credentials modified");
        Check((await db.KnowledgeBaseItems.SingleAsync()).Content == "Preservar integralmente", "Knowledge lost");
        Check((await db.ChatSessions.SingleAsync()).OwnerUserId == 1 && (await db.ChatSessions.SingleAsync()).OrganizationId == 1, "Session ownership changed");
        Check(await db.OrganizationMemberships.CountAsync(x => x.OrganizationId == 1 && x.UserId == 1) == 1, "Membership backfill");
        Check((await db.Roles.SingleAsync()).OrganizationId == 1 && await db.UserRoles.CountAsync() == 1, "RBAC migration");
    }
    var backup = Directory.GetFiles(Path.Combine(folder,"backups"),"*.sqlite3").Single();
    var restored = Path.Combine(folder,"restored.sqlite3");
    using (var source = new SqliteConnection($"Data Source={backup}"))
    using (var target = new SqliteConnection($"Data Source={restored}"))
    {
        source.Open(); target.Open(); source.BackupDatabase(target);
        using var c = target.CreateCommand(); c.CommandText = "PRAGMA integrity_check";
        Check((string?)c.ExecuteScalar() == "ok", "Backup not restorable");
        c.CommandText = "SELECT count(*) FROM sqlite_master WHERE name='Organizations'";
        Check(Convert.ToInt64(c.ExecuteScalar()) == 0, "Backup taken after schema change");
    }
    await using (var db = new PortalDbContext(Options(restored))) { await ChatSchemaMigrator.InitializeAsync(db,cfg); Check(await db.OrganizationMemberships.CountAsync() == 1, "Restored upgrade failed"); }
    var broken = Path.Combine(folder,"broken.sqlite3"); await Fixture(broken,true);
    await using (var db = new PortalDbContext(Options(broken)))
    {
        try { await ChatSchemaMigrator.InitializeAsync(db,cfg); throw new Exception("DDL collision accepted"); } catch (SqliteException) { }
        using var c = db.Database.GetDbConnection().CreateCommand();
        c.CommandText = "SELECT count(*) FROM SchemaVersions WHERE Version=5"; Check(Convert.ToInt64(await c.ExecuteScalarAsync()) == 0, "Failed version recorded");
        c.CommandText = "SELECT count(*) FROM pragma_table_info('KnowledgeBaseItem') WHERE name='OrganizationId'"; Check(Convert.ToInt64(await c.ExecuteScalarAsync()) == 0, "DDL not rolled back");
        c.CommandText = "SELECT Content FROM KnowledgeBaseItem"; Check((string?)await c.ExecuteScalarAsync() == "Preservar integralmente", "Rollback lost content");
    }
    Console.WriteLine("PASS: real v4 -> v5, default membership/RBAC/session/KB preservation, idempotency, pre-change consistent backup, restore+upgrade, injected failure rolls back DDL/version/data.");
}
finally { SqliteConnection.ClearAllPools(); Directory.Delete(folder,true); }
