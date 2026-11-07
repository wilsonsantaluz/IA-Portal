using System.Text.Json;
using AiGateway.Security;
using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Infrastructure;

namespace Portal.AI.Gateway.Infrastructure.Trino;

public sealed record ConnectorUpdate(string Name, string ProfileId, bool Enabled, string[] Tables, int[] GroupIds, int Version);
public sealed class DataConnectorService(PortalDbContext db, ConnectorProfiles profiles,
    ITrinoQueryService queries, ITrinoMetadataService metadata, IHttpContextAccessor http)
{
    public async Task<object> Groups(CancellationToken ct) => await db.Roles.OrderBy(x => x.RoleName).Select(x => new { id = x.RoleId, name = x.RoleName }).ToListAsync(ct);
    public async Task<ConnectorProfile> Profile(string profileId, CancellationToken ct)
    {
        var slug = await db.Organizations.Where(x => x.Id == db.OrganizationId && x.IsActive).Select(x => x.Slug).SingleAsync(ct);
        return profiles.Get(profileId,slug);
    }
    public async Task<object> Profiles(CancellationToken ct)
    {
        var slug = await db.Organizations.Where(x => x.Id == db.OrganizationId).Select(x => x.Slug).SingleAsync(ct);
        return profiles.All().Where(x => x.OrganizationSlug == slug).Select(x => new {
            x.Id,x.Category,x.Endpoint,x.Catalog,x.Schema,x.Tables,x.SourceUrl,x.License,
            authentication = x.PasswordEnvironment is null ? "none" : "environment-basic"
        }).ToList();
    }
    public async Task<DataConnector> Find(int id, CancellationToken ct) => await db.DataConnectors.FirstOrDefaultAsync(x => x.Id == id,ct)
        ?? throw new ChatException(404,"Conector nao encontrado.");
    public async Task<ConnectorProfile> Resolve(DataConnector connector, CancellationToken ct)
    {
        var profile = await Profile(connector.ProfileId,ct);
        if (profile.Endpoint != connector.Endpoint || profile.Catalog != connector.Catalog || profile.Schema != connector.Schema || profile.Category != connector.Category)
            throw new ChatException(409,"Perfil da fonte mudou. Revise e salve a conexao novamente.");
        return profile;
    }
    public async Task<object> List(bool administrative, CancellationToken ct)
    {
        var userId = PortalAccess.UserId(http.HttpContext!.User);
        var query = db.DataConnectors.AsNoTracking();
        if (!administrative) query = query.Where(x => x.Enabled && db.ConnectorGrants.Any(g => g.ConnectorId == x.Id && db.UserRoles.Any(r => r.RoleId == g.RoleId && r.UserId == userId)));
        var rows = await query.OrderBy(x => x.Name).ToListAsync(ct);
        var grants = administrative ? await db.ConnectorGrants.ToListAsync(ct) : [];
        return rows.Select(x => new { x.Id,x.Name,x.ProfileId,x.Category,x.Endpoint,x.Catalog,x.Schema,x.Enabled,
            tables = JsonSerializer.Deserialize<string[]>(x.TablesJson), x.Status,x.TestedAt,x.Version,
            groupIds = grants.Where(g => g.ConnectorId == x.Id).Select(g => g.RoleId) }).ToList();
    }
    public async Task<int> Save(int? id, ConnectorUpdate request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100 || request.Tables is null || request.GroupIds is null
            || request.Tables.Length > 100 || request.Tables.Any(x => !ConnectorProfiles.Identifier(x)))
            throw new ChatException(400,"Dados do conector invalidos.");
        var profile = await Profile(request.ProfileId,ct);
        if (request.Tables.Except(profile.Tables).Any()) throw new ChatException(400,"Tabela fora da allowlist da fonte.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var groupIds = request.GroupIds.Distinct().ToArray();
        if (await db.Roles.CountAsync(x => groupIds.Contains(x.RoleId),ct) != groupIds.Length) throw new ChatException(400,"Grupo nao autorizado.");
        var connector = id is int existing ? await Find(existing,ct) : new DataConnector();
        if (id.HasValue && connector.Version != request.Version) throw new ChatException(409,"Conector alterado. Atualize a tela.");
        connector.Name = request.Name.Trim(); connector.ProfileId = profile.Id; connector.Category = profile.Category;
        connector.Endpoint = profile.Endpoint; connector.Catalog = profile.Catalog; connector.Schema = profile.Schema;
        connector.Enabled = request.Enabled; connector.TablesJson = JsonSerializer.Serialize(request.Tables.Distinct());
        connector.Status = "not_tested"; connector.TestedAt = null; connector.Version++;
        if (!id.HasValue) db.DataConnectors.Add(connector);
        await db.SaveChangesAsync(ct);
        db.ConnectorGrants.RemoveRange(await db.ConnectorGrants.Where(x => x.ConnectorId == connector.Id).ToListAsync(ct));
        await db.SaveChangesAsync(ct);
        foreach (var groupId in groupIds) db.ConnectorGrants.Add(new ConnectorGrant { ConnectorId = connector.Id, RoleId = groupId });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return connector.Id;
    }
    public async Task<object> Test(int id, CancellationToken ct)
    {
        var connector = await Find(id,ct);
        var version = connector.Version;
        string status;
        try { await queries.QueryApprovedAsync(await Resolve(connector,ct),ApprovedTrinoQuery.Probe(),db.OrganizationId,ct); status = "connected"; }
        catch (ChatException e) { status = e.Status == 504 ? "timeout" : e.Status == 409 ? "not_configured" : "unavailable"; }
        var testedAt = DateTime.UtcNow;
        await db.DataConnectors.Where(x => x.Id == id && x.Version == version).ExecuteUpdateAsync(u => u.SetProperty(x => x.Status,status).SetProperty(x => x.TestedAt,testedAt),ct);
        return new { status, testedAt };
    }
    public async Task<object> Discover(int id, CancellationToken ct)
    {
        var connector = await Find(id,ct);
        return await metadata.GetAuthorizedMetadataAsync(await Resolve(connector,ct),db.OrganizationId,ct);
    }
    public async Task<(DataConnector Connector, ConnectorProfile Profile)> Authorize(int id, CancellationToken ct)
    {
        var user = http.HttpContext!.User;
        if (!user.HasClaim("perm",PortalAccess.DataQuery)) throw new ChatException(403,"Sem permissao para consultar dados.");
        var connector = await Find(id,ct); var userId = PortalAccess.UserId(user);
        if (!connector.Enabled || !await db.ConnectorGrants.AnyAsync(g => g.ConnectorId == id && db.UserRoles.Any(r => r.RoleId == g.RoleId && r.UserId == userId),ct))
            throw new ChatException(404,"Fonte nao disponivel para consulta.");
        return (connector,await Resolve(connector,ct));
    }
    public async Task<object> Analytics(int id, string kind, DateOnly reference, CancellationToken ct)
    {
        var (connector,profile) = await Authorize(id,ct);
        ApprovedTrinoQuery plan;
        try { plan = ApprovedTrinoQuery.Analytics(profile,kind,reference,JsonSerializer.Deserialize<string[]>(connector.TablesJson) ?? []); }
        catch (ArgumentException) { throw new ChatException(400,"Plano/data/tabelas nao suportados por esta fonte."); }
        var rows = await queries.QueryApprovedAsync(profile,plan,db.OrganizationId,ct);
        return new { source = connector.Name, connectorId = id, kind, referenceDate = reference, currency = "BRL", unit = "cents",
            queriedAt = DateTime.UtcNow, rows, sql = plan.Sql, synthetic = connector.Category == "demo", profile.SourceUrl,profile.License };
    }
}
