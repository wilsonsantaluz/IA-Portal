using AiGateway.Infrastructure.Sse;

namespace AiGateway.Application.Interfaces
{
    public interface ISqlPlannerService
    {
        public Task<SqlPlanResult> PlanAsync(string question, HttpSseEventEmitter sse);
    }
}
