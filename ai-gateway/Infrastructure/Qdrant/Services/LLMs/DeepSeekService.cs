using Portal.AI.Gateway.Application.Interfaces;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace AiGateway.Infrastructure.Qdrant.Services.LLMs
{


    public class DeepSeekService : IOllamaService   
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _cfg;

        public DeepSeekService(IHttpClientFactory f, IConfiguration cfg)
        {
            _cfg = cfg;
            _http = f.CreateClient();
            _http.Timeout = TimeSpan.FromMinutes(50);

            // Endpoint DeepSeek Cloud
            _http.BaseAddress = new Uri(cfg["DEEPSEEK_BASE_URL"] ?? "https://api.deepseek.com");

            // Autorização igual ao Ollama
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", cfg["DEEPSEEK_API_KEY"]);
        }
        public async IAsyncEnumerable<string> GenerateStreamAsync(
    string model,
    string prompt,
    [EnumeratorCancellation] CancellationToken ct = default)
        {
            model = "deepseek-chat";
            //// Prompt is supplied by the calling service.
           // 
           // prompt = prompt + systemMessage;

            var payload = new
            {
                model,
                stream = true,
                messages = new[]
                {
            new { role = "user", content = prompt }
        }
            };
            _http.DefaultRequestHeaders.Add("Accept", "text/event-stream");
            _http.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            var req = new HttpRequestMessage(HttpMethod.Post, "/chat/completions");
            
            req.Content = JsonContent.Create(payload);

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

                if (line.StartsWith("data: "))
                {
                    var json = line["data: ".Length..];
                    if (json == "[DONE]") yield break;

                    using var doc = JsonDocument.Parse(json);
                    var delta =
                        doc.RootElement
                           .GetProperty("choices")[0]
                           .GetProperty("delta");

                    if (delta.TryGetProperty("content", out var content))
                        yield return content.GetString()!;
                }
            }
        }


        public async Task<string> GenerateAsync(string model, string prompt, CancellationToken ct = default)
        {
        
            model = _cfg["DEEPSEEK_DEFAULT_MODEL"] ?? "deepseek-chat";

            Console.WriteLine($"[{DateTime.Now}] Modelo {model} - Consultando DeepSeek...");
            // Prompt is supplied by the calling service.
            

            var payload = new
            {
                model,
                enable_memory = true,
                think = true,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            HttpResponseMessage response;

            try
            {
                response = await _http.PostAsJsonAsync("/chat/completions", payload, ct);
            }
            catch (Exception ex)
            {
                throw new Exception($"❌ Falha ao conectar no DeepSeek: {ex.Message}", ex);
            }

            var raw = await response.Content.ReadAsStringAsync(ct);


            if (!response.IsSuccessStatusCode)
            {
            
                try
                {
                    using var doc = JsonDocument.Parse(raw);

                    if (doc.RootElement.TryGetProperty("error", out var err))
                    {
                        string msg = err.TryGetProperty("message", out var m) ? m.GetString()! : raw;
                        throw new Exception($"❌ Erro do DeepSeek: {msg}");
                    }
                }
                catch
                {
                    throw new Exception($"❌ Erro do DeepSeek (raw): {raw}");
                }
            }

            try
            {
                using var doc = JsonDocument.Parse(raw);

                var content =
                    doc.RootElement.GetProperty("choices")[0]
                                   .GetProperty("message")
                                   .GetProperty("content")
                                   .GetString();

                if (content == null)
                    throw new Exception("Campo 'choices[0].message.content' ausente.");

                return content;
            }
            catch (Exception ex)
            {
                throw new Exception($"❌ Falha ao interpretar retorno do DeepSeek: {ex.Message}\nRAW: {raw}", ex);
            }
        }
    }
}

