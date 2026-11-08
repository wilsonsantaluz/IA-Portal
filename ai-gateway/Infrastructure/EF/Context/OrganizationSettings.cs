namespace Portal.AI.Gateway.Infrastructure;

public sealed class OrganizationAiDefault : IOrganizationEntity
{
    public int OrganizationId { get; set; }
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
    public int Version { get; set; }
}

public sealed class OrganizationAiPreference : IOrganizationEntity
{
    public int OrganizationId { get; set; }
    public int UserId { get; set; }
    public string Provider { get; set; } = "";
    public string Model { get; set; } = "";
}
