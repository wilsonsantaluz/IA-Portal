using Portal.AI.Gateway.Application.Interfaces;
using Portal.AI.Gateway.Infrastructure.Trino;
using Microsoft.Extensions.Logging;

public class TrinoQueryService : ITrinoQueryService
{
    private readonly TrinoRestClient _client;
    private readonly ILogger<TrinoQueryService> _logger;

    public TrinoQueryService(TrinoRestClient client, ILogger<TrinoQueryService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<List<Dictionary<string, object>>> QueryAsync(string sql, string schema = "financeiro", CancellationToken ct = default)
    {

        var result = await _client.ExecuteQueryAsync(sql, schema, ct);

        Console.WriteLine($"[{DateTime.Now}] Linhas retornadas: {result.Rows.Count}");
        return result.Rows;
    }
    public async Task<List<Dictionary<string, object>>> QueryApprovedAsync(ConnectorProfile profile, ApprovedTrinoQuery query, int organizationId, CancellationToken ct = default)
        => (await _client.ExecuteApprovedAsync(profile, query, organizationId, ct)).Rows;
}
