using AiGateway.Dtos;
using Portal.AI.Gateway.Infrastructure;
using System.Threading;
using System.Threading.Tasks;

namespace AiGateway.Application.Interfaces
{
    public interface IKnowledgeBaseService
    {
        Task<int> UpsertAsync(UpsertKbItemRequest req, CancellationToken ct = default);
        Task ReindexItemInQdrantAsync(KnowledgeBaseItem item, CancellationToken ct);
    }
}
