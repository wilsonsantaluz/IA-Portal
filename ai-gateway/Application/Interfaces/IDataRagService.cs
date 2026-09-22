
namespace Portal.AI.Gateway.Application.Interfaces
{
    public interface IDataRagService
    {
        public Task<string> AnswerFromSqlAsync( SqlPlanResult plan, string sessionId, string pergunta,  string? externalModel, Boolean fullResponse, CancellationToken ct = default);
       
        public Task<(string markdown, string descricao)> QueryAsMarkdownAsync(SqlPlanResult plan, Boolean fullResponse, CancellationToken ct = default);

    }
}
