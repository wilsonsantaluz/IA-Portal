using AiGateway.Dtos;
using AiGateway.Security;
using Microsoft.EntityFrameworkCore;

namespace Portal.AI.Gateway.Infrastructure;

public sealed class UsageQueryService(PortalDbContext db, IHttpContextAccessor http)
{
    private async Task<IQueryable<LlmUsage>> Scope(UsageFilter filter, bool admin, CancellationToken ct)
    {
        var user = http.HttpContext!.User; var owner = PortalAccess.UserId(user);
        if (admin && !user.HasClaim("perm", PortalAccess.UsageAdmin)) throw new ChatException(403, "Acesso negado.");
        if (!admin && filter.UserId is not null) throw new ChatException(400, "Filtro de usuário disponível apenas na consulta administrativa.");
        if (filter.From.Kind != DateTimeKind.Utc || filter.To.Kind != DateTimeKind.Utc || filter.From >= filter.To || filter.To - filter.From > TimeSpan.FromDays(366)
            || (filter.Provider?.Length ?? 0) > 100 || (filter.Model?.Length ?? 0) > 200 || filter.Purpose is not (null or "chat" or "title" or "document") || filter.UserId <= 0)
            throw new ChatException(400, "Informe período UTC de até 366 dias e filtros válidos.");
        // Even usage administrators cannot inspect another owner's conversation by GUID.
        if (filter.SessionId is Guid session && !await db.ChatSessions.AnyAsync(s => s.Id == session && s.OwnerUserId == owner && s.DeletedAt == null, ct)) throw new ChatException(404, "Conversa não encontrada.");
        var query = db.LlmUsages.AsNoTracking().Where(u => u.CreatedAt >= filter.From && u.CreatedAt < filter.To);
        if (!admin) query = query.Where(u => u.OwnerUserId == owner);
        if (filter.UserId is int id) query = query.Where(u => u.OwnerUserId == id);
        if (filter.SessionId is Guid sessionId) query = query.Where(u => u.SessionId == sessionId && u.OwnerUserId == owner);
        if (filter.Provider is not null) query = query.Where(u => u.Provider == filter.Provider);
        if (filter.Model is not null) query = query.Where(u => u.Model == filter.Model);
        if (filter.Purpose is not null) query = query.Where(u => u.Purpose == filter.Purpose);
        return query;
    }
    private static IQueryable<UsageTotals> Totals<TKey>(IQueryable<IGrouping<TKey, LlmUsage>> groups) => groups.Select(g => new UsageTotals {
        LocalApiRequests = g.LongCount(u => u.ApiBilling == "local"), Requests = g.LongCount(), Errors = g.LongCount(u => u.Status != "completed" && u.Status != "started"), Unknown = g.LongCount(u => u.Status == "started"),
        ReportedRequests = g.LongCount(u => u.Source == "reported"), TotalTokenRequests = g.LongCount(u => u.TotalTokens != null), PricedRequests = g.LongCount(u => u.EstimatedCostNanoUsd != null),
        InputTokens = g.LongCount(u => u.InputTokens != null) == 0 ? null : g.Sum(u => u.InputTokens),
        OutputTokens = g.LongCount(u => u.OutputTokens != null) == 0 ? null : g.Sum(u => u.OutputTokens),
        TotalTokens = g.LongCount(u => u.TotalTokens != null) == 0 ? null : g.Sum(u => u.TotalTokens),
        CacheTokens = g.LongCount(u => u.CacheTokens != null) == 0 ? null : g.Sum(u => u.CacheTokens),
        EstimatedCostNanoUsd = g.LongCount(u => u.EstimatedCostNanoUsd != null) == 0 ? null : g.Sum(u => u.EstimatedCostNanoUsd)
    });
    public async Task<UsageTotals> Summary(UsageFilter filter, bool admin, CancellationToken ct) => await Totals((await Scope(filter, admin, ct)).GroupBy(u => 1)).SingleOrDefaultAsync(ct) ?? new();

