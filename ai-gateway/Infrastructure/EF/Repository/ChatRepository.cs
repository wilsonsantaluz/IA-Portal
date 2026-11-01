// Infrastructure/ChatRepository.cs
using Portal.AI.Gateway.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using AiGateway.Security;

namespace Portal.AI.Gateway.Infrastructure;

public class ChatRepository : IChatRepository
{
    private readonly PortalDbContext _ctx;
    private readonly IHttpContextAccessor _http;

    public ChatRepository(PortalDbContext ctx, IHttpContextAccessor http) { _ctx = ctx; _http = http; }

    private string CurrentUser => PortalAccess.UserId(_http.HttpContext!.User).ToString();

    public async Task<ChatSession> GetOrCreateSessionAsync(Guid sessionId, string? userId, CancellationToken ct)
    {
        var session = await _ctx.ChatSessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session is null)
        {
            session = new ChatSession
            {
                Id = sessionId,
                UserId = CurrentUser,
                CreatedAt = DateTime.UtcNow
            };
            _ctx.ChatSessions.Add(session);
            await _ctx.SaveChangesAsync(ct);
        }

        if (session.UserId != CurrentUser) throw new UnauthorizedAccessException("A conversa pertence a outro usuário.");
        return session;
    }

    public async Task<List<ChatMessage>> GetLastMessagesAsync(Guid sessionId, int limit, CancellationToken ct)
    {
        if (!await _ctx.ChatSessions.AnyAsync(x => x.Id == sessionId && x.UserId == CurrentUser, ct)) return [];
        return await _ctx.ChatMessages
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .OrderBy(m => m.CreatedAt) // volta pra ordem cronológica
            .ToListAsync(ct);
    }

    public async Task AddMessageAsync(ChatMessage message, CancellationToken ct)
    {
        _ctx.ChatMessages.Add(message);
        await _ctx.SaveChangesAsync(ct);
    }

}
