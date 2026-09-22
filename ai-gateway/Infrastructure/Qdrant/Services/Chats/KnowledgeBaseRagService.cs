using AiGateway.Application.Interfaces;
using AiGateway.Dtos;
using AiGateway.Infrastructure.Qdrant;
using Azure;
using Azure.Core;
using Portal.AI.Gateway.Application.Interfaces;
using Portal.AI.Gateway.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
// ==== KnowledgeBaseRagService.cs AJUSTADO ====
namespace AiGateway.Infrastructure.Qdrant.Services.Chats {}
public class KnowledgeBaseRagService : IKnowledgeBaseRagService
{
    private readonly PortalDbContext _db;
    private readonly IOnnxEmbeddingService _embed;
    private readonly IQdrantClient _qdrant;
    private readonly IConfiguration _config;
    private readonly IOllamaService _llm;
    private readonly IRerankerService _reranker;
    private AskKbRagRequest _request;

    private const string CollectionName = "kb_chunks";

    public KnowledgeBaseRagService(
        PortalDbContext db,
        IOnnxEmbeddingService embed,
        IQdrantClient qdrant,
        IConfiguration config,
        IOllamaService llm,
        IRerankerService reranker
        )
      
    {
        _db = db;
        _embed = embed;
        _qdrant = qdrant;
        _config = config;
        _llm = llm;
        _reranker = reranker;
      
    }

    public async Task<AskKbRagResponse> AskAsync(AskKbRagRequest req, bool fullResponse, CancellationToken ct = default)
    {

        _request = req;
        if (string.IsNullOrWhiteSpace(req.Prompt))
            throw new ArgumentException("Prompt vazio.");

        string pergunta = req.Prompt.Trim();        
        Console.WriteLine($"[{DateTime.Now}] Gerando embeding {pergunta}");
        var queryEmbedding = _embed.Generate(pergunta);
        Console.WriteLine($"[{DateTime.Now}] Emdeding gerado {queryEmbedding.Length}");
    
        var collection = _config["Qdrant:Collection"] ?? CollectionName;
        int limit = int.Parse(_config["RAG:CandidateLimit"] ?? "100");
        float minScore = float.Parse(_config["RAG:MinScore"] ?? "0.25",
                                     CultureInfo.InvariantCulture);

        
        Console.WriteLine($"[{DateTime.Now}] Consutando banco vetorial");
        var raw = await _qdrant.SearchAsync(collection, queryEmbedding, limit, minScore);

        var candidates = raw.Result;       

        foreach (var (c, i) in candidates.Select((c, i) => (c, i)).Take(10))
            Console.WriteLine($"[PRE] #{i + 1} embScore={c.score:F4} itemId={Payload(c, "itemId")} title={Payload(c, "title")}");
 
        var reranked = new List<(float Score, QdrantPointResult Point)>();

        foreach (var c in candidates)
        {
            string header = ExtractHeader(c.Payload);
            float score = await _reranker.ComputeScoreAsync(pergunta, header);
            reranked.Add((score, c));
        }
        reranked = reranked.OrderByDescending(x => x.Score).ToList();
        Console.WriteLine($"[{DateTime.Now}][RAG] Pós-reranking:");
        foreach (var (r, idx) in reranked.Select((x, i) => (x, i)).Take(10))
            Console.WriteLine($"[POST] #{idx + 1} rerankScore={r.Score:F4} itemId={Payload(r.Point, "itemId")} title={Payload(r.Point, "title")}");

  
        int maxSections = int.Parse(_config["RAG:MaxSections"] ?? "8");

        var topItemIds = reranked
            .Where(x => x.Point.Payload != null && x.Point.Payload.ContainsKey("itemId"))
            .Select(x => TryGetInt(x.Point.Payload["itemId"]) ?? -1)
            .Where(id => id > 0)
            .Distinct()
            .Take(maxSections)
            .ToList();


        Console.WriteLine($"[{DateTime.Now}][RAG] consultando itens ranqueados diretamente no banco de dados");
        var itens = _db.KnowledgeBaseItems
            .Where(x => topItemIds.Contains(x.Id) && x.IsActive)
            .AsEnumerable()                      
            .OrderBy(x => topItemIds.IndexOf(x.Id))
            .ToList();

        
        var contexto = BuildContext(itens);
        var sourcesInfo = BuildInfo(itens);
        var prompt = await BuildPrompt(contexto, pergunta);

      
        return new AskKbRagResponse
        {
            texto = prompt,
            Sources = sourcesInfo
        };
    }


    private string ExtractHeader(Dictionary<string, object>? payload)
    {
        if (payload == null) return "";

        if (payload.TryGetValue("header", out var h) && h is string hs)
            return hs;

        string section = PayloadStr(payload, "section");
        string subsection = PayloadStr(payload, "subsection");
        string title = PayloadStr(payload, "title");

        return $"SEÇÃO: {section}\nSUBSEÇÃO: {subsection}\nTÍTULO: {title}";
    }

    private static int? TryGetInt(object? obj)
    {
        try
        {
            if (obj == null) return null;

            if (obj is int i) return i;
            if (obj is long l) return (int)l;

            if (obj is JsonElement je)
            {
                if (je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out var n))
                    return n;
                if (je.ValueKind == JsonValueKind.String && int.TryParse(je.GetString(), out n))
                    return n;
            }

            if (int.TryParse(obj.ToString(), out var parsed))
                return parsed;

            return null;
        }
        catch { return null; }
    }

    private static string PayloadStr(Dictionary<string, object> p, string key)
        => p.TryGetValue(key, out var v) ? v?.ToString() ?? "" : "";

    private static string Payload(QdrantPointResult r, string key)
        => r.Payload != null && r.Payload.ContainsKey(key) ? r.Payload[key]?.ToString() ?? "" : "";

    private string BuildContext(List<KnowledgeBaseItem> itens)
    {
        var sb = new StringBuilder();

        foreach (var item in itens)
        {
            sb.AppendLine($"[Seção: {item.Section} | Subsec: {item.Subsection} | Título: {item.Title}]");
            sb.AppendLine(item.Content);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private string BuildInfo(List<KnowledgeBaseItem> itens)
    {
        var sb = new StringBuilder();

        foreach (var item in itens)
        {
            sb.AppendLine($"[Seção: {item.Section} | Subsec: {item.Subsection} | Título: {item.Title}]");
          
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private async Task<string> BuildPrompt(string contexto, string pergunta)
    {
        var sb = new StringBuilder();     
        sb.AppendLine(contexto);       
        return sb.ToString();
    }
}
