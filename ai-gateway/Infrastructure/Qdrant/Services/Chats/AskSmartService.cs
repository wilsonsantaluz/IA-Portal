using AiGateway.Application.Interfaces;
using AiGateway.Dtos;
using AiGateway.Infrastructure.Sse;
using Portal.AI.Gateway.Application.Interfaces;
using System.Text;
namespace AiGateway.Infrastructure.Qdrant.Services.Chats
{
    public class AskSmartService(
        ISqlPlannerService planner,
        IDataRagService dataRag,
        IKnowledgeBaseRagService kbRag,
        IChatRepository chatRepo,
        IOllamaService llm) : IAskSmartService
    {
        private readonly ISqlPlannerService _planner = planner;
        private readonly IDataRagService _dataRag = dataRag;
        private readonly IKnowledgeBaseRagService _kbRag = kbRag;
        private readonly IOllamaService _llm = llm;
        private readonly IChatRepository _chatRepo = chatRepo;

        public async Task<string> AskAsync(AskSmartRequest req, HttpSseEventEmitter sse)

        {
            var prompt = new StringBuilder();
           
            var plan = new SqlPlanResult();

            await sse.EmitAsync("status", "  ⏳ Iniciando smart rag pergunta \n\n");

            plan = await _planner.PlanAsync(req.Pergunta, sse);

            Console.WriteLine($"[{DateTime.Now}] Avaliação do tipo de pergunta realizada {plan.Type}");
            await sse.EmitAsync("status", "  ⏳ Avaliação do tipo de pergunta realizada {plan.Type} \n\n");

            await sse.EmitAsync("status", "  ⏳ Buscando historico de mensagens \n\n");
            var lastMessages =
                await _chatRepo.GetLastMessagesAsync(Guid.Parse(req.SessionId), 20, new CancellationToken());

            var historyText = string.Join(
                "\n",
                lastMessages.Select(m => $"{m.Role}: {m.Content}")
            );

            var docContext = string.IsNullOrWhiteSpace(req.DocumentContext)
                ? ""
                : $"""
           Contexto do documento atual (OCR / análise):
           {req.DocumentContext}
           """;          
            switch (plan.Type)
            {

                case "kb":
                    await sse.EmitAsync("status", "  ⏳ Buscando contexto no banco vetorial portal \n\n");
                    Console.WriteLine($"[{DateTime.Now}] Modo base de conhecimento");
                    var kbOnly = await _kbRag.AskAsync(new AskKbRagRequest
                    {
                        Prompt = req.Pergunta,
                        SessionId = req.SessionId
                    },
                      false, new CancellationToken());
                    if (kbOnly.texto == "") {
                        throw new Exception("base de conhecimento nao retorno valores");
                    }
                    prompt.AppendLine("### BASE DE CONHECIMENTO");
                    prompt.AppendLine(kbOnly.texto);
                    break;

                case "sql":
                    await sse.EmitAsync("status", "  ⏳ Realizando consultas ao data lake portal...\n\n");
                    Console.WriteLine($"[{DateTime.Now}] Modo de consulta sql");
                    var kbData = await _dataRag.AnswerFromSqlAsync(
                        plan,
                        req.SessionId,
                        req.Pergunta,
                        "",
                        false,
                        new CancellationToken()
                    );
                    prompt.AppendLine("### DADOS");
                    prompt.AppendLine(kbData);
                    break;

                case "hybrid":
                    Console.WriteLine($"[{DateTime.Now}] Modo hybrid");
                    await sse.EmitAsync("status", "   ⏳ Buscando contexto no banco vetorial portal ..");
                    var kbResp = await _kbRag.AskAsync(
                        new AskKbRagRequest
                        {
                            Prompt = plan.Topic ?? req.Pergunta
                        },
                        false,
                        new CancellationToken()
                    );
                    Console.WriteLine($"[{DateTime.Now}] Consultando datalake");
                    await sse.EmitAsync("status", "   ⏳ Realizando consultas ao data lake portal...");
                    var (markdown, desc) =
                        await _dataRag.QueryAsMarkdownAsync(plan, false, new CancellationToken());
                    prompt.AppendLine("### CONTEXTO CONCEITUAL");
                    prompt.AppendLine(kbResp.texto);
                    prompt.AppendLine();
                    if (!string.IsNullOrWhiteSpace(docContext))
                    {
                        prompt.AppendLine("### CONTEXTO DO DOCUMENTO");
                        prompt.AppendLine(docContext);
                        prompt.AppendLine();
                    }
                    prompt.AppendLine("### DADOS");
                    prompt.AppendLine(desc);
                    prompt.AppendLine();
                    prompt.AppendLine(markdown);
                    break;
                case "unknown":

                    break;

                default:
                    Console.WriteLine($"[{DateTime.Now}] Fallback base de conhecimento");

                    var fallback = await _kbRag.AskAsync(
                        new AskKbRagRequest
                        {
                            Prompt = req.Pergunta,
                            SessionId = req.SessionId
                        },
                        false,
                        new CancellationToken()
                    );

                    prompt.AppendLine("### BASE DE CONHECIMENTO");
                    prompt.AppendLine(fallback.texto);
                    break;
            }
            var master = new StringBuilder();
            var BasePrompt = "";
            var systemPath = "./ragbase/ragbaseinput.txt";
            var systemMessage = File.ReadAllText(systemPath);
            BasePrompt = systemMessage;
            master.AppendLine("### HISTÓRICO DA CONVERSA");
            master.AppendLine(historyText);
            master.AppendLine();
            master.AppendLine("### CONTEXTO CONSOLIDADO");
            master.Append(prompt);
            master.AppendLine();
            master.AppendLine(BasePrompt);
            master.AppendLine();
            master.AppendLine("### PERGUNTA");
            master.AppendLine(req.Pergunta);
            master.AppendLine();
            master.AppendLine("### INSTRUÇÕES FINAIS DE RESPOSTA");
            master.AppendLine("""
    - Responda de forma clara e objetiva
    - Use tabelas Markdown quando aplicável
    - Não invente dados
    - Se faltar informação, deixe explícito
    """);

            return master.ToString();
        }


    }
}
