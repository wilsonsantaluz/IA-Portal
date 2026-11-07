using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Portal.AI.Gateway.Infrastructure;

namespace Portal.AI.Gateway.Infrastructure.Trino;

// Server-owned allowlist. No secret values in the file, SQLite, DTOs or logs.
public sealed class ConnectorProfile
{
    public string Id { get; set; } = "";
    public string OrganizationSlug { get; set; } = "";
    public string Category { get; set; } = "enterprise";
    public string Endpoint { get; set; } = "";
    public string Catalog { get; set; } = "";
    public string Schema { get; set; } = "";
    public string User { get; set; } = "";
    public string? PasswordEnvironment { get; set; }
    public string[] AllowedAddresses { get; set; } = [];
    public string[] Tables { get; set; } = [];
    public bool ReadOnlyAccount { get; set; }
    public bool AllowExternalLlm { get; set; }
    public string? SourceUrl { get; set; }
    public string? License { get; set; }
}

public sealed class ConnectorProfiles(IConfiguration config)
{
    public static readonly HttpRequestOptionsKey<string[]> Addresses = new("trino-approved-addresses");
    public static bool Identifier(string value) => Regex.IsMatch(value, "^[a-z][a-z0-9_]{0,62}$", RegexOptions.CultureInvariant);
    public IReadOnlyList<ConnectorProfile> All()
    {
        var profiles = config.GetSection("DataConnectors:Sources").Get<ConnectorProfile[]>() ?? [];
        if (profiles.Select(x => x.Id).Distinct().Count() != profiles.Length) throw new ChatException(409, "Perfis de fonte duplicados.");
        // A physical source tuple cannot be shared silently across organizations.
        if (profiles.GroupBy(x => new { x.Endpoint, x.Catalog, x.Schema }).Any(g => g.Select(x => x.OrganizationSlug).Distinct().Count() > 1))
            throw new ChatException(409, "Fontes devem ter catalogo/schema exclusivo por organizacao.");
        if (profiles.GroupBy(x => x.Endpoint).Any(g => g.Select(x => string.Join(",",x.AllowedAddresses.Order())).Distinct().Count() > 1))
            throw new ChatException(409,"Perfis do mesmo destino devem fixar os mesmos IPs.");
        foreach (var profile in profiles) Validate(profile);
        return profiles;
    }
    public ConnectorProfile Get(string id, string organizationSlug) => All().SingleOrDefault(x => x.Id == id && x.OrganizationSlug == organizationSlug)
        ?? throw new ChatException(409, "Fonte nao aprovada para esta organizacao.");

    private static void Validate(ConnectorProfile p)
    {
        if (!Identifier(p.Id) || !Identifier(p.Catalog) || !Identifier(p.Schema) || p.Tables.Length is < 1 or > 100 || p.Tables.Any(x => !Identifier(x))
            || !p.ReadOnlyAccount || p.User.Length is < 1 or > 80 || p.User.Any(char.IsControl)
            || p.Category is not ("demo" or "enterprise" or "public")
            || (p.Category == "demo" && p.OrganizationSlug != "atlas-demo"))
            throw new ChatException(409, "Perfil Trino invalido ou sem conta somente leitura declarada.");
        if (!Uri.TryCreate(p.Endpoint, UriKind.Absolute, out var uri) || uri.AbsolutePath != "/" || !string.IsNullOrEmpty(uri.Query + uri.Fragment + uri.UserInfo)
            || (uri.Scheme != "https" && !(p.Category == "demo" && uri.Scheme == "http")))
            throw new ChatException(409, "Endpoint Trino invalido; TLS e obrigatorio fora da demonstracao.");
        if (p.AllowedAddresses.Length == 0 || p.AllowedAddresses.Any(x => !IPAddress.TryParse(x, out var address) || Forbidden(address)))
            throw new ChatException(409, "Defina enderecos IP aprovados para o destino Trino.");
        if (p.PasswordEnvironment is not null && !Regex.IsMatch(p.PasswordEnvironment, "^TRINO_CONNECTOR_[A-Z0-9_]+_PASSWORD$"))
            throw new ChatException(409, "Referencia de credencial invalida.");
        if (p.PasswordEnvironment is not null && uri.Scheme != "https")
            throw new ChatException(409, "Credenciais exigem TLS.");
        if (p.Category == "public" && (string.IsNullOrWhiteSpace(p.License) || !Uri.TryCreate(p.SourceUrl,UriKind.Absolute,out var source) || source.Scheme != "https"))
            throw new ChatException(409, "Fonte publica exige origem HTTPS e licenca revisadas no servidor.");
    }
    private static bool Forbidden(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.IsIPv6Multicast || address.IsIPv6LinkLocal) return true;
        var bytes = address.GetAddressBytes();
        return address.AddressFamily == AddressFamily.InterNetwork && (bytes[0] >= 224 || bytes[0] == 0 || (bytes[0] == 169 && bytes[1] == 254)
            || address.ToString() is "168.63.129.16" or "100.100.100.200");
    }
    public static SocketsHttpHandler Handler() => new()
    {
        AllowAutoRedirect = false, UseProxy = false, MaxConnectionsPerServer = 2,
        ConnectCallback = async (context, ct) =>
        {
            if (!context.InitialRequestMessage.Options.TryGetValue(Addresses, out var approved)) throw new HttpRequestException("Destino nao aprovado.");
            var allowed = approved.Select(IPAddress.Parse).ToArray();
            var resolved = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
            var address = resolved.FirstOrDefault(ip => allowed.Contains(ip) && !Forbidden(ip))
                ?? throw new HttpRequestException("Destino nao aprovado.");
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try { await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), ct); return new NetworkStream(socket, ownsSocket: true); }
            catch { socket.Dispose(); throw; }
        }
    };
}
