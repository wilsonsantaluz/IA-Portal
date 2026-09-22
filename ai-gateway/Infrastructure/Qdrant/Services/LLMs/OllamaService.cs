using Portal.AI.Gateway.Application.Interfaces;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace AiGateway.Infrastructure.Qdrant.Services.LLMs;
public class OllamaService : IOllamaService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;

    public OllamaService(IHttpClientFactory f, IConfiguration c)
    {
        _http = f.CreateClient();
        _http.Timeout = TimeSpan.FromMinutes(50);
        _http.BaseAddress = new Uri("https://ollama.com/api");
        _http.DefaultRequestHeaders.Authorization =
             new AuthenticationHeaderValue("Bearer", "bd0c89fb8a25400499ec8069a72bdb3c.5ih6t7-DgvLsFC1w_Pk69cZZ");
        _cfg = c;
    }

    public async Task<string> GenerateAsync(string model, string prompt, CancellationToken ct = default)
    {

        model = "deepseek-v3.1:671b-cloud";
            ;
        Console.WriteLine($"[{DateTime.Now}] Modelo {model} - Consultando LLM...");

        var payload = new { model, prompt, stream = false };

        HttpResponseMessage response;

        try
        {
            response = await _http.PostAsJsonAsync("/api/generate", payload, ct);
        }
        catch (Exception ex)
        {
            throw new Exception($"❌ Falha ao conectar no Ollama: {ex.Message}", ex);
        }

        string raw = await response.Content.ReadAsStringAsync(ct);

        Console.WriteLine($"[{DateTime.Now}] Resposta RAW do Ollama: {raw}");

        // Se status code for erro, tenta extrair mensagem do JSON
        if (!response.IsSuccessStatusCode)
        {
            try
            {
                var errJson = JsonSerializer.Deserialize<Dictionary<string, object>>(raw);
                if (errJson != null && errJson.TryGetValue("error", out var msg))
                {
                    throw new Exception($"❌ Erro do Ollama: {msg}");
                }
            }
            catch
            {
                // Se o parse falhar, joga o corpo bruto mesmo
                throw new Exception($"❌ Erro do Ollama (raw): {raw}");
            }
        }

        // Se sucesso → tenta parsear JSON normal
        try
        {
            var json = JsonSerializer.Deserialize<Dictionary<string, object>>(raw);

            if (json != null && json.TryGetValue("response", out var result))
                return result.ToString() ?? "";

            throw new Exception("❌ Erro: a resposta não contém o campo 'response'. Resposta completa: " + raw);
        }
        catch (Exception ex)
        {
            throw new Exception($"❌ Falha ao interpretar JSON do Ollama: {ex.Message}\nRAW: {raw}", ex);
        }
    }

    public  async IAsyncEnumerable<string> GenerateStreamAsync(string model, string prompt, [EnumeratorCancellation] CancellationToken ct = default)
    {
        model = "deepseek-v3.1:671b-cloud";

        var payload = new
        {
            model,
            think = true,
            prompt,
            stream = true
        };

        var req = new HttpRequestMessage(HttpMethod.Post, "/api/generate")
        {
            Content = JsonContent.Create(payload)
        };

        var resp = await _http.SendAsync(
            req,
            HttpCompletionOption.ResponseHeadersRead,
            ct
        );

        resp.EnsureSuccessStatusCode();

        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line)) continue;

            using var doc = JsonDocument.Parse(line);

            // Ollama sinaliza fim assim
            if (doc.RootElement.TryGetProperty("done", out var done) && done.GetBoolean())
                yield break;

            if (doc.RootElement.TryGetProperty("response", out var response))
                yield return response.GetString()!;

            if (doc.RootElement.TryGetProperty("thinking", out var thinking))
                yield return thinking.GetString()!;
        }
    }
}