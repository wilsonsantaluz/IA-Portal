namespace Portal.AI.Gateway.Infrastructure;

public sealed class OrganizationInvitation : IOrganizationEntity
{
    public Guid Id { get; set; }
    public int OrganizationId { get; set; }
    public string TokenHash { get; set; } = "";
    public string Role { get; set; } = "Member";
    public int CreatedByUserId { get; set; }
    public int IssuerMembershipVersion { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public int? AcceptedByUserId { get; set; }
    public int Version { get; set; }
}
