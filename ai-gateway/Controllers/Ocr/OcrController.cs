using AiGateway.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.AI.Gateway.Application.DTOs;
using Portal.AI.Gateway.Application.Interfaces;
using Portal.AI.Gateway.Infrastructure;
using System.Security.Cryptography;
namespace AiGateway.Controllers.Ocr;

[ApiController, Authorize(Policy = PortalAccess.Chat), Route("api/ocr")]
public class OcrController(IOcrService ocr, ChatUsageService usage, IChatRepository chats, IConfiguration cfg, ChatTitleService titles) : ControllerBase
{
    [HttpPost("analisar"), RequestSizeLimit(22 * 1024 * 1024)]
    public async Task<ActionResult<OcrAnalyzeResponse>> AnalyzeDocument(IFormFile file, [FromForm] Guid sessionId, [FromForm] Guid? requestId, CancellationToken ct)
    {
        // Authorization precedes file processing and external calls, including on replays.
        await chats.GetSessionAsync(sessionId, ct);
        if (file is null || file.Length == 0 || file.Length > 20 * 1024 * 1024 || !new[] { ".pdf", ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
            throw new ChatException(400, "Envie PDF, PNG ou JPEG de até 20 MB.");
        var id = requestId ?? Guid.NewGuid();
        using var hashStream = file.OpenReadStream();
        var fingerprint = Convert.ToHexString(await SHA256.HashDataAsync(hashStream, ct));
        var turn = await chats.BeginTurnAsync(sessionId, id, $"Documento enviado: {Path.GetFileName(file.FileName)}", ChatRequest.Fingerprint("document", fingerprint), ct);
        if (turn.Replayed) return Ok(new OcrAnalyzeResponse { Texto = turn.UserMessage.Content, Analise = turn.Answer!.Content });
        try {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromMinutes(2));
            using var stream = file.OpenReadStream();
            var text = await ocr.ExtractTextAsync(stream, file.FileName, file.ContentType, timeout.Token);
            if (string.IsNullOrWhiteSpace(text)) throw new ChatException(422, "Não foi possível extrair texto do documento.");
            text = text[..Math.Min(text.Length, 50000)];
            var history = await chats.GetLastMessagesAsync(sessionId, 20, ct);
            var prompt = $"Analise o documento em português. Resuma os pontos principais, datas e valores presentes. Informe limitações.\nHISTÓRICO:\n{string.Join("\n", history.Select(m => $"{m.Role}: {m.Content[..Math.Min(m.Content.Length, 4000)]}"))}\nDOCUMENTO (até 50.000 caracteres):\n{text}";
            var answer = await usage.GenerateAsync(sessionId, turn.UserMessage.Id, "document", cfg["DEEPSEEK_DEFAULT_MODEL"] ?? "deepseek-chat", prompt, timeout.Token);
            if (string.IsNullOrWhiteSpace(answer)) throw new InvalidOperationException();
            await chats.CompleteTurnAsync(sessionId, id, answer, timeout.Token, text, cfg["DEEPSEEK_DEFAULT_MODEL"] ?? "deepseek-chat");
            await titles.TryGenerateAsync(sessionId, ct);
            return Ok(new OcrAnalyzeResponse { Texto = text, Analise = answer });
        } catch (Exception ex) {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10)); await chats.FailTurnAsync(sessionId, id, ct.IsCancellationRequested ? "cancelled" : "failed", cleanup.Token);
            if (ex is ChatException) throw;
            throw new ChatException(502, "Não foi possível concluir a análise. A interação foi preservada.");
        }
    }
}
