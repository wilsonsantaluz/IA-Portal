// Infrastructure/ChatRepository.cs
using Portal.AI.Gateway.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Portal.AI.Gateway.Infrastructure;

public class ChatRepository : IChatRepository
{
    private readonly PortalDbContext _ctx;

    public ChatRepository(PortalDbContext ctx) => _ctx = ctx;

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
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            _ctx.ChatSessions.Add(session);
            await _ctx.SaveChangesAsync(ct);
        }

        return session;
    }

    public async Task<List<ChatMessage>> GetLastMessagesAsync(Guid sessionId, int limit, CancellationToken ct)
    {
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
