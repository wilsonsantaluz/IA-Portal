using Portal.AI.Gateway.Infrastructure.Trino;
using System.Data;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface ITrinoQueryService
{
    Task<List<Dictionary<string, object>>> QueryApprovedAsync(ConnectorProfile profile, ApprovedTrinoQuery query, int organizationId, CancellationToken ct = default);
    Task<List<Dictionary<string, object>>> QueryAsync(string sql, string schema = "financeiro", CancellationToken ct = default);
}

