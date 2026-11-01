using AiGateway.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Infrastructure;
namespace AiGateway.Controllers.Permissions;

// Compatibility read endpoint; changes go through the validated atomic group update.
[ApiController, Route("api/roles/{roleId:int}/permissions"), Authorize(Policy = PortalAccess.Administration)]
public class RolePermissionsController(PortalDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetByRole(int roleId, CancellationToken ct) => Ok(await db.RolePermissions
        .Where(x => x.RoleId == roleId).Select(x => new { x.PermissionId, x.Permission.PermissionKey }).ToListAsync(ct));
}
