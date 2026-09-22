using Portal.AI.Gateway.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiGateway.Controllers.Permissions
{
    [ApiController]
    [Route("api/roles/{roleId}/permissions")]
    public class RolePermissionsController : ControllerBase
    {
        private readonly PortalDbContext _db;

        public RolePermissionsController(PortalDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetByRole(int roleId, CancellationToken ct)
        {
            var data = await _db.RolePermissions
                .Where(x => x.RoleId == roleId)
                .Include(x => x.Permission)
                .ToListAsync(ct);

            return Ok(data);
        }

        [HttpPost("{permissionId}")]
        public async Task<IActionResult> Add(
            int roleId,
            int permissionId,
            CancellationToken ct)
        {
            var exists = await _db.RolePermissions
                .AnyAsync(x => x.RoleId == roleId && x.PermissionId == permissionId, ct);

            if (exists)
                return BadRequest("Permissão já vinculada.");

            _db.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                PermissionId = permissionId
            });

            await _db.SaveChangesAsync(ct);
            return NoContent();
        }

        [HttpDelete("{permissionId}")]
        public async Task<IActionResult> Remove(
            int roleId,
            int permissionId,
            CancellationToken ct)
        {
            var item = await _db.RolePermissions
                .FirstOrDefaultAsync(x =>
                    x.RoleId == roleId &&
                    x.PermissionId == permissionId, ct);

            if (item == null) return NotFound();

            _db.RolePermissions.Remove(item);
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }
    }

}
