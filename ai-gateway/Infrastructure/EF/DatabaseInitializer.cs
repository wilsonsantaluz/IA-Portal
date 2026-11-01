using AiGateway.Application.SeedWork;
using Microsoft.EntityFrameworkCore;
using AiGateway.Security;

namespace Portal.AI.Gateway.Infrastructure;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(PortalDbContext db, IConfiguration config)
    {
        // Local prototype: creates a new schema, never deletes existing data.
        // Schema evolution must be handled explicitly before changing this model.
        await db.Database.EnsureCreatedAsync();
        if (!await db.KnowledgeBaseItems.AnyAsync(x => x.KeyName == "organization_base"))
        {
            db.KnowledgeBaseItems.Add(new KnowledgeBaseItem
            {
                KeyName = "organization_base", Section = "Conhecimento corporativo",
                Title = "Orientações iniciais", Content = KnowledgeBaseSeed.Content,
                IsActive = true, CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        // Idempotent catalog: reuse the existing RBAC schema without deleting data.
        var role = await db.Roles.FirstOrDefaultAsync(x => x.RoleName == "admin");
        if (role is null) { role = new Role { RoleName = "admin" }; db.Roles.Add(role); }
        foreach (var entry in PortalAccess.Catalog)
            if (!await db.Permissions.AnyAsync(x => x.PermissionKey == entry.Key))
                db.Permissions.Add(new Permission { PermissionKey = entry.Key, Description = entry.Description });
        await db.SaveChangesAsync();
        var catalogKeys = PortalAccess.Catalog.Select(x => x.Key).ToArray();
        foreach (var permission in await db.Permissions.Where(x => catalogKeys.Contains(x.PermissionKey)).ToListAsync())
            if (!await db.RolePermissions.AnyAsync(x => x.RoleId == role.RoleId && x.PermissionId == permission.PermissionId))
                db.RolePermissions.Add(new RolePermission { RoleId = role.RoleId, PermissionId = permission.PermissionId });
        await db.SaveChangesAsync();

        // Only bootstrap an empty installation; never reset an existing password.
        if (await db.Users.AnyAsync()) return;
        var email = config["Bootstrap:AdminEmail"]?.Trim();
        var password = config["Bootstrap:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password)) return;
        if (password.Length < 12)
            throw new InvalidOperationException("Bootstrap:AdminPassword deve ter pelo menos 12 caracteres.");

        var user = new User
        {
            UserName = "Administrador", UserEmail = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        user.Roles.Add(new UserRole { User = user, Role = role });
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }
}
