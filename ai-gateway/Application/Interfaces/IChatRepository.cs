// Application/Interfaces/IChatRepository.cs
using Portal.AI.Gateway.Infrastructure;

namespace Portal.AI.Gateway.Application.Interfaces;

public interface IChatRepository
{
    Task<ChatSession> GetOrCreateSessionAsync(Guid sessionId, string? userId, CancellationToken ct);
    Task<List<ChatMessage>> GetLastMessagesAsync(Guid sessionId, int limit, CancellationToken ct);
    Task AddMessageAsync(ChatMessage message, CancellationToken ct);
}

public interface IKnowledgeBaseRepository
{
    Task<string?> GetOrganizationBaseAsync(CancellationToken ct);


    // usados pelo painel admin
    Task<List<KnowledgeBaseItem>> GetAllAsync(CancellationToken ct);
    Task<KnowledgeBaseItem?> GetByIdAsync(int id, CancellationToken ct);
    Task<KnowledgeBaseItem> SaveAsync(KnowledgeBaseItem item, CancellationToken ct);
    Task ToggleActiveAsync(int id, CancellationToken ct);
}
