using AiGateway.Dtos;
using AiGateway.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.AI.Gateway.Infrastructure;
using System.Net.Http.Headers;
using System.Text.Json;
namespace AiGateway.Controllers.Chat;

[ApiController, Authorize(Policy = PortalAccess.AiAdministration), Route("api/admin/ai")]
public sealed class AiSettingsController(AiProviderCatalog catalog, IHttpClientFactory factory) : ControllerBase
{
    [HttpGet("providers")]
    public async Task<IActionResult> Providers(CancellationToken ct) => Ok(await catalog.List(true, ct));
    [HttpPut("providers/{id}")]
    public async Task<IActionResult> Save(string id, ProviderUpdate request, CancellationToken ct) => Ok(await catalog.Save(id, request, ct));
    [HttpGet("default")]
    public async Task<IActionResult> Default(CancellationToken ct) => Ok(await catalog.Global(ct));
    [HttpPut("default")]
    public async Task<IActionResult> Default(DefaultUpdate request, CancellationToken ct) { await catalog.SetGlobal(request, ct); return Ok(await catalog.Global(ct)); }
    [HttpPost("providers/{id}/test")]
    public async Task<IActionResult> Test(string id, CancellationToken ct)
    {
        var info = await catalog.Get(id, ct);
        if (!info.Configured) return Ok(new { success = false, status = "not_configured", testedAt = DateTime.UtcNow });
        if (info.ConfigurationError is not null) return Ok(new { success = false, status = "invalid_endpoint", testedAt = DateTime.UtcNow });
        // Metadata-only request: no inference, no model download, no prompt or chargeable title.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        string status;
        try {
            using var client = factory.CreateClient("ai-provider");
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(info.Endpoint), id == "ollama" ? "api/tags" : "models"));
            var key = catalog.Key(id); if (!string.IsNullOrWhiteSpace(key)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode) status = (int)response.StatusCode is 401 or 403 ? "authentication" : (int)response.StatusCode == 429 ? "rate_limit" : "unavailable";
            else {
                // Bound diagnostic response size; never return its raw body or infer a model is installed from HTTP 200 alone.
                using var body = await response.Content.ReadAsStreamAsync(timeout.Token);
                using var buffer = new MemoryStream(); var chunk = new byte[4096];
                while (true) { var count = await body.ReadAsync(chunk, timeout.Token); if (count == 0) break; if (buffer.Length + count > 262144) throw new InvalidDataException(); buffer.Write(chunk, 0, count); }
                using var json = JsonDocument.Parse(buffer.ToArray());
                status = json.RootElement.TryGetProperty(id == "ollama" ? "models" : "data", out var models) && models.ValueKind == JsonValueKind.Array ? "connected" : "invalid_response";
            }
        } catch (OperationCanceledException) { status = "timeout"; }
        catch { status = "unavailable"; }
        return Ok(new { success = status == "connected", status, testedAt = DateTime.UtcNow });
    }
}
