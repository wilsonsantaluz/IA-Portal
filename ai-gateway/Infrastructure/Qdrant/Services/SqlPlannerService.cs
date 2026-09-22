using AiGateway.Application.Interfaces;
using AiGateway.Infrastructure.Sse;
using Portal.AI.Gateway.Application.Interfaces;
using System;
using System.Text;
using System.Text.Json;
using Tokenizers.HuggingFace.Processors;

namespace AiGateway.Infrastructure.Qdrant.Services
{
    public class SqlPlannerService : ISqlPlannerService
    {
        private readonly IOllamaService _llm;

        public SqlPlannerService(IOllamaService llm)
        {
            _llm = llm;
        }

        public async Task<SqlPlanResult> PlanAsync(string question, HttpSseEventEmitter sse)
        {
            var systemPath = "./ragbase/planservices.txt";            
            var systemMessage = File.ReadAllText(systemPath);
            var SqlPlannerPrompt =  systemMessage;

            string prompt = SqlPlannerPrompt.Replace("{QUESTION}", question);
            await sse.EmitAsync("status", "Consultando modelo para determinar tipo da consulta");
            Console.WriteLine($"[{DateTime.Now}] EXECUTANDO CONSULTA AO MODELO PLANER tamanho - {prompt.Length}");
            var respostaFinal = new StringBuilder();


        
            var buffer = new StreamingTextBuffer();
            var ct = new CancellationToken();
            await buffer.ProcessStreamWithSmartBuffer(
                 _llm.GenerateStreamAsync("deepseek-chat", prompt, ct),
                 sse,
                 respostaFinal);

            Console.WriteLine($"[{DateTime.Now}] Cunsulta ao planer finalizada");
            string json = ExtractJson(respostaFinal.ToString());

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                return new SqlPlanResult
                {
                    Type = root.GetPropertyOrDefault("type", "unknown"),
                    Topic = root.GetPropertyOrDefault("topic", null),
                    Sql = root.GetPropertyOrDefault("query", null),
                    Description = root.GetPropertyOrDefault("description", null)
                };
            }
            catch (Exception ex)
            {
                return new SqlPlanResult
                {
                    Type = "error",
                    Error = $"Erro ao interpretar JSON: {ex.Message}\nRAW:\n{respostaFinal}"
                };
            }
        }

        private static string ExtractJson(string text)
        {
            text = text.Trim();

            int s = text.IndexOf('{');
            int e = text.LastIndexOf('}');

            if (s < 0 || e < 0 || e <= s)
                return "{}";

            return text.Substring(s, e - s + 1);
        }
    }
  

    public static class JsonExtensions
    {
        public static string? GetPropertyOrDefault(this JsonElement element, string name, string? defaultValue)
        {
            return element.TryGetProperty(name, out var v) ? v.ToString() : defaultValue;
        }
    }
}
