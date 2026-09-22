using Portal.AI.Gateway.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/chatkb")]
public class ChatKbAnalyticsController : ControllerBase
{
    private readonly PortalDbContext _db;

    public ChatKbAnalyticsController(PortalDbContext db)
    {
        _db = db;
    }




    // Resumo geral (cards do dashboard)
    [HttpGet("metrics/summary")]
 //   [Authorize]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var last30 = now.AddDays(-30);

        var q = _db.ChatKbRatings.AsQueryable();

        var total = await q.CountAsync(ct);
        var last30Count = await q.Where(x => x.CreatedAt >= last30).CountAsync(ct);
        var avgRating = total > 0 ? await q.AverageAsync(x => (double)x.Rating, ct) : 0.0;

        var lowRatingPct = total > 0
            ? (double)await q.Where(x => x.Rating <= 5).CountAsync(ct) / total * 100.0
            : 0.0;

        return Ok(new
        {
            totalRatings = total,
            ratingsLast30Days = last30Count,
            avgRating = Math.Round(avgRating, 2),
            lowRatingPct = Math.Round(lowRatingPct, 1)
        });
    }

    // Médias por mês (gráfico de linha)
    [HttpGet("metrics/by-month")]
   // [Authorize]
    public async Task<IActionResult> GetByMonth(CancellationToken ct)
    {
        var data = await _db.ChatKbRatings
            .GroupBy(x => new { x.CreatedAt.Year, x.CreatedAt.Month })
            .Select(g => new
            {
                year = g.Key.Year,
                month = g.Key.Month,
                avgRating = g.Average(x => x.Rating),
                count = g.Count()
            })
            .OrderBy(x => x.year).ThenBy(x => x.month)
            .ToListAsync(ct);

        return Ok(data);
    }

    // Médias por seção (gráfico barra)
    [HttpGet("metrics/by-section")]
   // [Authorize]
    public async Task<IActionResult> GetBySection(CancellationToken ct)
    {
        var data = await _db.ChatKbRatings
            .Where(x => x.Section != null)
            .GroupBy(x => x.Section!)
            .Select(g => new
            {
                section = g.Key,
                avgRating = g.Average(x => x.Rating),
                count = g.Count()
            })
            .OrderBy(x => x.section)
            .ToListAsync(ct);

        return Ok(data);
    }

    // Médias por usuário (top problemáticos ou heavy users)
    [HttpGet("metrics/by-user")]
   // [Authorize]
    public async Task<IActionResult> GetByUser(CancellationToken ct)
    {
        var data = await _db.ChatKbRatings
            .GroupBy(x => x.UserId)
            .Select(g => new
            {
                userId = g.Key,
                avgRating = g.Average(x => x.Rating),
                count = g.Count()
            })
            .OrderByDescending(x => x.count)
            .Take(50)
            .ToListAsync(ct);

        return Ok(data);
    }

    // Tabela com filtros (admin auditoria)
    [HttpGet("ratings")]
   // [Authorize]
    public async Task<IActionResult> SearchRatings(
        [FromQuery] int? minRating,
        [FromQuery] int? maxRating,
        [FromQuery] string? userId,
        [FromQuery] string? section,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct = default)
    {
        var q = _db.ChatKbRatings.AsQueryable();

        if (minRating.HasValue)
            q = q.Where(x => x.Rating >= minRating);
        if (maxRating.HasValue)
            q = q.Where(x => x.Rating <= maxRating);
        if (!string.IsNullOrWhiteSpace(userId))
            q = q.Where(x => x.UserId == userId);
        if (!string.IsNullOrWhiteSpace(section))
            q = q.Where(x => x.Section == section);
        if (from.HasValue)
            q = q.Where(x => x.CreatedAt >= from.Value);
        if (to.HasValue)
            q = q.Where(x => x.CreatedAt <= to.Value);

        var data = await q
            .OrderByDescending(x => x.CreatedAt)
            .Take(500)
            .ToListAsync(ct);

        return Ok(data);
    }
}


