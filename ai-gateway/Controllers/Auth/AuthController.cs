using AiGateway.Dtos;
using Portal.AI.Gateway.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Org.BouncyCastle.Crypto.Generators;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly PortalDbContext _db;
    private readonly IConfiguration _cfg;

    public AuthController(PortalDbContext db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginRequest req,
        CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.Roles)
                .ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(x =>
                x.UserEmail == req.Email &&
                x.IsActive, ct);

        if (user == null)
            return Unauthorized("Usuário ou senha inválidos.");

        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized("Usuário ou senha inválidos.");

        // Roles
        var roles = user.Roles
            .Select(x => x.Role.RoleName)
            .Distinct()
            .ToArray();

        // Permissions
        var permissions = await _db.RolePermissions
            .Where(rp => user.Roles.Select(r => r.RoleId).Contains(rp.RoleId))
            .Include(rp => rp.Permission)
            .Select(rp => rp.Permission.PermissionKey)
            .Distinct()
            .ToArrayAsync(ct);

        var token = GenerateToken(user, roles, permissions);

        return Ok(new LoginResponse(
            user.UserId,
            user.UserName,
            user.UserEmail,
            token,
            roles,
            permissions
        ));
    }

    private string GenerateToken(
        User user,
        string[] roles,
        string[] permissions)
    {
        var jwt = _cfg.GetSection("Jwt");

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.UserEmail),
            new Claim("name", user.UserName)
        };

        claims.AddRange(roles.Select(r => new Claim("role", r)));
        claims.AddRange(permissions.Select(p => new Claim("perm", p)));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwt["Key"]!)
        );

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                int.Parse(jwt["ExpireMinutes"]!)
            ),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
