using AiGateway.Dtos;
using AiGateway.Security;
using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Application.Interfaces;
using System.Text.Json;
namespace Portal.AI.Gateway.Infrastructure;

public class ChatException(int status, string message) : Exception(message) { public int Status { get; } = status; }
public class ChatRepository(PortalDbContext db, IHttpContextAccessor http, AiProviderCatalog? catalog = null) : IChatRepository
{
    private int Owner => PortalAccess.UserId(http.HttpContext!.User);
    private IQueryable<ChatSession> Owned => db.ChatSessions.Where(s => s.OwnerUserId == Owner && s.DeletedAt == null);
    private async Task<ChatSession> Session(Guid id, CancellationToken ct) => await Owned.FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new ChatException(404, "Conversa não encontrada.");
    private static SessionDto Dto(ChatSession s) => new(s.Id, s.Title ?? "Nova conversa", s.TitleSource, DateTime.SpecifyKind(s.CreatedAt, DateTimeKind.Utc), DateTime.SpecifyKind(s.UpdatedAt, DateTimeKind.Utc), s.Provider, s.Model, s.Version);
    private static MessageDto Dto(ChatMessage m) => new(m.Id, m.SessionId, m.RequestId, m.Role, m.Content, DateTime.SpecifyKind(m.CreatedAt, DateTimeKind.Utc), m.Status, m.Provider, m.Model);
    private record Cursor(DateTime At, Guid Id, string Search, int Owner);
    public async Task<SessionDto> CreateSessionAsync(CancellationToken ct) {
        var selection = catalog is null ? new ModelSelection("deepseek", "deepseek-chat") : await catalog.Default(Owner, ct);
        var s = new ChatSession { Id = Guid.NewGuid(), OwnerUserId = Owner, UserId = Owner.ToString(), Title = "Nova conversa", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, Provider = "deepseek", Model = "deepseek-chat" };
        s.Provider = selection.Provider; s.Model = selection.Model;
        db.ChatSessions.Add(s); await db.SaveChangesAsync(ct); return Dto(s);
    }
    public async Task<SessionDto> ChangeModelAsync(Guid id, ChangeModelRequest request, CancellationToken ct) {
        await Session(id, ct);
        if (catalog is null) throw new ChatException(409, "Seleção de modelos indisponível.");
        await catalog.Resolve(request.Provider, request.Model, ct);
        var count = await Owned.Where(s => s.Id == id && s.Version == request.Version && (s.ActiveRequestId == null || s.LeaseUntil < DateTime.UtcNow))
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.Provider, request.Provider).SetProperty(s => s.Model, request.Model).SetProperty(s => s.Version, s => s.Version + 1), ct);
        if (count == 0) throw new ChatException(409, "Conversa alterada ou resposta em andamento; atualize antes de trocar o modelo.");
        db.ChangeTracker.Clear(); return await GetSessionAsync(id, ct);
    }
    public async Task<SessionDto> GetSessionAsync(Guid id, CancellationToken ct) => Dto(await Owned.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new ChatException(404, "Conversa não encontrada."));
    public async Task<SessionPage> ListSessionsAsync(int limit, string? cursor, string? search, CancellationToken ct) {
        if (limit is < 1 or > 100 || (search?.Length ?? 0) > 120) throw new ChatException(400, "Filtro ou limite inválido.");
        search = search?.Trim() ?? "";
        IQueryable<ChatSession> query = db.ChatSessions;
        if (cursor is not null) {
            Cursor c;
            try { if (cursor.Length > 2000) throw new FormatException(); c = JsonSerializer.Deserialize<Cursor>(Convert.FromBase64String(cursor))!; if (c is null || c.Owner != Owner || c.Search != search) throw new FormatException(); }
            catch { throw new ChatException(400, "Cursor inválido."); }
            query = db.ChatSessions.FromSqlInterpolated($"SELECT * FROM ChatSession WHERE UpdatedAt < {c.At} OR (UpdatedAt = {c.At} AND Id < {c.Id})");
        }
        var items = await query.AsNoTracking().Where(s => s.OwnerUserId == Owner && s.DeletedAt == null && (search == "" || (s.Title ?? "").Contains(search)))
            .OrderByDescending(s => s.UpdatedAt).ThenByDescending(s => s.Id).Take(limit + 1).ToListAsync(ct);
        var more = items.Count > limit; if (more) items.RemoveAt(limit);
        var last = items.LastOrDefault();
        return new(items.Select(Dto).ToList(), more && last is not null ? Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new Cursor(last.UpdatedAt, last.Id, search, Owner))) : null);
    }
    public async Task<MessagePage> ListMessagesAsync(Guid id, int limit, string? before, CancellationToken ct) {
        await Session(id, ct);
        if (limit is < 1 or > 100 || (before is not null && (!long.TryParse(before, out var value) || value <= 0))) throw new ChatException(400, "Limite ou cursor inválido.");
        var query = db.ChatMessages.AsNoTracking().Where(m => m.SessionId == id && m.Session.OwnerUserId == Owner && m.Session.DeletedAt == null);
        if (before is not null) {
            var anchorId = long.Parse(before);
            var anchor = await query.FirstOrDefaultAsync(m => m.Id == anchorId, ct) ?? throw new ChatException(400, "Cursor inválido.");
            query = query.Where(m => m.CreatedAt < anchor.CreatedAt || (m.CreatedAt == anchor.CreatedAt && m.Id < anchor.Id));
        }
        var items = await query.OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Take(limit + 1).ToListAsync(ct);
        var more = items.Count > limit; if (more) items.RemoveAt(limit);
        items.Reverse(); return new(items.Select(Dto).ToList(), more ? items[0].Id.ToString() : null);
    }
    public async Task<SessionDto> RenameAsync(Guid id, RenameSessionRequest request, CancellationToken ct) {
        var title = request.Title.Trim(); if (title.Length is < 1 or > 120) throw new ChatException(400, "Informe um título de até 120 caracteres.");
        var count = await Owned.Where(s => s.Id == id && s.Version == request.Version).ExecuteUpdateAsync(u => u.SetProperty(s => s.Title, title).SetProperty(s => s.TitleSource, "manual").SetProperty(s => s.UpdatedAt, DateTime.UtcNow).SetProperty(s => s.Version, s => s.Version + 1), ct);
        if (count == 0) { await Session(id, ct); throw new ChatException(409, "Conversa alterada. Atualize a lista e tente novamente."); }
        db.ChangeTracker.Clear(); return await GetSessionAsync(id, ct);
    }
    public async Task DeleteAsync(Guid id, CancellationToken ct) {
        var count = await Owned.Where(s => s.Id == id).ExecuteUpdateAsync(u => u.SetProperty(s => s.DeletedAt, DateTime.UtcNow).SetProperty(s => s.Version, s => s.Version + 1), ct);
        if (count == 0) throw new ChatException(404, "Conversa não encontrada.");
    }
    public async Task<TurnReservation> BeginTurnAsync(Guid id, Guid requestId, string content, string fingerprint, CancellationToken ct) {
        if (requestId == Guid.Empty || content.Length is < 1 or > 50000) throw new ChatException(400, "Mensagem ou identificador inválido.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var s = await Session(id, ct);
        var prior = await db.ChatMessages.FirstOrDefaultAsync(m => m.SessionId == id && m.RequestId == requestId && m.Role == "user", ct);
        if (prior is not null) {
            if (prior.RequestFingerprint != fingerprint) throw new ChatException(409, "Identificador já utilizado para outra mensagem.");
            if (prior.Status != "completed") throw new ChatException(409, "Interação já registrada. Atualize o histórico; um novo envio precisa de outro identificador.");
            var answer = await db.ChatMessages.SingleAsync(m => m.SessionId == id && m.RequestId == requestId && m.Role == "assistant", ct);
            return new(prior, answer, true);
        }
        if (catalog is not null) await catalog.Resolve(s.Provider ?? "deepseek", s.Model ?? "deepseek-chat", ct);
        if (s.ActiveRequestId is not null && s.LeaseUntil > DateTime.UtcNow) throw new ChatException(409, "Aguarde a resposta em andamento nesta conversa.");
        if (s.ActiveRequestId is not null) {
            var expired = await db.ChatMessages.Where(m => m.SessionId == id && m.RequestId == s.ActiveRequestId && m.Status == "pending").ToListAsync(ct);
            foreach (var message in expired) message.Status = "interrupted";
        }
        s.ActiveRequestId = requestId; s.LeaseUntil = DateTime.UtcNow.AddMinutes(3); s.UpdatedAt = DateTime.UtcNow; s.Version++;
        var question = new ChatMessage { SessionId = id, RequestId = requestId, Role = "user", Content = content, Status = "pending", RequestFingerprint = fingerprint, CreatedAt = DateTime.UtcNow };
        question.Provider = s.Provider; question.Model = s.Model;
        db.ChatMessages.Add(question); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new(question, null, false);
    }
    public async Task<ChatMessage> CompleteTurnAsync(Guid id, Guid requestId, string content, CancellationToken ct, string? userContent = null, string? model = null, string? provider = null) {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.ChangeTracker.Clear(); var s = await Session(id, ct);
        if (s.ActiveRequestId != requestId || s.LeaseUntil < DateTime.UtcNow) throw new ChatException(409, "Esta interação foi interrompida.");
        var question = await db.ChatMessages.SingleAsync(m => m.SessionId == id && m.RequestId == requestId && m.Role == "user", ct);
        question.Status = "completed"; if (userContent is not null) question.Content = userContent;
        var answer = new ChatMessage { SessionId = id, RequestId = requestId, Role = "assistant", Content = content, Status = "completed", Provider = "deepseek", Model = model ?? "deepseek-chat", CreatedAt = DateTime.UtcNow };
        answer.Provider = provider ?? s.Provider ?? "deepseek"; answer.Model = model ?? s.Model ?? "deepseek-chat";
        db.ChatMessages.Add(answer); s.ActiveRequestId = null; s.LeaseUntil = null; s.UpdatedAt = DateTime.UtcNow; s.Version++;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return answer;
    }
    public async Task FailTurnAsync(Guid id, Guid requestId, string status, CancellationToken ct) {
        db.ChangeTracker.Clear(); await using var tx = await db.Database.BeginTransactionAsync(ct);
        var s = await Owned.FirstOrDefaultAsync(s => s.Id == id && s.ActiveRequestId == requestId, ct);
        if (s is null) return;
        await db.ChatMessages.Where(m => m.SessionId == id && m.RequestId == requestId && m.Status == "pending").ExecuteUpdateAsync(u => u.SetProperty(m => m.Status, status), ct);
        s.ActiveRequestId = null; s.LeaseUntil = null; s.UpdatedAt = DateTime.UtcNow; s.Version++;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    // Compatibility methods never create or adopt a caller-supplied UUID.
    public Task<ChatSession> GetOrCreateSessionAsync(Guid id, string? userId, CancellationToken ct) => Session(id, ct);
    public async Task<List<ChatMessage>> GetLastMessagesAsync(Guid id, int limit, CancellationToken ct) {
        await Session(id, ct);
        var items = await db.ChatMessages.AsNoTracking().Where(m => m.SessionId == id && m.Session.OwnerUserId == Owner && m.Session.DeletedAt == null && m.Status == "completed").OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id).Take(Math.Clamp(limit, 1, 100)).ToListAsync(ct);
        items.Reverse(); return items;
    }
    public async Task AddMessageAsync(ChatMessage message, CancellationToken ct) {
        await using var tx = await db.Database.BeginTransactionAsync(ct); var s = await Session(message.SessionId, ct);
        db.ChatMessages.Add(message); s.UpdatedAt = DateTime.UtcNow; s.Version++; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
}
