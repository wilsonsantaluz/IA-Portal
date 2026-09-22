public interface ITrinoMetadataService
{
    Task<string> GetCatalogMetadataAsync(string schema, CancellationToken ct = default);
}