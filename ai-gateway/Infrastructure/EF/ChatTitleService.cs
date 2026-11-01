using AiGateway.Security;
using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Application.Interfaces;
using System.Diagnostics;
using System.Globalization;

namespace Portal.AI.Gateway.Infrastructure;

public sealed class ChatTitleService(PortalDbContext db, IHttpContextAccessor http,
    IChatTitleGenerator generator, ILogger<ChatTitleService> logger)
{
    // Bound by text elements so truncation does not split surrogate pairs/combining marks.
    public static string Shorten(string text, int max)
    {
        var clean = string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim('"', '\'', '`', '“', '”');
        var positions = StringInfo.ParseCombiningCharacters(clean);
        return positions.Length > max ? clean[..positions[max]] : clean;
    }

    public async Task TryGenerateAsync(Guid sessionId, CancellationToken ct)
    {
        try { await GenerateAsync(sessionId, ct); }
        catch (Exception ex) { logger.LogWarning("Título não finalizado para {SessionId} ({ErrorType}); resposta do chat preservada.", sessionId, ex.GetType().Name); }
    }

    private async Task GenerateAsync(Guid sessionId, CancellationToken ct)
    {
        var owner = PortalAccess.UserId(http.HttpContext!.User);
        var owned = db.ChatSessions.Where(s => s.Id == sessionId && s.OwnerUserId == owner && s.DeletedAt == null);
        LlmUsage attempt;
        string subject;
        // The fallback and durable attempt claim are atomic. A retry/restart never repeats a paid title call.
        await using (var tx = await db.Database.BeginTransactionAsync(ct)) {
            var session = await owned.AsNoTracking().SingleOrDefaultAsync(ct);
            if (session is null || session.TitleSource != "provisional" || await db.LlmUsages.AnyAsync(u => u.SessionId == sessionId && u.Purpose == "title", ct)) return;
            var first = await db.ChatMessages.AsNoTracking().Where(m => m.SessionId == sessionId && m.Role == "assistant" && m.Status == "completed").OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).FirstOrDefaultAsync(ct);
            if (first is null || first.Provider != "deepseek" || first.RequestId is null) return;
            var question = await db.ChatMessages.AsNoTracking().SingleAsync(m => m.SessionId == sessionId && m.RequestId == first.RequestId && m.Role == "user", ct);
            subject = Shorten(question.Content, 1000);
            var fallback = Shorten(question.Content, 80);
            if (string.IsNullOrWhiteSpace(fallback)) fallback = "Nova conversa";
            var claimed = await owned.Where(s => s.TitleSource == "provisional" && s.Version == session.Version)
                .ExecuteUpdateAsync(u => u.SetProperty(s => s.Title, fallback).SetProperty(s => s.TitleSource, "fallback").SetProperty(s => s.Version, s => s.Version + 1), ct);
            if (claimed == 0) return;
            attempt = new() { Id = Guid.NewGuid(), OwnerUserId = owner, SessionId = sessionId, MessageId = first.Id,
                Provider = first.Provider, Model = first.Model ?? "deepseek-chat", CreatedAt = DateTime.UtcNow };
            db.LlmUsages.Add(attempt); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        }
        var clock = Stopwatch.StartNew();
        string? title = null;
        try {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var result = await generator.GenerateTitleAsync(attempt.Model, subject, timeout.Token);
            attempt.Model = result.Model;
            attempt.InputTokens = result.InputTokens; attempt.OutputTokens = result.OutputTokens;
            attempt.TotalTokens = result.TotalTokens; attempt.CacheTokens = result.CacheTokens;
            attempt.Source = result.InputTokens.HasValue || result.OutputTokens.HasValue || result.TotalTokens.HasValue || result.CacheTokens.HasValue ? "reported" : "unavailable";
            // Explanations/multiline output are rejected; fallback remains. Tokens still belong to this call.
            if (!string.IsNullOrWhiteSpace(result.Content) && !result.Content.Trim().Contains('\n') && !result.Content.Trim().Contains('\r')) title = Shorten(result.Content, 80);
            attempt.Status = string.IsNullOrWhiteSpace(title) ? "invalid_response" : "completed";
        }
        catch (OperationCanceledException) { attempt.Status = ct.IsCancellationRequested ? "cancelled" : "timeout"; }
        catch { attempt.Status = "failed"; }
        attempt.DurationMs = clock.ElapsedMilliseconds;
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var final = await db.Database.BeginTransactionAsync(cleanup.Token);
        db.LlmUsages.Update(attempt);
        await db.SaveChangesAsync(cleanup.Token);
        if (!string.IsNullOrWhiteSpace(title)) await owned.Where(s => s.TitleSource == "fallback")
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.Title, title).SetProperty(s => s.TitleSource, "automatic").SetProperty(s => s.Version, s => s.Version + 1), cleanup.Token);
        await final.CommitAsync(cleanup.Token);
    }
}
