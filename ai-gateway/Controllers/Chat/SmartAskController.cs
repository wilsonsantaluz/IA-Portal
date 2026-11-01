using AiGateway.Application.Interfaces;
using AiGateway.Dtos;
using AiGateway.Infrastructure.Sse;
using AiGateway.Infrastructure.Qdrant.Services.LLMs;
using AiGateway.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.AI.Gateway.Application.Interfaces;
using Portal.AI.Gateway.Infrastructure;
using System.Text;
using System.Text.Json;
namespace AiGateway.Controllers.Chat;

[ApiController, Authorize(Policy = PortalAccess.Chat), Route("api/ia")]
public class SmartAskController(IAskSmartService service, IChatRepository chats, ChatUsageService usage, ChatTitleService titles, ILogger<SmartAskController> logger) : ControllerBase
{
    [HttpPost("ask/stream")]
    public async Task AskStream(AskSmartRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Pergunta) || req.Pergunta.Length > 50000 || (req.DocumentContext?.Length ?? 0) > 12000 || !Guid.TryParse(req.SessionId, out var id))
            throw new ChatException(400, "Informe pergunta, conversa válida e contexto dentro dos limites.");
        var requestId = req.RequestId ?? Guid.NewGuid();
        var turn = await chats.BeginTurnAsync(id, requestId, req.Pergunta, ChatRequest.Fingerprint("chat", req.Pergunta, req.DocumentContext), ct);
        Response.ContentType = "text/event-stream; charset=utf-8";
        Response.Headers["Cache-Control"] = "no-cache, no-transform";
        Response.Headers["X-Accel-Buffering"] = "no";
        var sse = new HttpSseEventEmitter(Response);
        try {
            await sse.EmitAsync("message", JsonSerializer.Serialize(new { requestId, userMessageId = turn.UserMessage.Id }));
            ChatMessage answer;
            if (turn.Replayed) { answer = turn.Answer!; await sse.EmitAsync("data", answer.Content); }
            else {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromMinutes(2));
                await sse.EmitAsync("status", "Consultando o conhecimento e preparando a resposta...");
                var prompt = await service.AskAsync(req, sse);
                var result = new StringBuilder();
                await foreach (var part in usage.GenerateStreamAsync(id, turn.UserMessage.Id, "deepseek-chat", prompt, timeout.Token)) { result.Append(part); await sse.EmitAsync("data", part); }
                if (result.Length == 0) throw new InvalidOperationException("Resposta vazia.");
                answer = await chats.CompleteTurnAsync(id, requestId, result.ToString(), timeout.Token, model: usage.ActualModel, provider: usage.ActualProvider);
            }
            await sse.EmitAsync("persisted", JsonSerializer.Serialize(new { id = answer.Id, requestId, status = answer.Status }));
            if (!turn.Replayed) {
                await sse.EmitAsync("status", "Finalizando conversa...");
                await titles.TryGenerateAsync(id, ct);
            }
            await sse.EmitAsync("done", "[*ENDSTREAN*]");
        }
        catch (Exception ex) {
            if (!turn.Replayed) { using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10)); await chats.FailTurnAsync(id, requestId, ex is OperationCanceledException ? "cancelled" : "failed", cleanup.Token); }
            logger.LogWarning("Interação {RequestId} não concluída ({ErrorType}).", requestId, ex.GetType().Name);
            await sse.EmitAsync("error", ex is AiProviderException providerError ? providerError.Message : ex is OperationCanceledException ? "Resposta interrompida ou tempo limite atingido. Sua pergunta foi preservada." : "Não foi possível concluir a resposta. Sua pergunta foi preservada; atualize o histórico.");
        }
    }
}
