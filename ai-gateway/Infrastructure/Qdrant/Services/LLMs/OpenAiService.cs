using Portal.AI.Gateway.Infrastructure;
namespace AiGateway.Infrastructure.Qdrant.Services.LLMs;

public sealed class OpenAiService(IHttpClientFactory factory, IConfiguration config, AiProviderCatalog? catalog = null)
    : ChatCompletionAdapter(factory, config, catalog)
{
    public override string ProviderId => "openai";
}
