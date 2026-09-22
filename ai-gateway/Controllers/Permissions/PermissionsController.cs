using Portal.AI.Gateway.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiGateway.Controllers.Permissions
{


        [ApiController]
        [Route("api/permissions")]
        public class PermissionsController : ControllerBase
        {
            private readonly PortalDbContext _db;

            public PermissionsController(PortalDbContext db)
            {
                _db = db;
            }

            [HttpGet]
            public async Task<IActionResult> GetAll(CancellationToken ct)
            {
                return Ok(await _db.Permissions.ToListAsync(ct));
            }

            [HttpPost]
            public async Task<IActionResult> Create(
                string permissionKey,
                string? description,
                CancellationToken ct)
            {
                var perm = new Permission
                {
                    PermissionKey = permissionKey,
                    Description = description
                };

                _db.Permissions.Add(perm);
                await _db.SaveChangesAsync(ct);

                return Ok(perm);
            }

            [HttpDelete("{id}")]
            public async Task<IActionResult> Delete(int id, CancellationToken ct)
            {
                var perm = await _db.Permissions.FindAsync(new object[] { id }, ct);
                if (perm == null) return NotFound();

                _db.Permissions.Remove(perm);
                await _db.SaveChangesAsync(ct);
                return NoContent();
            }
        }


}
