using Portal.AI.Gateway.Application.Interfaces;
using System.Text;

public class TrinoMetadataService : ITrinoMetadataService
{
    private readonly ITrinoQueryService _trino;

    public TrinoMetadataService(ITrinoQueryService trino)
    {
        _trino = trino;
    }

    public async Task<string> GetCatalogMetadataAsync(string schema, CancellationToken ct = default)
    {
        var sb = new StringBuilder();

        // SHOW TABLES
        var tables = await _trino.QueryAsync($"SHOW TABLES FROM {schema}", schema, ct);

        sb.AppendLine($"Schema: {schema}");

        foreach (var t in tables)
        {
            string table = t["Table"].ToString()!;
            sb.AppendLine($"Tabela: {table}");

            var cols = await _trino.QueryAsync($"DESCRIBE {schema}.{table}", schema, ct);

            foreach (var c in cols)
            {
                sb.AppendLine($" - {c["Column"]} ({c["Type"]})");
            }
        }

        return sb.ToString();
    }
}
