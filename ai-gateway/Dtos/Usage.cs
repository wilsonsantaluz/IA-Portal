namespace AiGateway.Dtos;

public sealed class UsageTotals
{
    public long LocalApiRequests { get; set; }
    public long Requests { get; set; }
    public long Errors { get; set; }
    public long Unknown { get; set; }
    public long ReportedRequests { get; set; }
    public long TotalTokenRequests { get; set; }
    public long PricedRequests { get; set; }
    public long? InputTokens { get; set; }
    public long? OutputTokens { get; set; }
    public long? TotalTokens { get; set; }
    public long? CacheTokens { get; set; }
    public long? EstimatedCostNanoUsd { get; set; }
    public decimal? EstimatedCostUsd => EstimatedCostNanoUsd / 1_000_000_000m;
    public string Currency => "USD";
    public bool TokensComplete => Requests > 0 && TotalTokenRequests == Requests;
    public bool CostComplete => Requests > 0 && PricedRequests == Requests;
}
public sealed record UsageCall(Guid Id, Guid SessionId, long MessageId, string Provider, string Model,
    string Purpose, DateTime CreatedAt, string Status, string Source, long? InputTokens, long? OutputTokens,
    long? TotalTokens, long? CacheTokens, long? DurationMs, decimal? EstimatedCostUsd, string? PriceVersion, string ApiBilling);
public sealed record UsageFilter(DateTime From, DateTime To, Guid? SessionId = null, string? Provider = null, string? Model = null, string? Purpose = null, int? UserId = null);
