using System.Net.Http.Headers;
using System.Net.Http.Json;
using Portal.AI.Gateway.Application.DTOs;
using Portal.AI.Gateway.Application.Interfaces;

namespace AiGateway.Infrastructure.Qdrant.Services;

public class OcrService:IOcrService{
 private readonly HttpClient _http;
 public OcrService(IHttpClientFactory f,IConfiguration c){
  _http=f.CreateClient();
  _http.BaseAddress=new Uri(c["OCR_SERVICE_URL"]!);
 }
 public async Task<string> ExtractTextAsync(Stream file,string name,string type,CancellationToken ct=default){
  var form=new MultipartFormDataContent();
  var fc=new StreamContent(file);
  fc.Headers.ContentType=MediaTypeHeaderValue.Parse(type);
  form.Add(fc,"file",name);
  var r=await _http.PostAsync("/ocr",form,ct);
  r.EnsureSuccessStatusCode();
  var o=await r.Content.ReadFromJsonAsync<OcrAnalyzeResponse>(cancellationToken:ct);
  return o?.Texto ??"";
 }
}
