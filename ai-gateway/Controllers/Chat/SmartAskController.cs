using AiGateway.Application.Interfaces;
using AiGateway.Dtos;
using AiGateway.Infrastructure.Sse;
using Azure;
using Portal.AI.Gateway.Application.Interfaces;
using Portal.AI.Gateway.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace AiGateway.Controllers.Chat
{
    [ApiController]
    [Route("api/ia")]
    public class SmartAskController : ControllerBase
    {
        private readonly IAskSmartService _service;
        private readonly IChatRepository _chatRepo;
        private readonly IOllamaService _llm;

        public SmartAskController(IAskSmartService service, IChatRepository chatRepo, IOllamaService llm)
        {
            _service = service;
            _chatRepo = chatRepo;
            _llm = llm;
        }

        [HttpPost("ask/stream")]
        public async Task AskStream([FromBody] AskSmartRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.Pergunta) || !Guid.TryParse(req.SessionId, out _))
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                await Response.WriteAsJsonAsync(new { error = "Pergunta e SessionId válido são obrigatórios." }, ct);
                return;
            }
            Response.StatusCode = 200;
            Response.ContentType = "text/event-stream; charset=utf-8";
            Response.Headers["Cache-Control"] = "no-cache, no-transform";
            Response.Headers["Connection"] = "keep-alive";
            Response.Headers["X-Accel-Buffering"] = "no";
            Response.Headers["Content-Encoding"] = "identity";

            await Response.StartAsync(ct);


            var sse = new HttpSseEventEmitter(Response);

            Guid sessionId = Guid.Empty;
            
            string? resposta = null;

            try
            {
               
                await sse.EmitAsync("status", "  ⏳  Iniciando...\n\n");


                sessionId = string.IsNullOrWhiteSpace(req.SessionId)
                    ? Guid.NewGuid()
                    : Guid.Parse(req.SessionId);

                req.SessionId = sessionId.ToString();

                await sse.EmitAsync("status", "  ⏳  Registrando sessão...\n\n");
                await _chatRepo.GetOrCreateSessionAsync(sessionId, req.UserId, ct);


                await sse.EmitAsync("status", "   ⏳   Montando contexto (planner/RAG)...\n\n");


                var prompt = await _service.AskAsync(req, sse);

                await sse.EmitAsync("status", "   ⏳  LlM respondendo...\n\n");

                Console.WriteLine($"[{DateTime.Now}] Iniciando consulta ao modelo principal tamanho:{prompt.Length}");

                var buffer = new StreamingTextBuffer();
                var respostaFinal = new StringBuilder();
                await buffer.ProcessStreamWithSmartBuffer(
                    _llm.GenerateStreamAsync("deepseek-chat", prompt, ct),
                    sse,
                    respostaFinal);
                Console.WriteLine($"[{DateTime.Now}] Consulta finalizada");
                //SO GRAVAR APOS A CONSULTA ATUAL 
                await _chatRepo.AddMessageAsync(new ChatMessage
                {
                    SessionId = sessionId,
                    Role = "user",
                    Content = req.Pergunta,
                    CreatedAt = DateTime.UtcNow
                }, ct);

                resposta = respostaFinal.ToString();

                await _chatRepo.AddMessageAsync(new ChatMessage
                {
                    SessionId = sessionId,
                    Role = "assistant",
                    Content = resposta,
                    CreatedAt = DateTime.UtcNow
                }, ct);


                await sse.EmitAsync("status", " 💾 **Conversa gravada.\n\n");
            }
            catch (OperationCanceledException)
            {

                await sse.EmitAsync("error", " ⚠️ **Requisição cancelada pelo cliente.\n\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now}]  ERRO: {ex}");


                await sse.EmitAsync("error", $"❌ **Erro:** {ex.Message}\n\n");
            }
            finally
            {

                await sse.EmitAsync("done", "[*ENDSTREAN*]");
                await Response.Body.FlushAsync(ct);
            }
        }
    }




}
