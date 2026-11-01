using AiGateway.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AiGateway.Controllers.Permissions;

[ApiController, Route("api/permissions"), Authorize(Policy = PortalAccess.Administration)]
public class PermissionsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetAll() => Ok(PortalAccess.Catalog.Select(x => new { key = x.Key, screen = x.Screen, description = x.Description }));
}
