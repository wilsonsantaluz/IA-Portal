using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Infrastructure;

namespace AiGateway.Infrastructure.Qdrant.Services.Chats;

// Shared bounded textual retrieval. No model call and no semantic/ReRAG claim.
public sealed class LocalKnowledgeRetriever(PortalDbContext db) {
    public async Task<List<KnowledgeBaseItem>> Retrieve(string question,int[]? allowed,CancellationToken ct) {
        var query=db.KnowledgeBaseItems.AsNoTracking().Where(x=>x.IsActive);
        if(allowed is not null)query=query.Where(x=>allowed.Contains(x.Id));
        var candidates=await query.OrderByDescending(x=>x.CreatedAt).Take(200).ToListAsync(ct);
        var words=question.Split(' ',StringSplitOptions.RemoveEmptyEntries).Where(x=>x.Length>3).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToArray();
        return candidates.Select(x=>new{Item=x,Score=words.Count(w=>(x.Title+" "+x.Content).Contains(w,StringComparison.OrdinalIgnoreCase))})
            .Where(x=>x.Score>0 || x.Item.KeyName=="organization_base").OrderByDescending(x=>x.Score).Take(8).Select(x=>x.Item).ToList();
    }
}
