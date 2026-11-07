namespace Portal.AI.Gateway.Infrastructure;

public sealed class DataConnector : IOrganizationEntity
{
    public int Id { get; set; }
    public int OrganizationId { get; set; } = 1;
    public string Name { get; set; } = "";
    public string ProfileId { get; set; } = "";
    public string Category { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string Catalog { get; set; } = "";
    public string Schema { get; set; } = "";
    public bool Enabled { get; set; }
    public string TablesJson { get; set; } = "[]";
    public string Status { get; set; } = "not_tested";
    public DateTime? TestedAt { get; set; }
    public int Version { get; set; }
}
public sealed class ConnectorGrant
{
    public int ConnectorId { get; set; }
    public int RoleId { get; set; }
    public DataConnector Connector { get; set; } = default!;
    public Role Role { get; set; } = default!;
}
