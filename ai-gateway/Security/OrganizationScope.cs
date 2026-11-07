namespace AiGateway.Security;

// Scoped once during JWT validation. Never derived directly from a body/LLM.
public sealed class OrganizationScope
{
    public int Id { get; internal set; }
    public int UserId { get; internal set; }
}