    public async Task<object> Groups(UsageFilter filter, bool admin, string by, int page, int limit, CancellationToken ct)
    {
        if (by is not ("day" or "month" or "model" or "provider" or "purpose" or "user") || (by == "user" && !admin)) throw new ChatException(400, "Agrupamento inválido ou não autorizado.");
        Page(page, limit);
        var groups = (await Scope(filter, admin, ct)).GroupBy(u => new {
            UserId = by == "user" ? u.OwnerUserId : 0,
            Provider = by == "model" || by == "provider" ? u.Provider : "",
            Model = by == "model" ? u.Model : "", Purpose = by == "purpose" ? u.Purpose : "",
            Year = by == "day" || by == "month" ? u.CreatedAt.Year : 0,
            Month = by == "day" || by == "month" ? u.CreatedAt.Month : 0,
            Day = by == "day" ? u.CreatedAt.Day : 0
        });
        var items = await groups.Select(g => new { key = g.Key, totals = new UsageTotals {
        LocalApiRequests = g.LongCount(u => u.ApiBilling == "local"), Requests = g.LongCount(), Errors = g.LongCount(u => u.Status != "completed" && u.Status != "started"), Unknown = g.LongCount(u => u.Status == "started"),
        ReportedRequests = g.LongCount(u => u.Source == "reported"), TotalTokenRequests = g.LongCount(u => u.TotalTokens != null), PricedRequests = g.LongCount(u => u.EstimatedCostNanoUsd != null),
        InputTokens = g.LongCount(u => u.InputTokens != null) == 0 ? null : g.Sum(u => u.InputTokens),
        OutputTokens = g.LongCount(u => u.OutputTokens != null) == 0 ? null : g.Sum(u => u.OutputTokens),
        TotalTokens = g.LongCount(u => u.TotalTokens != null) == 0 ? null : g.Sum(u => u.TotalTokens),
        CacheTokens = g.LongCount(u => u.CacheTokens != null) == 0 ? null : g.Sum(u => u.CacheTokens),
        EstimatedCostNanoUsd = g.LongCount(u => u.EstimatedCostNanoUsd != null) == 0 ? null : g.Sum(u => u.EstimatedCostNanoUsd)
        } }).OrderBy(x => x.key.UserId).ThenBy(x => x.key.Provider).ThenBy(x => x.key.Model).ThenBy(x => x.key.Purpose)
            .ThenBy(x => x.key.Year).ThenBy(x => x.key.Month).ThenBy(x => x.key.Day).Skip((page - 1) * limit).Take(limit + 1).ToListAsync(ct);
        var more = items.Count > limit; if (more) items.RemoveAt(limit);
        return new { items, nextPage = more ? (int?)(page + 1) : null };
    }

    public async Task<object> Calls(UsageFilter filter, int page, int limit, CancellationToken ct)
    {
        Page(page, limit);
        var rows = await (await Scope(filter, false, ct)).OrderByDescending(u => u.CreatedAt).ThenByDescending(u => u.Id).Skip((page - 1) * limit).Take(limit + 1).ToListAsync(ct);
        var more = rows.Count > limit; if (more) rows.RemoveAt(limit);
        return new { items = rows.Select(u => new UsageCall(u.Id, u.SessionId, u.MessageId, u.Provider, u.Model, u.Purpose, DateTime.SpecifyKind(u.CreatedAt, DateTimeKind.Utc), u.Status, u.Source, u.InputTokens, u.OutputTokens, u.TotalTokens, u.CacheTokens, u.DurationMs, u.EstimatedCostNanoUsd / 1_000_000_000m, u.PriceVersion, u.ApiBilling)), nextPage = more ? (int?)(page + 1) : null };
    }
    private static void Page(int page, int limit) { if (page is < 1 or > 10000 || limit is < 1 or > 100) throw new ChatException(400, "Página ou limite inválido."); }
}
