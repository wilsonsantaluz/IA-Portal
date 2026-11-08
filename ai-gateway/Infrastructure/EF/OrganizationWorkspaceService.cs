using AiGateway.Dtos;
using AiGateway.Security;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Portal.AI.Gateway.Infrastructure;

public sealed record CreateOrganizationRequest([Required, StringLength(100, MinimumLength=2)] string Name,
    [Required, StringLength(48, MinimumLength=3)] string Slug);
public sealed record MembershipPolicyRequest([Required] string Role, bool IsActive, int Version);
public sealed record OwnershipTransferRequest(int TargetUserId,int OwnerVersion,int TargetVersion);

public sealed class OrganizationWorkspaceService(PortalDbContext db, AiProviderCatalog catalog,
    ILogger<OrganizationWorkspaceService> logger)
{
    public async Task<object> Create(int actor, CreateOrganizationRequest request, CancellationToken ct)
    {
        // Recheck permission against current DB; never infer installation privilege from a body.
        var access = await PortalAccess.ReadAsync(db,actor,ct,db.OrganizationId);
        if (!access.Permissions.Contains(PortalAccess.CreateOrganization)) throw new ChatException(403,"Sem permissão para criar organizações.");
        var name = request.Name.Trim(); var slug = request.Slug.Trim();
        if (name.Length is < 2 or > 100 || name.Any(char.IsControl) || !Regex.IsMatch(slug,"^[a-z][a-z0-9-]{2,47}$") || slug.EndsWith('-'))
            throw new ChatException(400,"Informe nome e identificador válidos (letras minúsculas, números e hífen).");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var organization = new Organization { Name=name, Slug=slug };
        db.Organizations.Add(organization);
        try {
            await db.SaveChangesAsync(ct);
            db.OrganizationMemberships.Add(new() { OrganizationId=organization.Id, UserId=actor, PolicyRole="Owner" });
            // New organization is server-created, not the active HTTP scope. Parameterized,
            // narrowly bounded initialization avoids switching/bypassing the request scope.
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Roles (OrganizationId,RoleName) VALUES ({organization.Id},'admin')",ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO UserRoles (UserId,RoleId) SELECT {actor},RoleId FROM Roles WHERE OrganizationId={organization.Id} AND RoleName='admin'",ct);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        } catch (DbUpdateException) { throw new ChatException(409,"Identificador indisponível; atualize e tente outro."); }
        logger.LogInformation("OrganizationCreated Organization={Organization} Actor={Actor}",organization.Id,actor);
        return new { organization.Id, organization.Name, organization.Slug, policyRole="Owner" };
    }

    public async Task ChangeMember(int actor, int userId, MembershipPolicyRequest request, CancellationToken ct)
    {
        if (request.Role is not ("Owner" or "Admin" or "Member" or "Viewer")) throw new ChatException(400,"Papel inválido.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var owner = await db.OrganizationMemberships.SingleOrDefaultAsync(m => m.OrganizationId==db.OrganizationId && m.UserId==actor && m.IsActive,ct);
        if (owner?.PolicyRole != "Owner") throw new ChatException(403,"Somente o Owner pode alterar papéis e associações.");
        var member = await db.OrganizationMemberships.SingleOrDefaultAsync(m => m.OrganizationId==db.OrganizationId && m.UserId==userId,ct)
            ?? throw new ChatException(404,"Membro não encontrado nesta organização.");
        if (member.Version != request.Version) throw new ChatException(409,"Associação alterada; atualize a página.");
        if(request.Role=="Owner" && member.PolicyRole!="Owner")throw new ChatException(400,"Use a transferência de propriedade para definir um novo Owner.");
        if (member.PolicyRole=="Owner" && member.IsActive && (!request.IsActive || request.Role!="Owner") &&
            !await db.OrganizationMemberships.AnyAsync(m => m.OrganizationId==db.OrganizationId && m.UserId!=userId && m.IsActive && m.PolicyRole=="Owner" && db.Users.Any(u => u.UserId==m.UserId && u.IsActive),ct))
            throw new ChatException(409,"Mantenha pelo menos um Owner ativo.");
        member.PolicyRole=request.Role; member.IsActive=request.IsActive; member.Version++;
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw new ChatException(409,"Associação alterada; atualize a página."); }
        logger.LogInformation("MembershipPolicyChanged Organization={Organization} Actor={Actor} Member={Member} Role={Role} Active={Active}",db.OrganizationId,actor,userId,request.Role,request.IsActive);
    }

    public async Task<object> Default(CancellationToken ct)
    {
        var item=await db.OrganizationAiDefaults.AsNoTracking().SingleOrDefaultAsync(x=>x.OrganizationId==db.OrganizationId,ct);
        return new { item?.Provider, item?.Model, version=item?.Version ?? 0, inherited=item is null };
    }

    public async Task TransferOwnership(int actor,OwnershipTransferRequest request,CancellationToken ct) {
        if(actor==request.TargetUserId)throw new ChatException(400,"Selecione outro membro ativo.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var owner=await db.OrganizationMemberships.SingleOrDefaultAsync(m=>m.OrganizationId==db.OrganizationId && m.UserId==actor && m.IsActive && m.PolicyRole=="Owner",ct);
        if(owner is null)throw new ChatException(403,"Somente Owner pode transferir a propriedade.");
        var target=await db.OrganizationMemberships.SingleOrDefaultAsync(m=>m.OrganizationId==db.OrganizationId && m.UserId==request.TargetUserId && m.IsActive && db.Users.Any(u=>u.UserId==m.UserId && u.IsActive),ct)
            ?? throw new ChatException(404,"Membro ativo não encontrado nesta organização.");
        if(owner.Version!=request.OwnerVersion || target.Version!=request.TargetVersion)throw new ChatException(409,"Associação alterada; atualize a página.");
        if(target.PolicyRole=="Owner")throw new ChatException(409,"O membro já é Owner.");
        target.PolicyRole="Owner";target.Version++;owner.PolicyRole="Admin";owner.Version++;
        try{await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);}catch(DbUpdateConcurrencyException){throw new ChatException(409,"Associação alterada; atualize a página.");}
        logger.LogInformation("OrganizationOwnershipTransferred Organization={Organization} Actor={Actor} NewOwner={NewOwner}",db.OrganizationId,actor,request.TargetUserId);
    }

    public async Task SetDefault(int actor, DefaultUpdate request, CancellationToken ct)
    {
        var access=await PortalAccess.ReadAsync(db,actor,ct,db.OrganizationId);
        if (!access.Permissions.Contains(PortalAccess.Organizations)) throw new ChatException(403,"Sem permissão para configurar a organização.");
        await catalog.Resolve(request.Provider,request.Model,ct); // installed, enabled, server-approved models only
        var item=await db.OrganizationAiDefaults.SingleOrDefaultAsync(x=>x.OrganizationId==db.OrganizationId,ct);
        if ((item?.Version ?? 0)!=request.Version) throw new ChatException(409,"Padrão alterado; atualize a página.");
        if (item is null) { item=new() {OrganizationId=db.OrganizationId};db.OrganizationAiDefaults.Add(item); }
        item.Provider=request.Provider;item.Model=request.Model;item.Version++;
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { throw new ChatException(409,"Padrão alterado; atualize a página."); }
        logger.LogInformation("OrganizationModelChanged Organization={Organization} Actor={Actor}",db.OrganizationId,actor);
    }
}
