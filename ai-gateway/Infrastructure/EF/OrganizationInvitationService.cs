using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

namespace Portal.AI.Gateway.Infrastructure;

public sealed record InvitationCreateRequest([Required] string Role);
public sealed record InvitationTokenRequest(string? Token);
public sealed record InvitationRevokeRequest(int Version);
public sealed record InvitationCreated(Guid Id,string Token,string Role,DateTime ExpiresAt);

public sealed class OrganizationInvitationService(PortalDbContext db,TimeProvider clock,
    ILogger<OrganizationInvitationService> logger)
{
    private DateTime Now=>clock.GetUtcNow().UtcDateTime;
    private static ChatException Unavailable()=>new(404,"Convite inválido ou indisponível.");
    private static string Hash(string? token) {
        if(token is null || token.Length!=64 || token.Any(c=>!char.IsAsciiHexDigit(c)))throw Unavailable();
        return Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token.ToUpperInvariant())));
    }
    private async Task<OrganizationMembership> Owner(int actor,CancellationToken ct) {
        var member=await db.OrganizationMemberships.SingleOrDefaultAsync(m=>m.OrganizationId==db.OrganizationId && m.UserId==actor && m.IsActive && m.PolicyRole=="Owner" && db.Users.Any(u=>u.UserId==actor && u.IsActive) && db.Organizations.Any(o=>o.Id==m.OrganizationId && o.IsActive),ct);
        return member ?? throw new ChatException(403,"Somente Owner pode gerenciar convites.");
    }
    private IQueryable<OrganizationInvitation> Usable(DateTime now) => db.OrganizationInvitations
        // Deliberate capability boundary: callers know a 256-bit secret, not an organization ID.
        // This read is no-tracking; cross-tenant consumption uses narrowly bounded SQL below.
        .IgnoreQueryFilters().AsNoTracking().Where(i=>i.RevokedAt==null && i.AcceptedAt==null && i.ExpiresAt>now
            && db.Organizations.Any(o=>o.Id==i.OrganizationId && o.IsActive)
            && db.OrganizationMemberships.Any(m=>m.OrganizationId==i.OrganizationId && m.UserId==i.CreatedByUserId && m.IsActive && m.PolicyRole=="Owner" && m.Version==i.IssuerMembershipVersion)
            && db.Users.Any(u=>u.UserId==i.CreatedByUserId && u.IsActive));

    public async Task<InvitationCreated> Create(int actor,string role,CancellationToken ct) {
        if(role is not ("Member" or "Viewer"))throw new ChatException(400,"Convites permitem apenas Member ou Viewer.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var owner=await Owner(actor,ct);var now=Now;
        if(await Usable(now).CountAsync(i=>i.OrganizationId==db.OrganizationId,ct)>=20)
            throw new ChatException(409,"Limite de 20 convites pendentes. Revogue ou aguarde a expiração.");
        var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var item=new OrganizationInvitation{Id=Guid.NewGuid(),OrganizationId=db.OrganizationId,TokenHash=Hash(token),Role=role,
            CreatedByUserId=actor,IssuerMembershipVersion=owner.Version,CreatedAt=now,ExpiresAt=now.AddHours(24)};
        db.OrganizationInvitations.Add(item);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        logger.LogInformation("OrganizationInvitationCreated Organization={Organization} Actor={Actor} Invitation={Invitation}",db.OrganizationId,actor,item.Id);
        return new(item.Id,token,role,item.ExpiresAt);
    }
    public async Task<object> List(int actor,CancellationToken ct) {
        await Owner(actor,ct);var now=Now;
        var items=await db.OrganizationInvitations.AsNoTracking().Where(i=>i.OrganizationId==db.OrganizationId)
            .OrderByDescending(i=>i.CreatedAt).Take(100).Select(i=>new{i.Id,i.Role,i.CreatedAt,i.ExpiresAt,i.AcceptedAt,i.RevokedAt,i.Version,
                issuerValid=db.OrganizationMemberships.Any(m=>m.OrganizationId==i.OrganizationId && m.UserId==i.CreatedByUserId && m.IsActive && m.PolicyRole=="Owner" && m.Version==i.IssuerMembershipVersion) && db.Users.Any(u=>u.UserId==i.CreatedByUserId && u.IsActive)}).ToListAsync(ct);
        return items.Select(i=>new{i.Id,i.Role,i.CreatedAt,i.ExpiresAt,i.Version,
            status=i.AcceptedAt.HasValue?"accepted":i.RevokedAt.HasValue?"revoked":i.ExpiresAt<=now?"expired":!i.issuerValid?"invalidated":"pending"});
    }
    public async Task Revoke(int actor,Guid id,int version,CancellationToken ct) {
        await using var tx=await db.Database.BeginTransactionAsync(ct);await Owner(actor,ct);
        var item=await db.OrganizationInvitations.SingleOrDefaultAsync(i=>i.OrganizationId==db.OrganizationId && i.Id==id,ct) ?? throw Unavailable();
        if(item.Version!=version || item.AcceptedAt.HasValue || item.RevokedAt.HasValue)throw new ChatException(409,"Convite alterado; atualize a lista.");
        item.RevokedAt=Now;item.Version++;
        try{await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);}catch(DbUpdateConcurrencyException){throw new ChatException(409,"Convite alterado; atualize a lista.");}
        logger.LogInformation("OrganizationInvitationRevoked Organization={Organization} Actor={Actor} Invitation={Invitation}",db.OrganizationId,actor,id);
    }
    public async Task<object> Preview(int actor,string? token,CancellationToken ct) {
        var hash=Hash(token);var now=Now;
        var invitation=await Usable(now).SingleOrDefaultAsync(i=>i.TokenHash==hash,ct) ?? throw Unavailable();
        var organization=await db.Organizations.AsNoTracking().SingleAsync(o=>o.Id==invitation.OrganizationId,ct);
        return new{organization.Name,invitation.Role,invitation.ExpiresAt,
            alreadyMember=await db.OrganizationMemberships.AnyAsync(m=>m.OrganizationId==organization.Id && m.UserId==actor && m.IsActive,ct)};
    }
    public async Task<object> Accept(int actor,string? token,CancellationToken ct) {
        var hash=Hash(token);
        await using var tx=await db.Database.BeginTransactionAsync(ct);var now=Now;
        if(!await db.Users.AnyAsync(u=>u.UserId==actor && u.IsActive,ct))throw new ChatException(401,"Sessão inválida.");
        var invitation=await Usable(now).SingleOrDefaultAsync(i=>i.TokenHash==hash,ct) ?? throw Unavailable();
        var membership=await db.OrganizationMemberships.SingleOrDefaultAsync(m=>m.OrganizationId==invitation.OrganizationId && m.UserId==actor,ct);
        if(membership is {IsActive:false})throw Unavailable(); // do not undo a deliberate removal
        var alreadyMember=membership is not null;
        if(membership is null)db.OrganizationMemberships.Add(new(){OrganizationId=invitation.OrganizationId,UserId=actor,PolicyRole=invitation.Role});
        // Capability-derived target, never a body/header tenant override; no scope disabling.
        var changed=await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE OrganizationInvitations SET AcceptedAt={now},AcceptedByUserId={actor},Version=Version+1 WHERE Id={invitation.Id} AND TokenHash={hash} AND AcceptedAt IS NULL AND RevokedAt IS NULL AND ExpiresAt>{now}",ct);
        if(changed!=1)throw Unavailable();
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        logger.LogInformation("OrganizationInvitationAccepted Organization={Organization} Actor={Actor} Invitation={Invitation}",invitation.OrganizationId,actor,invitation.Id);
        return new{organizationId=invitation.OrganizationId,alreadyMember};
    }
}
