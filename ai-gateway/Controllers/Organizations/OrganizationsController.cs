using AiGateway.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Infrastructure;

namespace AiGateway.Controllers.Organizations;

[ApiController, Authorize, Route("api/organizations")]
public sealed class OrganizationsController(PortalDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var userId = PortalAccess.UserId(User);
        return Ok(await db.Organizations.Where(o => o.IsActive && db.OrganizationMemberships.Any(m =>
            m.OrganizationId == o.Id && m.UserId == userId && m.IsActive))
            .OrderBy(o => o.Id).Select(o => new { o.Id, o.Name, o.Slug }).ToListAsync(ct));
    }
}
