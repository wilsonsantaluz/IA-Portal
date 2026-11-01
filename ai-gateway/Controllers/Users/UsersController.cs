using AiGateway.Dtos;
using AiGateway.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Infrastructure;

namespace AiGateway.Controllers.Users;

[ApiController, Route("api/users"), Authorize(Policy = PortalAccess.Administration)]
public class UsersController(PortalDbContext db) : ControllerBase
{
    private IQueryable<object> Users => db.Users.AsNoTracking().OrderBy(x => x.UserName).Select(x => new {
        x.UserId, x.UserName, x.UserEmail, x.IsActive, x.CreatedAt,
        groups = x.Roles.Select(r => new { id = r.RoleId, name = r.Role.RoleName })
    });

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await Users.ToListAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        // Projection is explicit: password hashes and navigation entities never leave the API.
        var user = await db.Users.AsNoTracking().Where(x => x.UserId == id).Select(x => new {
            x.UserId, x.UserName, x.UserEmail, x.IsActive, x.CreatedAt,
            groups = x.Roles.Select(r => new { id = r.RoleId, name = r.Role.RoleName })
        }).FirstOrDefaultAsync(ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateUserRequest req, CancellationToken ct)
    {
        if (req.UserName.Trim().Length < 2 || System.Text.Encoding.UTF8.GetByteCount(req.Password) > 72)
            return BadRequest(new { error = "Informe um nome válido e senha de até 72 bytes." });
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var email = req.UserEmail.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.UserEmail.ToLower() == email, ct)) return Conflict(new { error = "E-mail já cadastrado." });
        var ids = (req.GroupIds ?? []).Distinct().ToArray();
        if (await db.Roles.CountAsync(x => ids.Contains(x.RoleId), ct) != ids.Length) return BadRequest(new { error = "Grupo inválido." });
        var user = new User { UserName = req.UserName.Trim(), UserEmail = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password), IsActive = true, CreatedAt = DateTime.UtcNow };
        foreach (var id in ids) user.Roles.Add(new UserRole { RoleId = id });
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return CreatedAtAction(nameof(GetById), new { id = user.UserId }, new { user.UserId });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateUserRequest req, CancellationToken ct)
    {
        if (req.GroupIds is null || req.UserName.Trim().Length < 2 || (req.Password is not null && System.Text.Encoding.UTF8.GetByteCount(req.Password) > 72))
            return BadRequest(new { error = "Dados de usuário inválidos." });
        // SQLite serializable write transaction prevents concurrent removal of the final admin.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = await db.Users.Include(x => x.Roles).ThenInclude(x => x.Role).FirstOrDefaultAsync(x => x.UserId == id, ct);
        if (user is null) return NotFound();
        var ids = req.GroupIds.Distinct().ToArray();
        if (await db.Roles.CountAsync(x => ids.Contains(x.RoleId), ct) != ids.Length) return BadRequest(new { error = "Grupo inválido." });
        var email = req.UserEmail.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(x => x.UserId != id && x.UserEmail.ToLower() == email, ct)) return Conflict(new { error = "E-mail já cadastrado." });
        var adminId = await db.Roles.Where(x => x.RoleName == "admin").Select(x => x.RoleId).SingleAsync(ct);
        if ((!req.IsActive || !ids.Contains(adminId)) && user.IsActive && user.Roles.Any(x => x.RoleId == adminId))
        {
            if (!await db.Users.AnyAsync(x => x.UserId != id && x.IsActive && x.Roles.Any(r => r.RoleId == adminId), ct))
                return Conflict(new { error = "Mantenha pelo menos um administrador ativo no grupo admin." });
        }
        if (id == PortalAccess.UserId(User) && (!req.IsActive || (user.Roles.Any(x => x.RoleId == adminId) && !ids.Contains(adminId))))
            return Conflict(new { error = "Não é permitido desativar a própria conta ou remover seu próprio grupo admin." });
        user.UserName = req.UserName.Trim(); user.UserEmail = email; user.IsActive = req.IsActive;
        if (req.Password is not null) user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password);
        db.UserRoles.RemoveRange(user.Roles.Where(x => !ids.Contains(x.RoleId)));
        foreach (var groupId in ids.Where(groupId => user.Roles.All(r => r.RoleId != groupId))) db.UserRoles.Add(new UserRole { UserId = id, RoleId = groupId });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }
}
