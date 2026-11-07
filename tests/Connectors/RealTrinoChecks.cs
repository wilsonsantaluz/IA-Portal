using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Portal.AI.Gateway.Infrastructure.Trino;

static class RealTrinoChecks
{
    public static async Task Run()
    {
        var profile = new ConnectorProfile { Id="atlas_local",OrganizationSlug="atlas-demo",Category="demo",Endpoint="http://127.0.0.1:18081/",Catalog="memory",Schema="atlas",User="atlas_reader",AllowedAddresses=["127.0.0.1"],Tables=["invoices","payments"],ReadOnlyAccount=true };
        var client = new TrinoRestClient(new ConfigurationBuilder().Build(),NullLogger<TrinoRestClient>.Instance,new RealFactory());
        var reference = new DateOnly(2026,11,1);
        using var golden = JsonDocument.Parse(await File.ReadAllTextAsync("trino/atlas/expected-v1.json"));
        var expected = new Dictionary<string,long> { ["01-15"]=15605108,["16-30"]=1140108,["31-60"]=11006177,["61-90"]=9354212,[">90"]=60303330 };
        foreach (var kind in new[] { "aging","aging_trend","cashflow","top_balances" })
        {
            var rows = (await client.ExecuteApprovedAsync(profile,ApprovedTrinoQuery.Analytics(profile,kind,reference,profile.Tables),2,default)).Rows;
            if (kind=="aging") {
                if (rows.Count!=5) throw new Exception("Aging row count");
                foreach (var row in rows) if (((JsonElement)row["saldo_centavos"]).GetInt64()!=expected[row["faixa"].ToString()!]) throw new Exception("Aging mismatch");
            }
            else if (rows.Count != (kind=="top_balances" ? 10 : 6)) throw new Exception("Financial plan row count");
            if (kind is "cashflow" or "aging_trend")
                foreach (var expectedRow in golden.RootElement.GetProperty(kind).EnumerateArray()) {
                    var row=rows.Single(r => r["mes"].ToString()==expectedRow.GetProperty("mes").GetString());
                    foreach (var property in expectedRow.EnumerateObject().Where(p => p.Name!="mes"))
                        if (((JsonElement)row[property.Name]).GetInt64()!=property.Value.GetInt64()) throw new Exception("Independent offline financial reconciliation failed");
                }
            Console.WriteLine($"PASS: real Trino {kind}, rows={rows.Count}");
        }
        var metadata = await client.ExecuteApprovedAsync(profile,ApprovedTrinoQuery.Metadata(profile),2,default);
        if (metadata.Rows.Count<10 || metadata.Rows.Any(r => r["table_name"].ToString() is not ("invoices" or "payments"))) throw new Exception("Metadata allowlist");
        // This check is deliberately fixed to the isolated loopback demo server.
        using var http=new HttpClient();
        foreach (var sql in new[] { "CREATE TABLE memory.atlas.forbidden_write (id BIGINT)", "SELECT * FROM system.runtime.nodes", "SELECT * FROM memory.atlas.atlas_manifest" })
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,profile.Endpoint+"v1/statement") {Content=new StringContent(sql)};
            request.Headers.Add("X-Trino-User","atlas_reader");
            using var response=await http.SendAsync(request);
            var packet=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var denied=false;
            for(var page=0;page<100;page++)
            {
                if(packet.RootElement.TryGetProperty("error",out var error)) {denied=error.GetProperty("errorName").GetString()=="PERMISSION_DENIED" || error.GetProperty("errorName").GetString()=="ACCESS_DENIED";break;}
                if(!packet.RootElement.TryGetProperty("nextUri",out var next)) break;
                var uri=new Uri(next.GetString()!);
                if(uri.Scheme!="http" || uri.Authority!="127.0.0.1:18081" || !uri.AbsolutePath.StartsWith("/v1/statement/")) throw new Exception("Unexpected nextUri");
                using var poll=new HttpRequestMessage(HttpMethod.Get,uri);poll.Headers.Add("X-Trino-User","atlas_reader");
                using var batch=await http.SendAsync(poll);packet.Dispose();packet=JsonDocument.Parse(await batch.Content.ReadAsStringAsync());
            }
            packet.Dispose();if(!denied) throw new Exception("Read-only/cross-source policy did not deny query");
        }
        Console.WriteLine("PASS: real Trino authorized column discovery; server denies write, system catalog and non-allowlisted table.");
    }
    sealed class RealFactory : IHttpClientFactory { public HttpClient CreateClient(string name)=>new(ConnectorProfiles.Handler()); }
}
