using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using AiGateway.Security;
using Microsoft.EntityFrameworkCore;

namespace Portal.AI.Gateway.Infrastructure;

public sealed record SignupInput([Required, StringLength(150)] string Name,
    [Required, EmailAddress, StringLength(150)] string Email,
    [Required, StringLength(72, MinimumLength=12)] string Password);
public sealed record SignupConfirmation(string? Token);

public interface ISignupMailer { Task Send(string email, string link, CancellationToken ct); }

public sealed class SmtpSignupMailer(IConfiguration config) : ISignupMailer
{
    public async Task Send(string email, string link, CancellationToken ct)
    {
        // Password comes exclusively from server environment/configuration, never SQLite or HTTP.
        using var client = new SmtpClient(config["Smtp:Host"] ?? "smtp.gmail.com", 587) {
            EnableSsl=true, UseDefaultCredentials=false,
            Credentials=new NetworkCredential(config["Smtp:Username"], config["Smtp:AppPassword"])
        };
        using var message=new MailMessage(config["Smtp:Username"]!, email) {
            Subject="Confirme seu cadastro no IA-Portal",
            Body=$"Confirme seu e-mail para acessar o IA-Portal e a base demonstrativa Atlas:\n\n{link}\n\nO link expira em 30 minutos. Se não solicitou este cadastro, ignore esta mensagem."
        };
        await client.SendMailAsync(message, ct);
    }
}

