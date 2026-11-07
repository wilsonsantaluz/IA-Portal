using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Portal.AI.Gateway.Infrastructure;
using Portal.AI.Gateway.Infrastructure.Trino;

void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
async Task Rejected(Func<Task> action, int status) { try { await action(); throw new Exception("Expected failure"); } catch (ChatException e) { Check(e.Status == status,$"Unexpected status {e.Status}"); } }
var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
    ["DataConnectors:Sources:0:Id"]="atlas_local", ["DataConnectors:Sources:0:OrganizationSlug"]="atlas-demo",
    ["DataConnectors:Sources:0:Category"]="demo", ["DataConnectors:Sources:0:Endpoint"]="http://127.0.0.1:18081/",
    ["DataConnectors:Sources:0:Catalog"]="memory", ["DataConnectors:Sources:0:Schema"]="atlas",
    ["DataConnectors:Sources:0:User"]="atlas_reader", ["DataConnectors:Sources:0:ReadOnlyAccount"]="true",
    ["DataConnectors:Sources:0:Tables:0"]="invoices", ["DataConnectors:Sources:0:Tables:1"]="payments",
    ["DataConnectors:Sources:0:AllowedAddresses:0"]="127.0.0.1"
}).Build();
var registry = new ConnectorProfiles(config);
var profile = registry.Get("atlas_local","atlas-demo");
await MigrationChecks.Run();
try { registry.Get("atlas_local","default"); throw new Exception("Cross-org profile"); } catch (ChatException) { }
var reference = new DateOnly(2026,11,1);
foreach (var malicious in new[] { "DROP TABLE invoices", "SELECT 1; DELETE FROM invoices", "WITH x AS (DELETE FROM invoices) SELECT * FROM x", "SELECT * FROM system.runtime.nodes", "SELECT http_get('http://169.254.169.254')" })
{
    try { ApprovedTrinoQuery.Analytics(profile,malicious,reference,profile.Tables); throw new Exception("Accepted SQL text"); } catch (ArgumentException) { }
}
foreach (var kind in new[] { "aging","cashflow","top_balances" })
{
    var plan = ApprovedTrinoQuery.Analytics(profile,kind,reference,profile.Tables);
    Check(plan.Sql.StartsWith("WITH ") && !plan.Sql.Contains(';') && plan.Sql.Contains("\"memory\".\"atlas\".\"invoices\"") && plan.RowLimit <= 10,"Unbounded or unqualified plan");
}
try { ApprovedTrinoQuery.Analytics(profile,"aging",reference,["invoices"]); throw new Exception("Missing table accepted"); } catch (ArgumentException) { }
var invalid = profile.Catalog; profile.Catalog = "memory; DROP SCHEMA atlas";
try { ApprovedTrinoQuery.Metadata(profile); throw new Exception("Identifier injection"); } catch (ArgumentException) { }
profile.Catalog = invalid;

var factory = new FakeFactory();
var client = new TrinoRestClient(config,NullLogger<TrinoRestClient>.Instance,factory);
await Rejected(async () => { await client.ExecuteQueryAsync("SELECT 1; DELETE FROM invoices"); },409);
factory.Packet = "{\"columns\":[{\"name\":\"connected\",\"type\":\"integer\"}],\"data\":[[1]],\"stats\":{\"state\":\"FINISHED\"}}";
Check((await client.ExecuteApprovedAsync(profile,ApprovedTrinoQuery.Probe(),2,default)).Rows.Count == 1,"Probe failed");
factory.Packet = "{\"nextUri\":\"http://169.254.169.254/latest/meta-data\"}";
var before = factory.Requests.Count;
await Rejected(async () => { await client.ExecuteApprovedAsync(profile,ApprovedTrinoQuery.Probe(),2,default); },502);
Check(factory.Requests.Count == before+1,"Followed malicious nextUri");
factory.Packet = "{\"columns\":[{\"name\":\"x\"}],\"data\":[[1],[2]]}";
await Rejected(async () => { await client.ExecuteApprovedAsync(profile,ApprovedTrinoQuery.Probe(),2,default); },502);
factory.Packet = "{\"error\":{\"message\":\"secret-upstream-payload\"}}";
await Rejected(async () => { await client.ExecuteApprovedAsync(profile,ApprovedTrinoQuery.Probe(),2,default); },502);
factory.Packet = new string('x',2_097_153);
await Rejected(async () => { await client.ExecuteApprovedAsync(profile,ApprovedTrinoQuery.Probe(),2,default); },502);
factory.Packet = "{}";
using (var cancelled = new CancellationTokenSource()) {
    cancelled.Cancel();
    try { await client.ExecuteApprovedAsync(profile,ApprovedTrinoQuery.Probe(),2,cancelled.Token); throw new Exception("Cancellation ignored"); } catch (OperationCanceledException) { }
}
foreach (var destination in new[] { "http://user:password@127.0.0.1:18081/", "http://127.0.0.1:18081/?redirect=1" }) {
    config["DataConnectors:Sources:0:Endpoint"] = destination;
    try { registry.All(); throw new Exception("Unsafe endpoint accepted"); } catch (ChatException) { }
}
config["DataConnectors:Sources:0:Endpoint"]="http://127.0.0.1:18081/";
config["DataConnectors:Sources:0:AllowedAddresses:0"]="169.254.169.254";
try { registry.All(); throw new Exception("Metadata IP accepted"); } catch (ChatException) { }
Console.WriteLine("PASS: server-owned source/organization policy, closed SQL plan compiler, identifier attacks, legacy free SQL blocked, nextUri SSRF blocked, row/byte limits, sanitized upstream errors, cancellation and endpoint/metadata-IP rejection. HTTP doubles only.");

sealed class FakeFactory : IHttpClientFactory
{
    public string Packet = "{}";
    public List<string> Requests { get; } = [];
    public HttpClient CreateClient(string name) => new(new FakeHandler(this));
}
sealed class FakeHandler(FakeFactory source) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        source.Requests.Add(request.RequestUri!.AbsoluteUri);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(source.Packet,Encoding.UTF8,"application/json") });
    }
}
