namespace Portal.AI.Gateway.Application.Interfaces;

// Metadata of the most recent call on the request-scoped adapter, never prompt/content.
public interface ILlmMetrics { LlmCallMetrics LastCall { get; } }
public sealed class LlmCallMetrics
{
    public string Model { get; set; } = "";
    public string Status { get; set; } = "started";
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long? TotalTokens { get; set; }
    public long? CacheTokens { get; set; }
    public string Source => InputTokens.HasValue || OutputTokens.HasValue || TotalTokens.HasValue || CacheTokens.HasValue ? "reported" : "unavailable";
}
