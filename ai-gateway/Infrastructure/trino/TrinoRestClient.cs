using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AiGateway.Application.Interfaces;

namespace Portal.AI.Gateway.Infrastructure.Trino;

public sealed class TrinoRestClient(IConfiguration config, ILogger<TrinoRestClient> logger,
    IHttpClientFactory factory) : ITrinoRestClient
{
    private static readonly SemaphoreSlim GlobalQueries = new(8,8);
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> TenantQueries = new();

    // Legacy SQL paths do not have a connector/tenant policy and must fail closed.
    public Task<TrinoResponseData> ExecuteQueryAsync(string sql, string schema = "financeiro", CancellationToken ct = default)
        => throw new ChatException(409, "SQL livre desabilitado. Use uma fonte autorizada em Data Connectors.");

    public async Task<TrinoResponseData> ExecuteApprovedAsync(ConnectorProfile profile, ApprovedTrinoQuery query, int organizationId, CancellationToken ct)
    {
        if (organizationId <= 0) throw new ChatException(403,"Organizacao obrigatoria.");
        var endpoint = new Uri(profile.Endpoint);
        var tenant = TenantQueries.GetOrAdd(organizationId,_ => new SemaphoreSlim(2,2));
        if (!await tenant.WaitAsync(0,ct)) throw new ChatException(429,"Limite de consultas simultaneas atingido.");
        bool granted;
        try { granted = await GlobalQueries.WaitAsync(0,ct); }
        catch { tenant.Release(); throw; }
        if (!granted) { tenant.Release(); throw new ChatException(429,"Limite global de consultas atingido."); }
        var trace = Guid.NewGuid().ToString("N");
        Uri? next = null;
        HttpClient? client = null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        HttpRequestMessage Request(HttpMethod method, Uri uri)
        {
            var request = new HttpRequestMessage(method,uri);
            request.Options.Set(ConnectorProfiles.Addresses, profile.AllowedAddresses);
            request.Headers.Add("X-Trino-User",profile.User);
            request.Headers.Add("X-Trino-Catalog",profile.Catalog);
            request.Headers.Add("X-Trino-Schema",profile.Schema);
            request.Headers.Add("X-Trino-Source","IA-Portal");
            request.Headers.Add("X-Trino-Trace-Token",trace);
            request.Headers.Add("X-Trino-Session","query_max_run_time=15s");
            if (profile.PasswordEnvironment is not null)
            {
                var secret = config[profile.PasswordEnvironment];
                if (string.IsNullOrWhiteSpace(secret)) { request.Dispose(); throw new ChatException(409,"Credencial da fonte nao configurada."); }
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic",Convert.ToBase64String(Encoding.UTF8.GetBytes(profile.User + ":" + secret)));
            }
            return request;
        }
        try
        {
            client = factory.CreateClient("trino-secure");
            var result = new TrinoResponseData();
            var uri = new Uri(endpoint,"v1/statement");
            var totalBytes = 0;
            for (var page = 0; page < 100; page++)
            {
                using var request = Request(page == 0 ? HttpMethod.Post : HttpMethod.Get, uri);
                if (page == 0) request.Content = new StringContent(query.Sql,Encoding.UTF8,"text/plain");
                using var response = await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
                if (!response.IsSuccessStatusCode) throw new ChatException(502,"Fonte Trino indisponivel ou acesso negado.");
                using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
                using var buffer = new MemoryStream();
                var chunk = new byte[8192];
                while (true)
                {
                    var count = await stream.ReadAsync(chunk,timeout.Token); if (count == 0) break;
                    totalBytes += count;
                    if (totalBytes > 8_388_608 || buffer.Length + count > 2_097_152) throw new ChatException(502,"Resposta da fonte excede o limite permitido.");
                    buffer.Write(chunk,0,count);
                }
                using var packet = JsonDocument.Parse(buffer.ToArray());
                if (packet.RootElement.TryGetProperty("nextUri",out var link) && link.ValueKind == JsonValueKind.String)
                {
                    var proposed = new Uri(link.GetString()!,UriKind.Absolute);
                    if (proposed.Scheme != endpoint.Scheme || proposed.Authority != endpoint.Authority || !string.IsNullOrEmpty(proposed.UserInfo + proposed.Fragment)
                        || !proposed.AbsolutePath.StartsWith("/v1/statement/",StringComparison.Ordinal))
                        throw new ChatException(502,"Destino de continuacao Trino rejeitado.");
                    next = proposed;
                }
                else next = null;
                if (packet.RootElement.TryGetProperty("error",out _)) throw new ChatException(502,"Consulta rejeitada pela fonte Trino.");
                var data = JsonSerializer.Deserialize<TrinoRawResponse>(buffer.ToArray()) ?? throw new JsonException();
                if (result.Rows.Count + (data.Data?.Count ?? 0) > query.RowLimit) throw new ChatException(502,"Fonte excedeu o limite de linhas do plano.");
                result.AddBatch(data.Columns,data.Data);
                // nextUri, not stats.state, determines completion in the Trino REST protocol.
                if (next is null)
                {
                    logger.LogInformation("Trino {TraceId}: org={OrganizationId}, plano={Plan}, linhas={Rows}, status=completed",trace,organizationId,query.Kind,result.Rows.Count);
                    return result;
                }
                uri = next;
            }
            throw new ChatException(502,"Limite de paginas Trino excedido.");
        }
        catch (ChatException) { throw; }
        catch (OperationCanceledException) { throw new ChatException(504,"Consulta cancelada ou tempo limite atingido."); }
        catch { throw new ChatException(502,"Nao foi possivel consultar a fonte Trino."); }
        finally
        {
            if (next is not null && client is not null)
            {
                try { using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(2)); using var request = Request(HttpMethod.Delete,next); using var response = await client.SendAsync(request,cancel.Token); }
                catch { /* The server time limit is a second bound if cancellation fails. */ }
            }
            logger.LogInformation("Trino {TraceId}: org={OrganizationId}, plano={Plan}, encerrada",trace,organizationId,query.Kind);
            client?.Dispose();
            GlobalQueries.Release(); tenant.Release();
        }
    }
}
