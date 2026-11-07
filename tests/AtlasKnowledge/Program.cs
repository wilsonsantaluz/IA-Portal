using AiGateway.Application.SeedWork;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Portal.AI.Gateway.Infrastructure;

void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
await using var connection = new SqliteConnection("Data Source=:memory:");
await connection.OpenAsync();
await using var db = new PortalDbContext(new DbContextOptionsBuilder<PortalDbContext>().UseSqlite(connection).Options);
var off = new ConfigurationBuilder().Build();
var on = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
    ["Demo:Atlas:SeedKnowledge"] = "true"
}).Build();
await DatabaseInitializer.InitializeAsync(db, off);
Check(await db.KnowledgeBaseItems.CountAsync() == 1, "Seed must be opt-in");
var legacy = await db.KnowledgeBaseItems.SingleAsync();
var original = legacy.Content;
await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_atlas BEFORE INSERT ON KnowledgeBaseItem WHEN NEW.KeyName = 'atlas-aging-policy' BEGIN SELECT RAISE(ABORT, 'injected'); END;");
try { await AtlasKnowledgeSeed.InitializeAsync(db, on); throw new Exception("Injected failure not observed"); }
catch (DbUpdateException) { }
db.ChangeTracker.Clear();
Check(await db.KnowledgeBaseItems.CountAsync() == 1, "Partial seed survived rollback");
await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fail_atlas");
await AtlasKnowledgeSeed.InitializeAsync(db, on);
var docs = await db.KnowledgeBaseItems.Where(x => x.KeyName.StartsWith("atlas-")).ToListAsync();
Check(docs.Count == 10 && docs.All(x => x.IsActive && x.Subsection == "synthetic-demo / v1"), "Snapshot metadata");
Check(docs.All(x => x.Content.Contains("fict") && !string.IsNullOrWhiteSpace(x.Section)), "Synthetic content/category");
var edited = docs.Single(x => x.KeyName == "atlas-collections-policy");
edited.Content = "Edição humana preservada";
edited.Title = "Título editado";
edited.IsActive = false;
await db.SaveChangesAsync();
await DatabaseInitializer.InitializeAsync(db, on);
await DatabaseInitializer.InitializeAsync(db, on);
db.ChangeTracker.Clear();
edited = await db.KnowledgeBaseItems.SingleAsync(x => x.KeyName == "atlas-collections-policy");
Check(edited.Content == "Edição humana preservada" && edited.Title == "Título editado" && !edited.IsActive, "Human edits/state overwritten");
Check(await db.KnowledgeBaseItems.CountAsync() == 11, "Duplicate documents");
Check((await db.KnowledgeBaseItems.SingleAsync(x => x.Id == legacy.Id)).Content == original, "Legacy knowledge overwritten");
Check(await db.KnowledgeBaseItems.CountAsync(x => x.IsActive && (x.Title + " " + x.Content).Contains("negociação")) > 0, "Local retrieval candidates missing");
Console.WriteLine("PASS: opt-in, ten documents, metadata, idempotency, human edits, inactive state, legacy preservation, local retrieval candidates; no external services.");
