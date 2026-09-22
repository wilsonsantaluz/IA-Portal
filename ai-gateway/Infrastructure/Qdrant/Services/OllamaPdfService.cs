using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
namespace AiGateway.Infrastructure.Qdrant.Services
{
    public class OllamaPdfService
    {
        private readonly HttpClient _http;

        public OllamaPdfService(HttpClient http)
        {
            _http = http;
            _http.Timeout = TimeSpan.FromMinutes(15);
        }


        public async Task<string> ProcessarPdfAsync(IFormFile file, string modelo = "gemma3:4b")
        {
            var http = new HttpClient();
            var url = "http://localhost:11434/v1/chat/completions";

            using var form = new MultipartFormDataContent();

            // prompt normal
            form.Add(new StringContent(modelo), "model");

            form.Add(new StringContent(
                @"[
            { ""role"": ""user"", ""content"": ""Extraia todas as tabelas deste PDF e retorne JSON estruturado."" }
        ]"
            ), "messages");

            // PDF físico
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            var pdfBytes = new ByteArrayContent(ms.ToArray());
            pdfBytes.Headers.ContentType = MediaTypeHeaderValue.Parse("application/pdf");
            form.Add(pdfBytes, "input_files", file.FileName);

            var response = await http.PostAsync(url, form);
            return await response.Content.ReadAsStringAsync();
        }
    }
}
