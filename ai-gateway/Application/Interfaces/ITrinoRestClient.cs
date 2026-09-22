using Portal.AI.Gateway.Infrastructure.Trino;

namespace AiGateway.Application.Interfaces
{
    public interface ITrinoRestClient
    {
        public Task<TrinoResponseData> ExecuteQueryAsync(
      string sql,
      string schema = "financeiro",
      CancellationToken ct = default);
    }
}
