using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Portal.AI.Gateway.Application.Interfaces;

namespace AiGateway.Infrastructure.Qdrant.Services.LLMs
{
    public class OpenAiService : IOllamaService
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _cfg;

        public OpenAiService(IHttpClientFactory f, IConfiguration cfg)
        {
            _cfg = cfg;
            _http = f.CreateClient();
            _http.Timeout = TimeSpan.FromMinutes(10);

            // endpoint OpenAI
            _http.BaseAddress = new Uri("https://api.openai.com/v1/");

            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", cfg["OPENAI_API_KEY"]);
        }

        public async Task<string> GenerateAsync(string model, string prompt, CancellationToken ct = default)
        {

            model = "gpt-4.1";
            var systemPath = "./ragbase/ragbaseinput.txt";
            var systemMessage = File.ReadAllText(systemPath);        

            object payload;

            // Tenta ler como JSON
            JsonDocument? jsonDoc = null;
            bool isJson = false;

            try
            {
                jsonDoc = JsonDocument.Parse(prompt);
                isJson = true;
            }
            catch
            {
                isJson = false;
            }

            // =============================
            // CASO 1 — NÃO É JSON
            // =============================
            if (!isJson)
            {
                payload = new
                {
                    model,
                    messages = new[]
                    {
                new { role = "system", content = systemMessage },
                new { role = "user", content = prompt }
            }
                };
            }
            else
            {
                // É JSON válido
                var root = jsonDoc!.RootElement;

                // Se não tiver "messages", tratar como texto normal
                if (!root.TryGetProperty("messages", out var messagesElement))
                {
                    payload = new
                    {
                        model,
                        messages = new[]
                        {
                    new { role = "system", content = systemMessage },
                    new { role = "user", content = prompt }
                }
                    };
                }
                else
                {
                    // Converter array messages para lista dinâmica
                    var finalMessages = new List<object>();

                    bool hasSystemInsidePrompt = false;

                    foreach (var msg in messagesElement.EnumerateArray())
                    {
                        var role = msg.GetProperty("role").GetString() ?? "";
                        if (role == "system")
                            hasSystemInsidePrompt = true;
                    }

                    // =============================
                    // CASO 2 — JSON com system → adicionar o system do arquivo ANTES
                    // CASO 3 — JSON sem system → adicionar o system do arquivo ANTES
                    // =============================

                    finalMessages.Add(new { role = "system", content = systemMessage });

                    // Agora copia todas as mensagens originais
                    foreach (var msg in messagesElement.EnumerateArray())
                    {
                        finalMessages.Add(new
                        {
                            role = msg.GetProperty("role").GetString(),
                            content = msg.GetProperty("content").GetString()
                        });
                    }

                    payload = new
                    {
                        model,
                        messages = finalMessages
                    };
                }
            }

            // Enviar p/ OpenAI
            var resp = await _http.PostAsJsonAsync("chat/completions", payload, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Erro OpenAI: {raw}");

            using var parsed = JsonDocument.Parse(raw);
            return parsed.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()!;
        }

        public async IAsyncEnumerable<string> GenerateStreamAsync(string model, string prompt, [EnumeratorCancellation] CancellationToken ct = default)
        {
            model = "gpt-4.1";
            var payload = new
            {
                model,
                stream = true,
                messages = new[]
          {
                new { role = "user", content = prompt }
            }
            };
           
            var req = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.openai.com/v1/chat/completions"
            );

            req.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _cfg["OPENAI_API_KEY"]);

            req.Headers.Accept.Add(
                new MediaTypeWithQualityHeaderValue("text/event-stream")
            );       
            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true 
            });        
            var folder = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(folder);          
            var filePath = Path.Combine(
                folder,
                $"openai-request-{DateTime.Now:yyyyMMdd-HHmmss}.json"
            );         
            File.WriteAllText(filePath, json, Encoding.UTF8);        
            req.Content = new StringContent(
                json,
                Encoding.UTF8,
                "application/json"
            );
            using var resp = await _http.SendAsync(
                req,
                HttpCompletionOption.ResponseHeadersRead,
                ct
            );
            resp.EnsureSuccessStatusCode();
            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (!line.StartsWith("data:"))
                    continue;

                var data = line["data:".Length..].Trim();

                if (data == "[DONE]")
                {
                   
                    yield break;
                }
                using var doc = JsonDocument.Parse(data);

                var root = doc.RootElement;

                if (!root.TryGetProperty("choices", out var choices))
                    continue;

                var delta = choices[0].GetProperty("delta");

                if (delta.TryGetProperty("content", out var content))
                {
                    var token = content.GetString();
                    if (!string.IsNullOrEmpty(token))
                        yield return token;
                }
            }
        }
    }
   
}
