using AiGateway.Dtos;
using Portal.AI.Gateway.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Org.BouncyCastle.Crypto.Generators;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AiGateway.Security;
using Microsoft.AspNetCore.Authorization;

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
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        LoginRequest req,
        CancellationToken ct)
    {
        var user = await _db.Users
            .Include(u => u.Roles)
                .ThenInclude(r => r.Role)
            .FirstOrDefaultAsync(x =>
                x.UserEmail.ToLower() == req.Email.Trim().ToLower() &&
                x.IsActive, ct);

        if (user == null)
            return Unauthorized("Usuário ou senha inválidos.");

        if (!BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized("Usuário ou senha inválidos.");

        // Roles
        var organizationId = await PortalAccess.DefaultOrganizationAsync(_db,user.UserId,_cfg,ct);
        if (organizationId == 0) return Unauthorized("Nenhuma organizacao autorizada.");
        var access = await PortalAccess.ReadAsync(_db, user.UserId, ct, organizationId);
        var roles = access.Roles;

        // Permissions
        var permissions = access.Permissions;

        var token = GenerateToken(user, roles, permissions);
        AiGateway.Infrastructure.Audit.ActionAuditContext.Login(HttpContext,user.UserId,organizationId,user.UserName);

        return Ok(new { user.UserId, user.UserName, email = user.UserEmail, token, roles, permissions, organizationId });
    }

    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout() => NoContent();

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().SingleAsync(x => x.UserId == PortalAccess.UserId(User), ct);
        return Ok(new { user.UserId, user.UserName, organizationId = _db.OrganizationId, email = user.UserEmail,
            roles = User.FindAll(ClaimTypes.Role).Select(x => x.Value),
            permissions = User.FindAll("perm").Select(x => x.Value) });
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
            new Claim("name", user.UserName),
            new Claim("ver", PortalAccess.Version(user, _cfg))
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
