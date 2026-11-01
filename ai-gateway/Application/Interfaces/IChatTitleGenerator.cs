namespace Portal.AI.Gateway.Application.Interfaces;

// Optional capability of the existing provider adapter; no parallel chat pipeline.
public interface IChatTitleGenerator
{
    Task<TitleGeneration> GenerateTitleAsync(string model, string firstQuestion, CancellationToken ct);
}

public sealed record TitleGeneration(string Content, string Model, long? InputTokens = null,
    long? OutputTokens = null, long? TotalTokens = null, long? CacheTokens = null);
