using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace AiGateway.Infrastructure.Qdrant
{
    public class IQdrantClient
    {
        private readonly HttpClient _http;

        public IQdrantClient(HttpClient http, IConfiguration cfg)
        {
            _http = http;

            var baseUrl = cfg["Qdrant:Endpoint"] ?? "http://qdrant:6333";
            if (!baseUrl.EndsWith("/"))
                baseUrl += "/";
            _http.BaseAddress = new Uri(baseUrl);
            _http.Timeout = TimeSpan.FromMinutes(2);
        }

        public async Task EnsureCollectionAsync(string collection, int vectorSize)
        {
            // 1 — Se já existe, retorna
            var exists = await _http.GetAsync($"collections/{collection}");
            if (exists.IsSuccessStatusCode)
                return;

            // 2 — Cria collection com parâmetros IDEAL para RAG
            var body = new
            {
                vectors = new
                {
                    size = vectorSize,
                    distance = "Cosine"
                },

                // HNSW ajustado para indexação imediata
                hnsw_config = new
                {
                    m = 16,
                    ef_construct = 100,
                    full_scan_threshold = 1000
                },

                // Otimizador configurado para indexar IMEDIATAMENTE
                optimizer_config = new
                {
                    indexing_threshold = 0,       // << ESSENCIAL para não ficar com indexed_vectors_count = 0
                    flush_interval_sec = 1
                },

                // Opcional: payload salvo em disco (ótimo para sua KB grande)
                on_disk_payload = true
            };

            var resp = await _http.PutAsJsonAsync($"collections/{collection}", body);
            var content = await resp.Content.ReadAsStringAsync();
            Console.WriteLine("QDRANT CREATE COLLECTION RESPONSE: " + content);

            resp.EnsureSuccessStatusCode();
        }

        public record QdrantPoint(string id, float[] vector, object payload);

       
        public async Task UpsertPointsAsync(string collection, IEnumerable<QdrantPoint> points, CancellationToken ct = default)
        {
            var body = new
            {
                points = points.Select(p => new
                {
                    id = p.id,
                    vector = p.vector.Select(v => (float)v).ToArray(), // garante float32
                    payload = RemoveNulls(p.payload)
                })
            };

            var resp = await _http.PutAsJsonAsync($"collections/{collection}/points", body, ct);
            var content = await resp.Content.ReadAsStringAsync(ct);
            Console.WriteLine("QDRANT UPSERT RESPONSE: " + content);
            resp.EnsureSuccessStatusCode();
        }

        private object RemoveNulls(object payload)
        {
            var dict = new Dictionary<string, object>();

            foreach (var prop in payload.GetType().GetProperties())
            {
                var value = prop.GetValue(payload);
                if (value != null)
                    dict[prop.Name] = value;
            }

            return dict;
        }
        public async Task DeleteByItemIdAsync(string collection, int itemId, CancellationToken ct = default)
        {
            var body = new
            {
                filter = new
                {
                    must = new[]
                    {
                    new
                    {
                        key = "itemId",
                        match = new { value = itemId }
                    }
                }
                }
            };

            var resp = await _http.PostAsJsonAsync($"collections/{collection}/points/delete", body, ct);
            var content = await resp.Content.ReadAsStringAsync(ct);
            //Console.WriteLine("QDRANT DELETE RESPONSE: " + content);
            resp.EnsureSuccessStatusCode();
        }


     public async Task<QdrantSearchResponse> SearchAsync(
      string collection,
      float[] vector,
      int limit = 8,
      float? scoreThreshold = null)
        {
            var body = new Dictionary<string, object>
            {
                ["vector"] = vector,
                ["limit"] = limit,

                ["with_payload"] = true
           
            };

            if (scoreThreshold.HasValue)
            {
                body["score_threshold"] = scoreThreshold.Value;
            }

            var resp = await _http.PostAsJsonAsync($"collections/{collection}/points/search", body);

            var json = await resp.Content.ReadAsStringAsync();
           // Console.WriteLine("QDRANT SEARCH RESPONSE: " + json);

            resp.EnsureSuccessStatusCode();

            var opts = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var result = System.Text.Json.JsonSerializer.Deserialize<QdrantSearchResponse>(json, opts);
            return result ?? new QdrantSearchResponse();
        }
    }

    public class QdrantSearchResponse
    {
        public List<QdrantPointResult> Result { get; set; } = new();
    }

    public class QdrantPointResult
    {
        public float score { get; set; }
        public Dictionary<string, object>? Payload { get; set; }
    }
}
