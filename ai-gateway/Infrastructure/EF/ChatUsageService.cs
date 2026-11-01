using AiGateway.Security;
using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Application.Interfaces;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using AiGateway.Infrastructure.Qdrant.Services.LLMs;

namespace Portal.AI.Gateway.Infrastructure;

public sealed class ChatUsageService(PortalDbContext db, IHttpContextAccessor http, IOllamaService provider,
    ILlmMetrics metrics, ModelPriceCatalog prices, ILogger<ChatUsageService> logger, AiProviderRouter? router = null)
{
    public string ActualProvider { get; private set; } = "deepseek";
    public string ActualModel { get; private set; } = "deepseek-chat";
    private async Task<LlmUsage> Start(Guid session, long message, string purpose, string model, CancellationToken ct)
    {
        var owner = PortalAccess.UserId(http.HttpContext!.User);
        if (purpose is not ("chat" or "document")) throw new ArgumentException("Finalidade inválida.");
        if (!await db.ChatMessages.AnyAsync(m => m.Id == message && m.SessionId == session && m.Role == "user" && m.Status == "pending" && m.Session.OwnerUserId == owner && m.Session.DeletedAt == null, ct)) throw new ChatException(404, "Conversa não encontrada.");
        if (router is not null) {
            var question = await db.ChatMessages.AsNoTracking().SingleAsync(m => m.Id == message && m.SessionId == session, ct);
            await router.Select(question.Provider ?? "deepseek", question.Model ?? model, ct);
            model = router.Model;
        }
        var usage = new LlmUsage { Id = Guid.NewGuid(), OwnerUserId = owner, SessionId = session, MessageId = message,
            Purpose = purpose, Provider = router?.ProviderId ?? "deepseek", Model = model, ApiBilling = router?.ApiBilling ?? "external", CreatedAt = DateTime.UtcNow };
        db.LlmUsages.Add(usage); await db.SaveChangesAsync(ct); return usage;
    }

    private async Task Finish(LlmUsage usage, Stopwatch clock, bool completed, CancellationToken ct)
    {
        var result = metrics.LastCall;
        usage.Model = string.IsNullOrWhiteSpace(result.Model) ? usage.Model : result.Model;
        ActualProvider = usage.Provider; ActualModel = usage.Model;
        usage.InputTokens = result.InputTokens; usage.OutputTokens = result.OutputTokens;
        usage.TotalTokens = result.TotalTokens; usage.CacheTokens = result.CacheTokens; usage.Source = result.Source;
        usage.Status = completed ? "completed" : result.Status == "invalid_response" ? "invalid_response" :
            ct.IsCancellationRequested ? http.HttpContext!.RequestAborted.IsCancellationRequested ? "cancelled" : "timeout" : "failed";
        usage.DurationMs = clock.ElapsedMilliseconds;
        prices.Apply(usage);
        try {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            db.LlmUsages.Update(usage); await db.SaveChangesAsync(cleanup.Token);
        } catch (Exception ex) { logger.LogWarning("Consumo {CallId} permanece com resultado desconhecido ({ErrorType}).", usage.Id, ex.GetType().Name); db.Entry(usage).State = EntityState.Detached; }
    }

    public async Task<string> GenerateAsync(Guid session, long message, string purpose, string model, string prompt, CancellationToken ct)
    {
        var usage = await Start(session, message, purpose, model, ct); var clock = Stopwatch.StartNew(); var completed = false;
        try { var answer = await provider.GenerateAsync(model, prompt, ct); if (string.IsNullOrWhiteSpace(answer)) throw new InvalidOperationException("Resposta vazia."); completed = true; return answer; }
        finally { await Finish(usage, clock, completed, ct); }
    }

    public async IAsyncEnumerable<string> GenerateStreamAsync(Guid session, long message, string model, string prompt, [EnumeratorCancellation] CancellationToken ct)
    {
        var usage = await Start(session, message, "chat", model, ct); var clock = Stopwatch.StartNew(); var completed = false; var hasContent = false;
        try {
            await foreach (var part in provider.GenerateStreamAsync(model, prompt, ct)) { hasContent |= !string.IsNullOrWhiteSpace(part); yield return part; }
            if (!hasContent) throw new InvalidOperationException("Resposta vazia.");
            completed = true;
        } finally { await Finish(usage, clock, completed, ct); }
    }
}
