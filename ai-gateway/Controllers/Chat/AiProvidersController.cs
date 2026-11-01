using AiGateway.Dtos;
using AiGateway.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.AI.Gateway.Infrastructure;
namespace AiGateway.Controllers.Chat;

[ApiController, Authorize(Policy = PortalAccess.Chat), Route("api/ai")]
public sealed class AiProvidersController(AiProviderCatalog catalog) : ControllerBase
{
    [HttpGet("providers")]
    public async Task<IActionResult> Providers(CancellationToken ct) => Ok((await catalog.List(false, ct)).Select(p => new { p.Id, p.Models, p.DefaultModel, p.Streaming, p.ApiBilling }));
    [HttpGet("/api/me/ai-preference")]
    public async Task<IActionResult> Preference(CancellationToken ct) => Ok(await catalog.Preference(PortalAccess.UserId(User), ct));
    [HttpPut("/api/me/ai-preference")]
    public async Task<IActionResult> SetPreference(ModelSelection selection, CancellationToken ct) { await catalog.SetPreference(PortalAccess.UserId(User), selection, ct); return Ok(selection); }
}
