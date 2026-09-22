using Portal.AI.Gateway.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/roles")]
public class RolesController : ControllerBase
{
    private readonly PortalDbContext _db;

    public RolesController(PortalDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        return Ok(await _db.Roles.ToListAsync(ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create(string roleName, CancellationToken ct)
    {
        var role = new Role { RoleName = roleName };
        _db.Roles.Add(role);
        await _db.SaveChangesAsync(ct);
        return Ok(role);
    }
}
