namespace Portal.AI.Gateway.Infrastructure;

public sealed class ActionAuditEvent
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? OrganizationId { get; set; }
    public int? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string Action { get; set; } = "";
    public string Operation { get; set; } = "";
    public string Area { get; set; } = "";
    public string Method { get; set; } = "";
    public string Route { get; set; } = "";
    public string? TargetId { get; set; }
    public string Outcome { get; set; } = "";
    public int StatusCode { get; set; }
    public long DurationMs { get; set; }
    public string RequestId { get; set; } = "";
    public string? RemoteAddress { get; set; }
}
