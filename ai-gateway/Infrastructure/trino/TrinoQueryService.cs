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
        _logger.LogInformation("Executando SQL no Trino: {Sql}", sql);
        Console.WriteLine($"[{DateTime.Now}] Executando SQL no Trino: {sql}");

        var result = await _client.ExecuteQueryAsync(sql, schema, ct);

        Console.WriteLine($"[{DateTime.Now}] Linhas retornadas: {result.Rows.Count}");
        return result.Rows;
    }
}
