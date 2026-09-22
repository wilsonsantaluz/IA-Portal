using AiGateway.Application.SeedWork;
using Portal.AI.Gateway.Application.DTOs;
using Portal.AI.Gateway.Application.Interfaces;
using Portal.AI.Gateway.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace AiGateway.Controllers.Chat;

[ApiController]
[Route("api/chatBot")]
public class ChatBotContoller : ControllerBase
{
    private readonly IOllamaService _ollama;
    private readonly IChatRepository _chatRepo;
    private readonly IKnowledgeBaseRepository _kbRepo;

    public ChatBotContoller(
        IOllamaService ollama,
        IChatRepository chatRepo,
        IKnowledgeBaseRepository kbRepo)
    {
        _ollama = ollama;
        _chatRepo = chatRepo;
        _kbRepo = kbRepo;
    }

    [HttpPost("ask")]
    public async Task<ActionResult<object>> AskSql([FromBody] AskSqlRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Question))
            return BadRequest("Pergunta vazia.");

        // SessionId vindo do front (mantido em localStorage)
        Guid sessionId = string.IsNullOrWhiteSpace(req.SessionId)
            ? Guid.NewGuid()
            : Guid.Parse(req.SessionId);

        var session = await _chatRepo.GetOrCreateSessionAsync(sessionId, req.UserId, ct);

        // Histórico recente
        var lastMessages = await _chatRepo.GetLastMessagesAsync(sessionId, 20, ct);

        var historyText = string.Join("\n", lastMessages.Select(m => $"{m.Role}: {m.Content}"));

        // Knowledge base do banco (fallback SeedWork se não achar)
        var organizationBase = await _kbRepo.GetOrganizationBaseAsync(ct)
                           ?? KnowledgeBaseSeed.Content;

        var docContext = string.IsNullOrWhiteSpace(req.DocumentContext)
            ? ""
            : $"\n\nContexto do documento atual (OCR / análise):\n{req.DocumentContext}\n";

        var prompt = $@"
Você é um assistente de conhecimento corporativo, consultoria financeira e auditoria de recebíveis.
Use a base de conhecimento abaixo e o histórico da conversa para responder.

BASE DE CONHECIMENTO:
{organizationBase}

{docContext}

HISTÓRICO (últimas mensagens):
{historyText}

Usuário: {req.Question}
IA (responda em Markdown, com tabelas quando fizer sentido):
";

        var resposta = await _ollama.GenerateAsync("deepseek-chat", prompt, ct);

        // Persiste mensagens
        await _chatRepo.AddMessageAsync(new ChatMessage
        {
            SessionId = sessionId,
            Role = "user",
            Content = req.Question,
            CreatedAt = DateTime.UtcNow
        }, ct);

        await _chatRepo.AddMessageAsync(new ChatMessage
        {
            SessionId = sessionId,
            Role = "assistant",
            Content = resposta,
            CreatedAt = DateTime.UtcNow
        }, ct);

        return Ok(new
        {
            sessionId = sessionId.ToString(),
            texto = resposta
        });
    }

    [HttpGet("history/{sessionId:guid}")]
    public async Task<ActionResult<IEnumerable<object>>> GetHistory(Guid sessionId, CancellationToken ct)
    {
        var msgs = await _chatRepo.GetLastMessagesAsync(sessionId, 100, ct);

        var dto = msgs.Select(m => new
        {
            m.Role,
            m.Content,
            m.CreatedAt
        });

        return Ok(dto);
    }
}
