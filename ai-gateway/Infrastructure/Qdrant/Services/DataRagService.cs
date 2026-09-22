using AiGateway.Application.Interfaces;
using Portal.AI.Gateway.Application.Interfaces;

using System.Text;
namespace AiGateway.Infrastructure.Qdrant.Services
{
    public class DataRagService(ITrinoQueryService trino, IOllamaService llm  , IChatRepository chatRepo) : IDataRagService
    {
        private readonly ITrinoQueryService _trino = trino;
        private readonly IOllamaService _llm = llm;
        private readonly IChatRepository _chatRepo = chatRepo;

        /// <summary>
        /// Executa SQL, gera markdown e envia para o LLM externo (DeepSeek).
        /// </summary>
        public async Task<string> AnswerFromSqlAsync(
            SqlPlanResult plan,
            string sessionId,
            string pergunta,
            string? externalModel,
            bool fullResponse,
            CancellationToken ct = default)
        {
         
            // Histórico recente
            var lastMessages = await _chatRepo.GetLastMessagesAsync(Guid.Parse(sessionId), 20, ct);

            var historyText = string.Join("\n", lastMessages.Select(m => $"{m.Role}: {m.Content}"));

            
            if (string.IsNullOrWhiteSpace(plan.Sql))
                return "O planejador SQL não retornou uma query válida.";
            var rows = await _trino.QueryAsync(plan.Sql, "financeiro", ct);
            string markdown = ToMarkdown(rows);
            string desc = plan.Description ?? "Consulta no fonte de dados configurada.";
            var sb = new StringBuilder();            
            sb.AppendLine(markdown);          
            return sb.ToString();
       
        } 

        /// <summary>
        /// Retorna somente o markdown da consulta (sem chamar LLM externo).
        /// </summary>
        public async Task<(string markdown, string descricao)> QueryAsMarkdownAsync(
            SqlPlanResult plan,
            bool fullResponse,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(plan.Sql))
                return ("_(Query vazia gerada pelo planner.)_", plan.Description ?? "");

            var rows = await _trino.QueryAsync(plan.Sql, "financeiro", ct);

            string markdown = ToMarkdown(rows);
            string desc = plan.Description ?? "Consulta no fonte de dados configurada.";

            return (markdown, desc);
        }

        /// <summary>
        /// Converte List<Dictionary<string, object>> em uma tabela Markdown.
        /// </summary>
        public string ToMarkdown(List<Dictionary<string, object>> rows, int maxRows = 50)
        {
            if (rows == null || rows.Count == 0)
                return "_(Nenhum dado encontrado)_";

            var sb = new StringBuilder();

            // Cabeçalhos
            var columns = rows.First().Keys.ToList();
            foreach (var col in columns)
                sb.Append("| ").Append(col);
            sb.AppendLine("|");

            foreach (var _ in columns)
                sb.Append("|---");
            sb.AppendLine("|");

            // Linhas (limitadas)
            int count = Math.Min(rows.Count, maxRows);
            for (int i = 0; i < count; i++)
            {
                var row = rows[i];
                foreach (var col in columns)
                {
                    var value = row[col] ?? "NULL";
                    sb.Append("| ").Append(value.ToString());
                }
                sb.AppendLine("|");
            }

            if (rows.Count > maxRows)
                sb.AppendLine($"\n_(Exibindo {count} de {rows.Count} linhas)_");

            return sb.ToString();
        }
    }
}
