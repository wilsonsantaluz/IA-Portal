using System.Globalization;
using System.Text.Json;
using AiGateway.Security;
using Portal.AI.Gateway.Infrastructure;

namespace AiGateway.Infrastructure.Qdrant.Services.LLMs;

// Routing/retries belong to the proxy. Never retry a partially delivered stream here.
public sealed class LiteLlmService(IHttpClientFactory factory, IConfiguration config,
    AiProviderCatalog catalog, OrganizationScope organization, IHttpContextAccessor http)
    : ChatCompletionAdapter(factory, config, catalog)
{
    public override string ProviderId => "litellm";
    private string requestedModel = "";
    protected override void ConfigurePayload(Dictionary<string, object> payload)
    {
        if (organization.Id <= 0 || organization.UserId <= 0)
            throw new ChatException(403, "Organização autenticada obrigatória para o gateway.");
        LastCall.GatewayCallId = Guid.NewGuid().ToString("N");
        requestedModel = (string)payload["model"];
        payload["metadata"] = new {
            organization_id = organization.Id,
            requested_route = requestedModel,
            request_id = http.HttpContext?.TraceIdentifier ?? LastCall.GatewayCallId,
            portal_call_id = LastCall.GatewayCallId
        };
    }
    protected override void Capture(JsonElement root)
    {
        base.Capture(root);
        if ((LastCall.Model == requestedModel || LastCall.Model.StartsWith(requestedModel + "-fallback-", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(LastCall.Model)) && LastCall.DeploymentModel is string deployed)
            LastCall.Model = deployed;
    }
    protected override void CaptureHeaders(HttpResponseMessage response)
    {
        string? Header(string name) => response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;
        int? Count(string name) => int.TryParse(Header(name), NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n >= 0 ? n : null;
        LastCall.Retries = Count("x-litellm-attempted-retries");
        LastCall.Fallbacks = Count("x-litellm-attempted-fallbacks");
        // Generated deployment IDs identify the configured upstream even when body.model is a route alias.
        var deployment = Header("x-litellm-model-id");
        if (deployment is { Length: <= 260 } && !deployment.Any(char.IsControl)) {
            var parts = deployment.Split('|', 3);
            if (parts.Length >= 2 && parts[0].Length is > 0 and <= 40 && parts[1].Length > 0) {
                LastCall.UpstreamProvider = parts[0]; LastCall.DeploymentModel = parts[1];
            }
        }
        if (!response.IsSuccessStatusCode)
            LastCall.ErrorCode = ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);
    }
}
