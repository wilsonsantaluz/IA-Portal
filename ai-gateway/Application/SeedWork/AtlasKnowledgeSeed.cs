using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Infrastructure;

namespace AiGateway.Application.SeedWork;

/// <summary>Insert-only, opt-in snapshot. Existing keys always belong to the operator.</summary>
public static class AtlasKnowledgeSeed
{
    public static async Task InitializeAsync(PortalDbContext db, IConfiguration config,
        CancellationToken ct = default)
    {
        if (!config.GetValue<bool>("Demo:Atlas:SeedKnowledge")) return;
        using var stream = typeof(AtlasKnowledgeSeed).Assembly.GetManifestResourceStream(
            "AiGateway.Application.SeedWork.atlas-v1.json")
            ?? throw new InvalidOperationException("Snapshot Atlas v1 ausente.");
        var items = await JsonSerializer.DeserializeAsync<List<KnowledgeBaseItem>>(stream, cancellationToken: ct)
            ?? throw new InvalidOperationException("Snapshot Atlas v1 inválido.");
        if (items.Count != 10 || items.Select(x => x.KeyName).Distinct().Count() != 10)
            throw new InvalidOperationException("Snapshot Atlas deve conter dez documentos distintos.");

        // SQLite serializes writers. A single transaction also prevents partial seeds.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var keys = items.Select(x => x.KeyName).ToArray();
        var existing = await db.KnowledgeBaseItems.Where(x => keys.Contains(x.KeyName))
            .Select(x => x.KeyName).ToListAsync(ct);
        foreach (var item in items.Where(x => !existing.Contains(x.KeyName)))
        {
            item.IsActive = true;
            item.CreatedAt = DateTime.UtcNow;
            db.KnowledgeBaseItems.Add(item);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        // Vector indexing is explicit via the existing authenticated reindex endpoint.
        // No model downloads or network calls are required for local knowledge lookup.
    }
}
