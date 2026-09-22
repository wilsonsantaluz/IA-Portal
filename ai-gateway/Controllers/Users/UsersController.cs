using AiGateway.Dtos;
using Portal.AI.Gateway.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace AiGateway.Controllers.Users
{
    [ApiController]
    [Route("api/users")]
    public class UsersController : ControllerBase
    {
        private readonly PortalDbContext _db;

        public UsersController(PortalDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken ct)
        {
            var users = await _db.Users
                .Include(u => u.Roles)
                .ThenInclude(r => r.Role)
                .ToListAsync(ct);

            return Ok(users);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id, CancellationToken ct)
        {
            var user = await _db.Users
                .Include(u => u.Roles)
                .ThenInclude(r => r.Role)
                .FirstOrDefaultAsync(x => x.UserId == id, ct);

            if (user == null) return NotFound();
            return Ok(user);
        }

        [HttpPost]
        public async Task<IActionResult> Create(CreateUserRequest req, CancellationToken ct)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);

            var user = new User
            {
                UserName = req.UserName,
                UserEmail = req.UserEmail,
                PasswordHash = hash,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);

            return CreatedAtAction(nameof(GetById), new { id = user.UserId }, user);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, UpdateUserRequest req, CancellationToken ct)
        {
            var user = await _db.Users.FindAsync(new object[] { id }, ct);
            if (user == null) return NotFound();

            user.UserName = req.UserName;
            user.UserEmail = req.UserEmail;
            user.IsActive = req.IsActive;

            await _db.SaveChangesAsync(ct);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id, CancellationToken ct)
        {
            var user = await _db.Users.FindAsync(new object[] { id }, ct);
            if (user == null) return NotFound();

            _db.Users.Remove(user);
            await _db.SaveChangesAsync(ct);
            return NoContent();
        }


        [HttpPost("{userId}/roles/{roleId}")]
        public async Task<IActionResult> AddRole(int userId, int roleId, CancellationToken ct)
        {
            var exists = await _db.UserRoles
                .AnyAsync(x => x.UserId == userId && x.RoleId == roleId, ct);

            if (exists) return BadRequest("Role já atribuída.");

            _db.UserRoles.Add(new UserRole
            {
                UserId = userId,
                RoleId = roleId
            });

            await _db.SaveChangesAsync(ct);
            return NoContent();
        }
    }

    }
