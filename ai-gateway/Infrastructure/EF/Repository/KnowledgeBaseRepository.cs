using AiGateway.Application.Interfaces;
using Portal.AI.Gateway.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Portal.AI.Gateway.Infrastructure;

public class KnowledgeBaseRepository : IKnowledgeBaseRepository
{
    private readonly PortalDbContext _ctx;
    private readonly IServiceProvider _services;
    private readonly bool _advanced;
    private IOnnxEmbeddingService Embeddings => _services.GetRequiredService<IOnnxEmbeddingService>();

    public KnowledgeBaseRepository(PortalDbContext ctx, IServiceProvider services, IConfiguration config)
    { _ctx = ctx; _services = services; _advanced = config.GetValue<bool>("Chat:UseAdvancedRag"); }

    public async Task<string?> GetOrganizationBaseAsync(CancellationToken ct)
    {
        var item = await _ctx.KnowledgeBaseItems
            .Where(x => x.KeyName == "organization_base" && x.IsActive)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return item?.Content;
    }

    public async Task<List<KnowledgeBaseItem>> GetAllAsync(CancellationToken ct)
    {
        return await _ctx.KnowledgeBaseItems
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<KnowledgeBaseItem?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await _ctx.KnowledgeBaseItems
            .FirstOrDefaultAsync(x => x.Id == id, ct);
    }

    public async Task<KnowledgeBaseItem> SaveAsync(KnowledgeBaseItem item, CancellationToken ct)
    {
        item.Embedding = _advanced ? ToBytes(Embeddings.Generate(item.Content)) : null;
        if (item.Id == 0)
        {
            item.CreatedAt = DateTime.UtcNow;
            item.IsActive = true;
            _ctx.KnowledgeBaseItems.Add(item);
        }
        else
        {
            _ctx.KnowledgeBaseItems.Update(item);
        }

        await _ctx.SaveChangesAsync(ct);
        return item;

    
    }

    byte[] ToBytes(float[] v) =>
        v.SelectMany(BitConverter.GetBytes).ToArray();


    public async Task ToggleActiveAsync(int id, CancellationToken ct)
    {
        var item = await _ctx.KnowledgeBaseItems
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (item is null) return;

        item.IsActive = !item.IsActive;
        await _ctx.SaveChangesAsync(ct);
    }

    public async Task<List<KnowledgeBaseItem>> SemanticSearch(string query)
    {
        if (!_advanced) throw new InvalidOperationException("Busca vetorial requer Chat:UseAdvancedRag.");
        var qVec = Embeddings.Generate(query);

        var all = await _ctx.KnowledgeBaseItems.ToListAsync(); 

        return all.OrderBy(x => CosineDistance(FromBytes(x.Embedding), qVec))
                  .Take(5)
                  .ToList();
    }

    float[] FromBytes(byte[]? mem) =>
        mem == null ? Array.Empty<float>() :
        Enumerable.Range(0, mem.Length / 4)
            .Select(i => BitConverter.ToSingle(mem, i * 4)).ToArray();

    float CosineDistance(float[] a, float[] b)
    {
        float dot = a.Zip(b, (x, y) => x * y).Sum();
        float na = MathF.Sqrt(a.Sum(x => x * x));
        float nb = MathF.Sqrt(b.Sum(x => x * x));
        return 1 - (dot / (na * nb));
    }

}
