using AiGateway.Dtos;
using AiGateway.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.AI.Gateway.Infrastructure;

namespace AiGateway.Controllers.Chat;

[ApiController, Authorize, Route("api/usage")]
public sealed class UsageController(UsageQueryService usage) : ControllerBase
{
    private static UsageFilter Filter(DateTimeOffset? from, DateTimeOffset? to, Guid? sessionId, string? provider, string? model, string? purpose, int? userId = null)
    {
        var end = to?.UtcDateTime ?? DateTime.UtcNow;
        return new(from?.UtcDateTime ?? new DateTime(end.Year, end.Month, 1, 0, 0, 0, DateTimeKind.Utc), end, sessionId, provider, model, purpose, userId);
    }
    [HttpGet("summary"), Authorize(Policy = PortalAccess.Chat)]
    public async Task<IActionResult> Summary(DateTimeOffset? from, DateTimeOffset? to, Guid? sessionId, string? provider, string? model, string? purpose, int? userId, CancellationToken ct) => Ok(await usage.Summary(Filter(from, to, sessionId, provider, model, purpose, userId), false, ct));
    [HttpGet("groups"), Authorize(Policy = PortalAccess.Chat)]
    public async Task<IActionResult> Groups(DateTimeOffset? from, DateTimeOffset? to, Guid? sessionId, string? provider, string? model, string? purpose, string by = "day", int page = 1, int limit = 30, CancellationToken ct = default) => Ok(await usage.Groups(Filter(from, to, sessionId, provider, model, purpose), false, by, page, limit, ct));
    [HttpGet("calls"), Authorize(Policy = PortalAccess.Chat)]
    public async Task<IActionResult> Calls(DateTimeOffset? from, DateTimeOffset? to, Guid? sessionId, string? provider, string? model, string? purpose, int page = 1, int limit = 30, CancellationToken ct = default) => Ok(await usage.Calls(Filter(from, to, sessionId, provider, model, purpose), page, limit, ct));
    [HttpGet("admin/summary"), Authorize(Policy = PortalAccess.UsageAdmin)]
    public async Task<IActionResult> AdminSummary(DateTimeOffset? from, DateTimeOffset? to, string? provider, string? model, string? purpose, int? userId, CancellationToken ct) => Ok(await usage.Summary(Filter(from, to, null, provider, model, purpose, userId), true, ct));
    [HttpGet("admin/groups"), Authorize(Policy = PortalAccess.UsageAdmin)]
    public async Task<IActionResult> AdminGroups(DateTimeOffset? from, DateTimeOffset? to, string? provider, string? model, string? purpose, int? userId, string by = "user", int page = 1, int limit = 30, CancellationToken ct = default) => Ok(await usage.Groups(Filter(from, to, null, provider, model, purpose, userId), true, by, page, limit, ct));
}
