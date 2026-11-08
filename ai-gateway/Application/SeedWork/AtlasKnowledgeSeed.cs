using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Infrastructure;

namespace AiGateway.Application.SeedWork;

/// <summary>Insert-only, opt-in snapshot. Existing keys always belong to the operator.</summary>
public static class AtlasKnowledgeSeed
{
    public static async Task InitializeAsync(PortalDbContext db, IConfiguration config,
        CancellationToken ct = default)
    {
        if (!config.GetValue<bool>("Demo:Atlas:SeedKnowledge")) return;
        using var stream = typeof(AtlasKnowledgeSeed).Assembly.GetManifestResourceStream(
            "AiGateway.Application.SeedWork.atlas-v1.json")
            ?? throw new InvalidOperationException("Snapshot Atlas v1 ausente.");
        var items = await JsonSerializer.DeserializeAsync<List<KnowledgeBaseItem>>(stream, cancellationToken: ct)
            ?? throw new InvalidOperationException("Snapshot Atlas v1 inválido.");
        if (items.Count != 10 || items.Select(x => x.KeyName).Distinct().Count() != 10)
            throw new InvalidOperationException("Snapshot Atlas deve conter dez documentos distintos.");

        // SQLite serializes writers. A single transaction also prevents partial seeds.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var organization = await db.Organizations.SingleOrDefaultAsync(x => x.Slug == "atlas-demo", ct);
        if (organization is null)
        {
            organization = new Organization { Name = "Atlas Serviços Digitais (demonstração)", Slug = "atlas-demo" };
            db.Organizations.Add(organization);
            await db.SaveChangesAsync(ct);
        }
        var role = await db.Roles.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.OrganizationId == organization.Id && x.RoleName == "admin", ct);
        if (role is null)
        {
            role = new Role { OrganizationId = organization.Id, RoleName = "admin" };
            db.Roles.Add(role);
            await db.SaveChangesAsync(ct);
        }
        // Explicit demo opt-in grants default-organization admins access to Atlas only.
        // Ordinary users must be provisioned by an Atlas administrator.
        var administrators = await db.UserRoles.IgnoreQueryFilters().Where(x => x.Role.OrganizationId == 1 && x.Role.RoleName == "admin")
            .Select(x => x.UserId).ToListAsync(ct);
        foreach (var userId in administrators)
        {
            if (!await db.OrganizationMemberships.AnyAsync(x => x.OrganizationId == organization.Id && x.UserId == userId, ct))
            {
                var hasOwner = await db.OrganizationMemberships.AnyAsync(x => x.OrganizationId == organization.Id && x.PolicyRole == "Owner" && x.IsActive, ct);
                db.OrganizationMemberships.Add(new OrganizationMembership { OrganizationId = organization.Id, UserId = userId, PolicyRole = hasOwner ? "Admin" : "Owner" });
                if (!await db.UserRoles.IgnoreQueryFilters().AnyAsync(x => x.UserId == userId && x.RoleId == role.RoleId, ct))
                    db.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.RoleId });
            }
        }
        var keys = items.Select(x => x.KeyName).ToArray();
        var existing = await db.KnowledgeBaseItems.IgnoreQueryFilters().Where(x => x.OrganizationId == organization.Id && keys.Contains(x.KeyName))
            .Select(x => x.KeyName).ToListAsync(ct);
        foreach (var item in items.Where(x => !existing.Contains(x.KeyName)))
        {
            item.OrganizationId = organization.Id;
            item.IsActive = true;
            item.CreatedAt = DateTime.UtcNow;
            db.KnowledgeBaseItems.Add(item);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        // Vector indexing is explicit via the existing authenticated reindex endpoint.
        // No model downloads or network calls are required for local knowledge lookup.
    }
}
