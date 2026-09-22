using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Tokenizers.HuggingFace.Tokenizer;
using AiGateway.Application.Interfaces;

namespace AiGateway.Infrastructure.Qdrant.Services.Reranking
{
    /// <summary>
    /// Reranker ONNX usando o modelo BAAI/bge-reranker-v2-m3 exportado para ONNX.
    /// Entrada: query + header do chunk.
    /// Saída: score único (quanto maior, mais relevante).
    /// </summary>
    public class OnnxRerankerService : IRerankerService
    {
        private readonly InferenceSession _session;
        private readonly Tokenizer _tokenizer;

        public OnnxRerankerService(IConfiguration cfg)
        {
            var modelPath = cfg["Reranker:ModelPath"];
            var tokenizerPath = cfg["Reranker:TokenizerPath"];

            if (string.IsNullOrWhiteSpace(modelPath))
                throw new InvalidOperationException("Reranker:ModelPath não configurado.");
            if (string.IsNullOrWhiteSpace(tokenizerPath))
                throw new InvalidOperationException("Reranker:TokenizerPath não configurado.");

            _session = new InferenceSession(modelPath);
            _tokenizer = Tokenizer.FromFile(tokenizerPath);
        }

        public Task<float> ComputeScoreAsync(string query, string passage)
        {
            // BGE recomenda algo assim:
            // "query: ... document: ..."
            var text = $"query: {query}\ndocument: {passage}";

            // Encode → já com CLS/SEP internos
            var encoding = _tokenizer
                .Encode(
                    text,
                    addSpecialTokens: true,
                    includeTypeIds: false,         // modelo NÃO usa token_type_ids
                    includeAttentionMask: true
                )
                .First();

            var seqLen = encoding.Ids.Count;

            var inputIds = new DenseTensor<long>(new[] { 1, seqLen });
            var attMask = new DenseTensor<long>(new[] { 1, seqLen });

            for (int i = 0; i < seqLen; i++)
            {
                inputIds[0, i] = encoding.Ids[i];
                attMask[0, i] = encoding.AttentionMask[i];
            }

            var inputs = new[]
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
                NamedOnnxValue.CreateFromTensor("attention_mask", attMask)
            };

            using var outputs = _session.Run(inputs);

            // bge-reranker-v2-m3 → logits [batch, 1]
            var logits = outputs.First().AsEnumerable<float>().ToArray();
            var score = logits[0];

            return Task.FromResult(score);
        }
    }
}
