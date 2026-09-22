using AiGateway.Application.Interfaces;
using AiGateway.Dtos;
using AiGateway.Infrastructure.Qdrant;
using Portal.AI.Gateway.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AiGateway.Infrastructure.Qdrant.Services
{
    /// <summary>
    /// Serviço de manutenção da Knowledge Base:
    /// - Persiste o documento original em KnowledgeBaseItem (SQL)
    /// - Gera embedding local via IOnnxEmbeddingService
    /// - Armazena o embedding em JSON (debug) e binário (Embedding)
    /// </summary>
    public static class TextChunker
    {
        // chunkSizeChars ~1200, overlapChars ~200 é um bom ponto de partida
        public static IEnumerable<(int Index, string Text)> ChunkSubsection(
            string section,
            string? subsection,
            string? title,
            string content,
            int chunkSizeChars = 1200,
            int overlapChars = 200)
        {
            // Monta um cabeçalho mais rico semanticamente
            var headerLines = new List<string>();

            if (!string.IsNullOrWhiteSpace(section))
                headerLines.Add($"SEÇÃO: {section}");

            if (!string.IsNullOrWhiteSpace(subsection))
                headerLines.Add($"SUBSEÇÃO: {subsection}");

            if (!string.IsNullOrWhiteSpace(title))
                headerLines.Add($"TÍTULO: {title}");

            var header = string.Join(Environment.NewLine, headerLines);
            if (!string.IsNullOrEmpty(header))
                header += Environment.NewLine + Environment.NewLine;

            var full = header + content;

            if (string.IsNullOrWhiteSpace(full))
                yield break;

            int index = 0;
            int pos = 0;
            while (pos < full.Length)
            {
                var remaining = full.Length - pos;
                var len = Math.Min(chunkSizeChars, remaining);
                var chunkText = full.Substring(pos, len);

                yield return (index, chunkText);

                if (pos + len >= full.Length)
                    break;

                // avança com overlap
                pos += chunkSizeChars - overlapChars;
                index++;
            }
        }
    }

    public class KnowledgeBaseService : IKnowledgeBaseService
    {
        private readonly PortalDbContext _context;
        private readonly IServiceProvider _services;
        private readonly bool _advanced;
        private IOnnxEmbeddingService Embed => _services.GetRequiredService<IOnnxEmbeddingService>();
        private readonly IQdrantClient _qdrant;
        private const string CollectionName = "kb_chunks";
        private const int VectorSize = 768; 

        public KnowledgeBaseService(
            PortalDbContext context,
            IServiceProvider services,
            IConfiguration config,
            IQdrantClient qdrant)
        {
            _context = context;
            _services = services;
            _advanced = config.GetValue<bool>("Chat:UseAdvancedRag");
            _qdrant = qdrant;
        }

        public async Task<int> UpsertAsync(UpsertKbItemRequest req, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(req.KeyName))
                throw new ArgumentException("KeyName é obrigatório.", nameof(req.KeyName));
            if (string.IsNullOrWhiteSpace(req.Section))
                throw new ArgumentException("Section é obrigatória.", nameof(req.Section));
            if (string.IsNullOrWhiteSpace(req.Content))
                throw new ArgumentException("Content é obrigatório.", nameof(req.Content));

            KnowledgeBaseItem entity;

            // UPDATE
            if (req.Id.HasValue && req.Id.Value > 0)
            {
                entity = await _context.KnowledgeBaseItems
                    .FirstOrDefaultAsync(x => x.Id == req.Id.Value, ct)
                    ?? throw new InvalidOperationException($"KnowledgeBaseItem {req.Id} não encontrado.");
            }
            else // INSERT
            {
                entity = new KnowledgeBaseItem
                {
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };
                _context.KnowledgeBaseItems.Add(entity);
            }

            entity.KeyName = req.KeyName;
            entity.Section = req.Section;
            entity.Subsection = req.Subsection;
            entity.Title = req.Title;
            entity.Content = req.Content;
            entity.CategoryId = req.CategoryId;
            entity.IsActive = req.IsActive;

            await _context.SaveChangesAsync(ct);

            // 🔥 Agora (re)indexa esse item no Qdrant
            if (_advanced) await ReindexItemInQdrantAsync(entity, ct);

            return entity.Id;
        }

        public async Task ReindexItemInQdrantAsync(KnowledgeBaseItem item, CancellationToken ct)
        {
            if (!_advanced) throw new InvalidOperationException("Indexação vetorial requer Chat:UseAdvancedRag.");

            await _qdrant.EnsureCollectionAsync(CollectionName, VectorSize);
            // Apaga chunks antigos desse item no Qdrant
            try
            {
                await _qdrant.DeleteByItemIdAsync(CollectionName, item.Id, ct);
            }
            catch (Exception ex) { }

            var chunks = TextChunker.ChunkSubsection(
                    item.Section,
                    item.Subsection,
                    item.Title,
                    item.Content,
                    chunkSizeChars: 1200,
                    overlapChars: 200);
            var points = new List<IQdrantClient.QdrantPoint>();

            foreach (var ch in chunks)
            {
                var vec = Embed.Generate(ch.Text); // float[]
                Console.WriteLine("Size :" + vec.Length);

                var payload = new
                {
                    itemId = item.Id,
                    keyName = item.KeyName,
                    section = item.Section,
                    subsection = item.Subsection,
                    title = item.Title,
                    chunkIndex = ch.Index,
                    text = ch.Text
                };

                points.Add(new IQdrantClient.QdrantPoint(
                    id: Guid.NewGuid().ToString(),
                    vector: vec,
                    payload: payload
                ));
            }

            Console.WriteLine($">>> Enviando {points.Count} chunks para Qdrant (itemId={item.Id})");
            if (points.Count > 0)
                await _qdrant.UpsertPointsAsync(CollectionName, points, ct);
        }
    }

}
