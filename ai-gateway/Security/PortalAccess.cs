using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Infrastructure;

namespace AiGateway.Security;

public static class PortalAccess
{
    public const string UsageAdmin = "usage.admin";
    public const string AiAdministration = "ai.manage";
    public const string Chat = "chat.access";
    public const string Dashboard = "dashboard.view";
    public const string Knowledge = "kb.view";
    public const string EditKnowledge = "kb.manage";
    public const string Administration = "access.manage";
    public static readonly (string Key, string Screen, string Description)[] Catalog =
    [
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

    public static async Task<(string[] Roles, string[] Permissions)> ReadAsync(PortalDbContext db, int userId, CancellationToken ct)
    {
        var roleIds = db.UserRoles.Where(x => x.UserId == userId).Select(x => x.RoleId);
        var roles = await db.Roles.Where(x => roleIds.Contains(x.RoleId)).Select(x => x.RoleName).ToArrayAsync(ct);
        var permissions = roles.Contains("admin") ? Catalog.Select(x => x.Key).ToArray() :
            await db.RolePermissions.Where(x => roleIds.Contains(x.RoleId)).Select(x => x.Permission.PermissionKey).Distinct().ToArrayAsync(ct);
        if (permissions.Contains(EditKnowledge) && !permissions.Contains(Knowledge)) permissions = [.. permissions, Knowledge];
        return (roles, permissions);
    }
}
