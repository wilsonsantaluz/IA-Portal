// Application/Interfaces/IChatRepository.cs
using Portal.AI.Gateway.Infrastructure;
using AiGateway.Dtos;

namespace Portal.AI.Gateway.Application.Interfaces;

public interface IChatRepository
{
    Task<SessionDto> CreateSessionAsync(CancellationToken ct);
    Task<SessionDto> GetSessionAsync(Guid id, CancellationToken ct);
    Task<SessionPage> ListSessionsAsync(int limit, string? cursor, string? search, CancellationToken ct);
    Task<MessagePage> ListMessagesAsync(Guid id, int limit, string? before, CancellationToken ct);
    Task<SessionDto> RenameAsync(Guid id, RenameSessionRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<TurnReservation> BeginTurnAsync(Guid id, Guid requestId, string content, string fingerprint, CancellationToken ct);
    Task<ChatMessage> CompleteTurnAsync(Guid id, Guid requestId, string content, CancellationToken ct, string? userContent = null, string? model = null);
    Task FailTurnAsync(Guid id, Guid requestId, string status, CancellationToken ct);
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
