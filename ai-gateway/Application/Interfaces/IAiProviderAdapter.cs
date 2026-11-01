namespace Portal.AI.Gateway.Application.Interfaces;

public interface IAiProviderAdapter : IOllamaService, ILlmMetrics, IChatTitleGenerator
{
    string ProviderId { get; }
}
