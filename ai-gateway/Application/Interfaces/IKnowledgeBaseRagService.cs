using System.Threading;
using System.Threading.Tasks;
using AiGateway.Dtos;

namespace AiGateway.Application.Interfaces
{
    /// <summary>
    /// Serviço de alto nível que orquestra o fluxo RAG sobre a Knowledge Base.
    /// </summary>
    public interface IKnowledgeBaseRagService
    {
        /// <summary>
        /// Executa uma pergunta utilizando RAG sobre a tabela KnowledgeBaseItem.
        /// </summary>
        Task<AskKbRagResponse> AskAsync(AskKbRagRequest request, Boolean fullResponse, CancellationToken ct = default);
    }
}
