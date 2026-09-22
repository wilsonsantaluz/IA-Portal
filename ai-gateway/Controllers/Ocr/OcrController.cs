using Portal.AI.Gateway.Application.DTOs;
using Portal.AI.Gateway.Application.Interfaces;
using Portal.AI.Gateway.Infrastructure;
using Microsoft.AspNetCore.Mvc;


namespace AiGateway.Controllers.Ocr;

[ApiController]
[Route("api/ocr")]
public class OcrController : ControllerBase
{
    private readonly IOcrService _ocr;
    private readonly IOllamaService _ollama;
    private readonly IChatRepository _chatRepo;
    private readonly ILogger<OcrController> _logger;

    public OcrController(ILogger<OcrController> logger, IOcrService ocr, IOllamaService ollama, IChatRepository chatRepo)
    {
        _ocr = ocr;
        _ollama = ollama;
        _logger = logger;
        _chatRepo = chatRepo;
    }

    [HttpPost("analisar")]
    [RequestSizeLimit(22 * 1024 * 1024)]
    public async Task<ActionResult<OcrAnalyzeResponse>> AnalyzeDocument(
        IFormFile file,
        [FromForm] Guid sessionId,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest("Arquivo não enviado.");
        if (sessionId == Guid.Empty)
            return BadRequest("SessionId válido é obrigatório.");
        if (file.Length > 20 * 1024 * 1024)
            return BadRequest("O limite é de 20 MB por documento.");
        if (!new[] { ".pdf", ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(file.FileName).ToLowerInvariant()))
            return BadRequest("Envie um PDF ou uma imagem PNG/JPEG.");

        using var stream = file.OpenReadStream();
        var lastMessages = await _chatRepo.GetLastMessagesAsync(sessionId, 20, ct);

        var historyText = string.Join("\n", lastMessages.Select(m => $"{m.Role}: {m.Content}"));
        var texto = await _ocr.ExtractTextAsync(stream, file.FileName, file.ContentType, ct);

        if (string.IsNullOrWhiteSpace(texto))
        {
            return Ok(new OcrAnalyzeResponse { Texto = "", Analise = "" });
        }

        var prompt = $@"
Você é um assistente de análise documental e financeira.
HISTÓRICO(últimas mensagens
  {historyText} 

Texto extraído do documento: {texto}
Retorne os dados em forma de tabela
Retorne a analise abaixo da tabela
Analise e entregue:
1. Resumo do documento.
2. Valores financeiros identificados.
3. Datas importantes.
4. Se for contrato: partes envolvidas e objeto.
5. Alertas importantes.

";
        _logger.LogInformation("Documento extraído: {Length} caracteres", texto.Length);
        if (texto  =="") throw new ArgumentException("nehum texto pode ser digitaliazdo do documento");
        var analise = await _ollama.GenerateAsync("deepseek-chat", prompt, ct);
        var session = await _chatRepo.GetOrCreateSessionAsync(sessionId, User.Identity?.Name, ct);
        await _chatRepo.AddMessageAsync(new ChatMessage
        {
            SessionId = sessionId,
            Role = "user",
            Content =texto,
            CreatedAt = DateTime.UtcNow
        }, ct);

        await _chatRepo.AddMessageAsync(new ChatMessage
        {
            SessionId = sessionId,
            Role = "assistant",
            Content = analise,
            CreatedAt = DateTime.UtcNow
        }, ct);

  

        return Ok(new OcrAnalyzeResponse
        {
            Texto = texto,
            Analise = analise
        });
    }
  
}
