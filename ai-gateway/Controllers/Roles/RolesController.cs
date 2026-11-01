using AiGateway.Dtos;
using AiGateway.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Infrastructure;

[ApiController, Route("api/roles"), Authorize(Policy = PortalAccess.Administration)]
public class RolesController(PortalDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await db.Roles.AsNoTracking().OrderBy(x => x.RoleName).Select(x => new {
        id = x.RoleId, name = x.RoleName, isSystem = x.RoleName == "admin",
        memberCount = db.UserRoles.Count(r => r.RoleId == x.RoleId),
        permissions = db.RolePermissions.Where(r => r.RoleId == x.RoleId).Select(r => r.Permission.PermissionKey).ToArray()
    }).ToListAsync(ct));

    [HttpPost]
    public Task<IActionResult> Create(GroupRequest req, CancellationToken ct) => Save(null, req, ct);
    [HttpPut("{id:int}")]
    public Task<IActionResult> Update(int id, GroupRequest req, CancellationToken ct) => Save(id, req, ct);

    private async Task<IActionResult> Save(int? id, GroupRequest req, CancellationToken ct)
    {
        var name = req.Name.Trim();
        if (name.Length < 2 || name.Equals("admin", StringComparison.OrdinalIgnoreCase) || req.Permissions is null || req.Permissions.Except(PortalAccess.Catalog.Select(x => x.Key)).Any())
            return BadRequest(new { error = "Nome reservado ou permissões inválidas." });
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var role = id.HasValue ? await db.Roles.FindAsync(new object[] { id.Value }, ct) : new Role();
        if (role is null) return NotFound();
        if (role.RoleName == "admin") return Conflict(new { error = "O grupo admin é protegido." });
        if (await db.Roles.AnyAsync(x => x.RoleId != (id ?? 0) && x.RoleName.ToLower() == name.ToLower(), ct)) return Conflict(new { error = "Já existe um grupo com esse nome." });
        role.RoleName = name;
        if (!id.HasValue) db.Roles.Add(role);
        await db.SaveChangesAsync(ct);
        var keys = req.Permissions.Distinct().ToList();
        if (keys.Contains(PortalAccess.EditKnowledge) && !keys.Contains(PortalAccess.Knowledge)) keys.Add(PortalAccess.Knowledge);
        var permissions = await db.Permissions.Where(x => keys.Contains(x.PermissionKey)).ToListAsync(ct);
        db.RolePermissions.RemoveRange(await db.RolePermissions.Where(x => x.RoleId == role.RoleId).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
        foreach (var p in permissions) db.RolePermissions.Add(new RolePermission { RoleId = role.RoleId, PermissionId = p.PermissionId });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(new { id = role.RoleId });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var role = await db.Roles.FindAsync(new object[] { id }, ct);
        if (role is null) return NotFound();
        if (role.RoleName == "admin" || await db.UserRoles.AnyAsync(x => x.RoleId == id, ct))
            return Conflict(new { error = "Remova os usuários do grupo antes de excluí-lo. O grupo admin é protegido." });
        db.Roles.Remove(role); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return NoContent();
    }
}
