using AiGateway.Dtos;
using AiGateway.Infrastructure.Qdrant.Services.LLMs;
using AiGateway.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.AI.Gateway.Application.DTOs;
using Portal.AI.Gateway.Application.Interfaces;
using Portal.AI.Gateway.Infrastructure;
namespace AiGateway.Controllers.Chat;

[ApiController, Authorize(Policy = PortalAccess.Chat), Route("api/chatBot")]
public class ChatBotContoller(IChatRepository chats, ChatUsageService usage, IKnowledgeBaseRepository knowledge, IConfiguration cfg, ChatTitleService titles) : ControllerBase
{
    [HttpGet("sessions")]
    public async Task<IActionResult> List([FromQuery] int limit = 30, [FromQuery] string? cursor = null, [FromQuery] string? search = null, CancellationToken ct = default) => Ok(await chats.ListSessionsAsync(limit, cursor, search, ct));
    [HttpPost("sessions")]
    public async Task<IActionResult> Create(CancellationToken ct) {
        var session = await chats.CreateSessionAsync(ct); return CreatedAtAction(nameof(Get), new { id = session.Id }, session);
    }
    [HttpGet("sessions/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await chats.GetSessionAsync(id, ct));
    [HttpGet("sessions/{id:guid}/messages")]
    public async Task<IActionResult> Messages(Guid id, [FromQuery] int limit = 50, [FromQuery] string? before = null, CancellationToken ct = default) => Ok(await chats.ListMessagesAsync(id, limit, before, ct));
    [HttpPatch("sessions/{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, RenameSessionRequest request, CancellationToken ct) => Ok(await chats.RenameAsync(id, request, ct));
    [HttpPatch("sessions/{id:guid}/model")]
    public async Task<IActionResult> Model(Guid id, ChangeModelRequest request, CancellationToken ct) => Ok(await chats.ChangeModelAsync(id, request, ct));
    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { await chats.DeleteAsync(id, ct); return NoContent(); }
    [HttpGet("history/{sessionId:guid}")]
    public async Task<IActionResult> GetHistory(Guid sessionId, CancellationToken ct) => Ok((await chats.GetLastMessagesAsync(sessionId, 100, ct)).Select(m => new { m.Id, m.Role, m.Content, m.CreatedAt, m.Status }));

    [HttpPost("ask")]
    public async Task<IActionResult> AskSql(AskSqlRequest req, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(req.Question) || req.Question.Length > 50000 || (req.DocumentContext?.Length ?? 0) > 12000 || !Guid.TryParse(req.SessionId, out var id)) throw new ChatException(400, "Pergunta ou conversa inválida.");
        var requestId = req.RequestId ?? Guid.NewGuid();
        var turn = await chats.BeginTurnAsync(id, requestId, req.Question, ChatRequest.Fingerprint("plain", req.Question, req.DocumentContext), ct);
        if (turn.Replayed) return Ok(new { sessionId = id, texto = turn.Answer!.Content, messageId = turn.Answer.Id });
        try {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromMinutes(2));
            var history = await chats.GetLastMessagesAsync(id, 20, ct);
            var context = await knowledge.GetOrganizationBaseAsync(ct);
            var prompt = $"Responda em português com base no conhecimento e histórico.\nBASE:\n{context}\nHISTÓRICO:\n{string.Join("\n", history.Select(m => $"{m.Role}: {m.Content[..Math.Min(m.Content.Length, 4000)]}"))}\nDOCUMENTO:\n{req.DocumentContext}\nPERGUNTA:\n{req.Question}";
            var response = await usage.GenerateAsync(id, turn.UserMessage.Id, "chat", cfg["DEEPSEEK_DEFAULT_MODEL"] ?? "deepseek-chat", prompt, timeout.Token);
            if (string.IsNullOrWhiteSpace(response)) throw new InvalidOperationException();
            var answer = await chats.CompleteTurnAsync(id, requestId, response, timeout.Token, model: usage.ActualModel, provider: usage.ActualProvider);
            await titles.TryGenerateAsync(id, ct);
            return Ok(new { sessionId = id, texto = response, messageId = answer.Id });
        } catch (Exception ex) {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10)); await chats.FailTurnAsync(id, requestId, ct.IsCancellationRequested ? "cancelled" : "failed", cleanup.Token);
            if (ex is AiProviderException) throw;
            throw new ChatException(502, "Não foi possível concluir a resposta. A pergunta foi preservada.");
        }
    }
}
