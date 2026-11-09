using Portal.AI.Gateway.Infrastructure;

namespace AiGateway.Channels;

// External contacts are not portal users. Never use a phone number as a JWT identity.
public sealed class ChannelConnection : IOrganizationEntity {
    public Guid Id { get; set; }
    public int OrganizationId { get; set; }
    public string Provider { get; set; } = "simulator";
    public string ExternalAccountId { get; set; } = "";
    public string ExternalPhoneNumberId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
    public bool AutoReply { get; set; }
    public bool AgentEnabled { get; set; } = true;
    // First assignment is a constrained extractive agent; no arbitrary tools or SQL.
    public string AgentId { get; set; } = "local-extractive-v1";
    public string KnowledgeIdsJson { get; set; } = "[]";
    public int RetentionDays { get; set; } = 30;
    public int MonthlyLimit { get; set; } = 100;
    public int Version { get; set; }
}
public sealed class ChannelContact : IOrganizationEntity {
    public Guid Id { get; set; }
    public int OrganizationId { get; set; }
    public Guid ConnectionId { get; set; }
    public string ExternalId { get; set; } = "";
}
public sealed class ChannelConversation : IOrganizationEntity {
    public Guid Id { get; set; }
    public int OrganizationId { get; set; }
    public Guid ConnectionId { get; set; }
    public Guid ContactId { get; set; }
    public bool HumanHandoff { get; set; }
    public DateTime LastInboundAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }
}
public sealed class ChannelMessage : IOrganizationEntity {
    public Guid Id { get; set; }
    public int OrganizationId { get; set; }
    public Guid ConnectionId { get; set; }
    public Guid ConversationId { get; set; }
    public string ExternalId { get; set; } = "";
    public string Direction { get; set; } = "inbound";
    public string Text { get; set; } = "";
    public string State { get; set; } = "pending";
    public DateTime CreatedAt { get; set; }
    public DateTime NextAttemptAt { get; set; }
    public int Attempts { get; set; }
    public string SourcesJson { get; set; } = "[]";
    public string Category { get; set; } = "service";
}
public sealed class MessageDeliveryEvent : IOrganizationEntity {
    public Guid Id { get; set; }
    public int OrganizationId { get; set; }
    public Guid MessageId { get; set; }
    public string ExternalKey { get; set; } = "";
    public string Status { get; set; } = "";
    public string? PricingCategory { get; set; }
    public bool? Billable { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed record InboundChannelMessage(string ExternalId,string ContactId,string Text,DateTime Timestamp);
public sealed record ChannelSendResult(string ExternalId);
public interface IMessageChannelProvider { string Name { get; } }
public interface IInboundMessageHandler {
    Task<bool> Receive(Guid connectionId,InboundChannelMessage message,CancellationToken ct);
}
public interface IOutboundMessageSender : IMessageChannelProvider {
    Task<ChannelSendResult> Send(ChannelConnection connection,ChannelMessage message,CancellationToken ct);
}
// Separate from IChatRepository: external contacts have no portal-user session ownership.
public interface IChannelConversationRepository {
    Task<object> Messages(Guid conversationId,int offset,CancellationToken ct);
}
