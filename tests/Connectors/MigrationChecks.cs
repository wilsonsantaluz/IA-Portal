using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Portal.AI.Gateway.Infrastructure;

static class MigrationChecks
{
    public static async Task Run()
    {
        var folder = Path.Combine(Path.GetTempPath(),"portal-connector-schema-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["SQLITE_BACKUP_DIRECTORY"]=Path.Combine(folder,"backups") }).Build();
        DbContextOptions<PortalDbContext> Options(string path) => new DbContextOptionsBuilder<PortalDbContext>().UseSqlite($"Data Source={path};Foreign Keys=True").Options;
        void Check(bool ok) { if (!ok) throw new Exception("Connector migration invariant failed"); }
        async Task Fixture(string path, bool fail)
        {
            await using var db = new PreConnectorContext(Options(path)); await db.Database.EnsureCreatedAsync();
            db.Organizations.Add(new Organization { Id=1, Name="Padrao",Slug="default" }); await db.SaveChangesAsync();
            db.KnowledgeBaseItems.Add(new KnowledgeBaseItem { KeyName="legacy",Content="preserved",Section="test" }); await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE SchemaVersions (Version INTEGER PRIMARY KEY, Checksum TEXT NOT NULL, AppliedAt TEXT NOT NULL)");
            var names = new[] { "001_ChatSessions", "002_TitleUsage", "003_ChatUsage", "004_AiProviders", "005_Organizations" };
            for (var v=1;v<=5;v++) {
                using var r = typeof(ChatSchemaMigrator).Assembly.GetManifestResourceStream($"AiGateway.Infrastructure.EF.Migrations.{names[v-1]}.sql")!;
                var sql = (await new StreamReader(r).ReadToEndAsync()).Replace("\r\n","\n");
                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO SchemaVersions VALUES ({v},{hash},{DateTime.UtcNow})");
            }
            if (fail) await db.Database.ExecuteSqlRawAsync("CREATE TABLE Collision(Id INTEGER); CREATE INDEX IX_DataConnectors_OrganizationId ON Collision(Id)");
        }
        try {
            var path = Path.Combine(folder,"v5.db"); await Fixture(path,false);
            await using (var db = new PortalDbContext(Options(path))) {
                await ChatSchemaMigrator.InitializeAsync(db,cfg); await ChatSchemaMigrator.InitializeAsync(db,cfg);
                Check(await db.DataConnectors.CountAsync()==0 && (await db.KnowledgeBaseItems.SingleAsync()).Content=="preserved");
            }
            var backup = Directory.GetFiles(Path.Combine(folder,"backups")).Single();
            var restored = Path.Combine(folder,"restored.db");
            using (var a=new SqliteConnection($"Data Source={backup}")) using (var b=new SqliteConnection($"Data Source={restored}")) {
                a.Open();b.Open();a.BackupDatabase(b);using var c=b.CreateCommand();c.CommandText="PRAGMA integrity_check";Check((string?)c.ExecuteScalar()=="ok");
                c.CommandText="SELECT count(*) FROM sqlite_master WHERE name='DataConnectors'";Check(Convert.ToInt64(c.ExecuteScalar())==0);
            }
            await using (var db=new PortalDbContext(Options(restored))) { await ChatSchemaMigrator.InitializeAsync(db,cfg); Check(await db.DataConnectors.CountAsync()==0); }
            var broken=Path.Combine(folder,"broken.db");await Fixture(broken,true);
            await using (var db=new PortalDbContext(Options(broken))) {
                try { await ChatSchemaMigrator.InitializeAsync(db,cfg); throw new Exception("Expected migration failure"); } catch (SqliteException) { }
                using var c=db.Database.GetDbConnection().CreateCommand();c.CommandText="SELECT count(*) FROM SchemaVersions WHERE Version=6";Check(Convert.ToInt64(await c.ExecuteScalarAsync())==0);
                c.CommandText="SELECT count(*) FROM sqlite_master WHERE name='DataConnectors'";Check(Convert.ToInt64(await c.ExecuteScalarAsync())==0);
                c.CommandText="SELECT Content FROM KnowledgeBaseItem";Check((string?)await c.ExecuteScalarAsync()=="preserved");
            }
            Console.WriteLine("PASS: real v5 -> v6 migration, existing data preserved, backup before change, idempotency, restore+upgrade and injected DDL rollback.");
        }
        finally { SqliteConnection.ClearAllPools(); Directory.Delete(folder,true); }
    }
}
sealed class PreConnectorContext(DbContextOptions<PortalDbContext> options) : PortalDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder model) { base.OnModelCreating(model); model.Ignore<ConnectorGrant>(); model.Ignore<DataConnector>(); }
}
