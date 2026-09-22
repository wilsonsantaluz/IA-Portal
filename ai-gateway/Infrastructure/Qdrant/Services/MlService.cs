using System.Text.Json;
using Portal.AI.Gateway.Application.Interfaces;

namespace AiGateway.Infrastructure.Qdrant.Services;

public class MlService:IMlService{
 private readonly HttpClient _http;
 public MlService(IHttpClientFactory f,IConfiguration c){
  _http=f.CreateClient();
  _http.BaseAddress=new Uri(c["ML_SERVICE_URL"]!);
 }
 public async Task<List<Dictionary<string,object?>>> PredictCarteiraAsync(string carteira,int meses,CancellationToken ct=default){
  var payload=new{carteiraId=carteira, mesesProjecao=meses};
  var resp=await _http.PostAsJsonAsync("/predict",payload,ct);
  resp.EnsureSuccessStatusCode();
  using var st=await resp.Content.ReadAsStreamAsync(ct);
  var doc=await JsonDocument.ParseAsync(st,cancellationToken:ct);
  var list=new List<Dictionary<string,object?>>();
  if(doc.RootElement.TryGetProperty("previsoes",out var arr)){
   foreach(var it in arr.EnumerateArray()){
    var d=new Dictionary<string,object?>();
    foreach(var p in it.EnumerateObject()){
     d[p.Name]=p.Value.ToString();
    }
    list.Add(d);
   }
  }
  return list;
 }
}
