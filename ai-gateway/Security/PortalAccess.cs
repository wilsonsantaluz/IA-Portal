using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Infrastructure;

namespace AiGateway.Security;

public static class PortalAccess
{
    public const string Organizations = "organizations.manage";
    public const string CreateOrganization = "organizations.create";
    public const string Connectors = "connectors.manage";
    public const string DataQuery = "data.query";
    public const string UsageAdmin = "usage.admin";
    public const string AiAdministration = "ai.manage";
    public const string Chat = "chat.access";
    public const string Dashboard = "dashboard.view";
    public const string Knowledge = "kb.view";
    public const string EditKnowledge = "kb.manage";
    public const string Administration = "access.manage";
    public static readonly (string Key, string Screen, string Description)[] Catalog =
    [
        (Organizations, "Organizações", "Configurar a organização e seu modelo padrão"),
        (CreateOrganization, "Organizações", "Criar organizações na instalação"),
        (Connectors, "Data Connectors", "Gerenciar conectores e fontes autorizadas da organizacao"),
        (DataQuery, "Dados analiticos", "Consultar fontes explicitamente concedidas ao grupo"),
        (UsageAdmin, "Consumo de IA", "Consultar consumo agregado de todos os usuários, sem acesso ao conteúdo das conversas"),
        (AiAdministration, "Configurações de IA", "Configurar provedores, modelos e padrão global; testar conectividade sem expor segredos"),
        (Chat, "Chat IA", "Conversar, consultar o próprio histórico e analisar documentos"),
        (Dashboard, "Dashboard", "Consultar indicadores de qualidade e avaliações"),
        (Knowledge, "Base de conhecimento", "Consultar conteúdos"),
        (EditKnowledge, "Base de conhecimento", "Criar, editar, ativar e reindexar conteúdos (inclui consulta)"),
        (Administration, "Usuários e grupos", "Administrar usuários, senhas, grupos e permissões")
    ];

    public static int UserId(ClaimsPrincipal principal) => int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    public static string Version(User user, IConfiguration cfg) => Convert.ToHexString(
        HMACSHA256.HashData(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!), Encoding.UTF8.GetBytes(user.PasswordHash)));

    public static Task<int> DefaultOrganizationAsync(PortalDbContext db, int userId, IConfiguration config, CancellationToken ct)
    {
        var preferAtlas = config.GetValue<bool>("Demo:Atlas:PreferOrganization");
        return db.OrganizationMemberships.Where(m => m.UserId == userId && m.IsActive)
            .Join(db.Organizations.Where(o => o.IsActive), m => m.OrganizationId, o => o.Id, (m,o) => o)
            .OrderBy(o => preferAtlas && o.Slug == "atlas-demo" ? 0 : 1).ThenBy(o => o.Id)
            .Select(o => o.Id).FirstOrDefaultAsync(ct);
    }

    public static async Task<(string[] Roles, string[] Permissions)> ReadAsync(PortalDbContext db, int userId, CancellationToken ct, int organizationId = 1)
    {
        var membership = await db.OrganizationMemberships.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId && x.OrganizationId == organizationId && x.IsActive, ct);
        if (membership is null) return ([], []);
        var roleIds = db.UserRoles.IgnoreQueryFilters().Where(x => x.UserId == userId && x.Role.OrganizationId == organizationId).Select(x => x.RoleId);
        var roles = await db.Roles.IgnoreQueryFilters().Where(x => x.OrganizationId == organizationId && roleIds.Contains(x.RoleId)).Select(x => x.RoleName).ToArrayAsync(ct);
        var permissions = roles.Contains("admin") ? Catalog.Select(x => x.Key).ToArray() :
            await db.RolePermissions.IgnoreQueryFilters().Where(x => x.Role.OrganizationId == organizationId && roleIds.Contains(x.RoleId)).Select(x => x.Permission.PermissionKey).Distinct().ToArrayAsync(ct);
        // Explicit policies override group grants. Null preserves the existing custom RBAC.
        if (membership.PolicyRole is not null) {
            roles = [membership.PolicyRole];
            permissions = membership.PolicyRole switch {
                "Owner" or "Admin" => Catalog.Select(x => x.Key).ToArray(),
                "Member" => [Chat, Knowledge, EditKnowledge, Dashboard, DataQuery],
                "Viewer" => [Knowledge],
                _ => []
            };
        }
        // Provider configuration belongs to the installation, not individual tenants.
        if (organizationId != 1) permissions = permissions.Except([AiAdministration, CreateOrganization]).ToArray();
        if (permissions.Contains(EditKnowledge) && !permissions.Contains(Knowledge)) permissions = [.. permissions, Knowledge];
        return (roles, permissions);
    }
}