public sealed class AtlasSignupService(PortalDbContext db, OrganizationScope scope,
    IConfiguration config, TimeProvider clock, ISignupMailer mailer, ILogger<AtlasSignupService> logger)
{
    public const string GroupName="Atlas — Colaboração";
    public static readonly string[] Grants=[PortalAccess.Chat, PortalAccess.Knowledge,
        PortalAccess.EditKnowledgeContent, PortalAccess.Dashboard, PortalAccess.DataQuery];
    private DateTime Now=>clock.GetUtcNow().UtcDateTime;
    public bool Enabled=>config.GetValue<bool>("Signup:Enabled");
    private async Task<Organization> Target(CancellationToken ct)
    {
        if(!Enabled)throw new ChatException(404,"Cadastro público indisponível.");
        return await db.Organizations.SingleOrDefaultAsync(o=>o.Slug=="atlas-demo" && o.IsActive && o.Id!=1,ct)
            ?? throw new ChatException(503,"Demonstração Atlas indisponível.");
    }
    private string PublicUrl()
    {
        if(!Uri.TryCreate(config["Signup:PublicUrl"],UriKind.Absolute,out var uri)
            || uri.Scheme!="https" || !string.IsNullOrEmpty(uri.UserInfo) || uri.AbsolutePath!="/"
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
            || string.IsNullOrWhiteSpace(config["Smtp:Username"]) || string.IsNullOrWhiteSpace(config["Smtp:AppPassword"]))
            throw new ChatException(503,"Cadastro por e-mail ainda não configurado.");
        return uri.GetLeftPart(UriPartial.Authority);
    }
    private static string Hash(string token)=>Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token)));
    public async Task Register(SignupInput input,CancellationToken ct)
    {
        var target=await Target(ct);var origin=PublicUrl();
        if(string.IsNullOrWhiteSpace(input.Name) || !new EmailAddressAttribute().IsValid(input.Email)
            || input.Name.Trim().Length>150 || input.Email.Length>150 || input.Password.Length<12
            || Encoding.UTF8.GetByteCount(input.Password)>72)
            throw new ChatException(400,"Informe nome, e-mail válido e senha de 12 a 72 bytes.");
        var email=input.Email.Trim().ToLowerInvariant();var now=Now;
        // Generic response for existing/disabled accounts. Never reactivate or change their passwords.
        if(await db.Users.AnyAsync(u=>u.UserEmail.ToLower()==email,ct))return;
        if(await db.SignupRequests.AnyAsync(r=>r.Email==email && r.CreatedAt>now.AddMinutes(-2),ct))return;
        var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var pending=new SignupRequest{TokenHash=Hash(token),Email=email,Name=input.Name.Trim(),
            PasswordHash=BCrypt.Net.BCrypt.HashPassword(input.Password),CreatedAt=now,ExpiresAt=now.AddMinutes(30)};
        var previous=scope.Id;
        try {
            // The only anonymous provisioning scope is resolved by server slug, never headers/body.
            scope.Id=target.Id;
            await db.SignupRequests.Where(r=>r.ExpiresAt<=now).ExecuteDeleteAsync(ct);
            db.SignupRequests.Add(pending);await db.SaveChangesAsync(ct);
            try { await mailer.Send(email,$"{origin}/signup#confirm={token}",ct); }
            catch(Exception ex) when(ex is SmtpException or InvalidOperationException) {
                await db.SignupRequests.Where(r=>r.TokenHash==pending.TokenHash).ExecuteDeleteAsync(CancellationToken.None);
                logger.LogWarning("Signup delivery failed ({ErrorType})",ex.GetType().Name);
                throw new ChatException(503,"Não foi possível enviar a confirmação. Tente novamente mais tarde.");
            }
        } finally { scope.Id=previous; }
    }
    public async Task Confirm(string? token,CancellationToken ct)
    {
        var target=await Target(ct);
        if(token is null || token.Length!=64 || token.Any(c=>!char.IsAsciiHexDigit(c)))throw Invalid();
        var hash=Hash(token.ToUpperInvariant());var now=Now;var previous=scope.Id;
        try {
            scope.Id=target.Id;
            await using var tx=await db.Database.BeginTransactionAsync(ct);
            var pending=await db.SignupRequests.AsNoTracking().SingleOrDefaultAsync(r=>r.TokenHash==hash && r.ExpiresAt>now,ct)
                ?? throw Invalid();
            if(await db.Users.AnyAsync(u=>u.UserEmail.ToLower()==pending.Email,ct))throw Invalid();
            var consumed=await db.SignupRequests.Where(r=>r.TokenHash==hash && r.ExpiresAt>now).ExecuteDeleteAsync(ct);
            if(consumed!=1)throw Invalid();
            var role=await db.Roles.SingleOrDefaultAsync(r=>r.OrganizationId==target.Id && r.RoleName==GroupName,ct);
            if(role is null) {
                role=new Role{OrganizationId=target.Id,RoleName=GroupName};db.Roles.Add(role);
                await db.SaveChangesAsync(ct);
                var permissionIds=await db.Permissions.Where(p=>Grants.Contains(p.PermissionKey)).Select(p=>p.PermissionId).ToArrayAsync(ct);
                if(permissionIds.Length!=Grants.Length)throw new ChatException(503,"Permissões de cadastro indisponíveis.");
                foreach(var id in permissionIds)db.RolePermissions.Add(new(){RoleId=role.RoleId,PermissionId=id});
            } else {
                var actual=await db.RolePermissions.Where(r=>r.RoleId==role.RoleId).Select(r=>r.Permission.PermissionKey).ToArrayAsync(ct);
                if(!actual.Order().SequenceEqual(Grants.Order()))throw new ChatException(503,"Grupo de cadastro foi alterado. Solicite revisão ao administrador.");
            }
            var demoConnectors=await db.DataConnectors.Where(c=>c.OrganizationId==target.Id && c.Enabled && c.Category=="demo").Select(c=>c.Id).ToArrayAsync(ct);
            foreach(var connectorId in demoConnectors)
                if(!await db.ConnectorGrants.AnyAsync(g=>g.ConnectorId==connectorId && g.RoleId==role.RoleId,ct))
                    db.ConnectorGrants.Add(new(){ConnectorId=connectorId,RoleId=role.RoleId});
            var user=new User{UserName=pending.Name,UserEmail=pending.Email,PasswordHash=pending.PasswordHash,IsActive=true,CreatedAt=now};
            db.Users.Add(user);await db.SaveChangesAsync(ct);
            db.OrganizationMemberships.Add(new(){OrganizationId=target.Id,UserId=user.UserId,PolicyRole=null});
            db.UserRoles.Add(new(){UserId=user.UserId,RoleId=role.RoleId});
            // Only synthetic demo connectors above receive group grants; other sources remain explicit.
            await db.SignupRequests.Where(r=>r.Email==pending.Email).ExecuteDeleteAsync(ct);
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        } finally { scope.Id=previous; }
    }
    private static ChatException Invalid()=>new(400,"Confirmação inválida ou expirada. Solicite um novo cadastro.");
}
