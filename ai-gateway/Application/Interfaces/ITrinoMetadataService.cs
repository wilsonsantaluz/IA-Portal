public interface ITrinoMetadataService
{
    Task<List<Dictionary<string, object>>> GetAuthorizedMetadataAsync(Portal.AI.Gateway.Infrastructure.Trino.ConnectorProfile profile, int organizationId, CancellationToken ct = default);
    Task<string> GetCatalogMetadataAsync(string schema, CancellationToken ct = default);
}
