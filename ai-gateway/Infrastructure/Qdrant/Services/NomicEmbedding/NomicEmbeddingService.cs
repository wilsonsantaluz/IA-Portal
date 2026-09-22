namespace AiGateway.Infrastructure.Qdrant.Services.NomicEmbedding
{
    using AiGateway.Application.Interfaces;
    using Microsoft.ML.OnnxRuntime;
    using Microsoft.ML.OnnxRuntime.Tensors;
    using Microsoft.Extensions.Configuration;
    using System;
    using System.Linq;
    using Tokenizers.HuggingFace.Tokenizer;

    public class NomicEmbeddingService : IOnnxEmbeddingService
    {
        private readonly InferenceSession _session;
        private readonly Tokenizer _tokenizer;
        private readonly int _dimension;

        public NomicEmbeddingService(IConfiguration cfg)
        {
            var modelPath = cfg["Embedding:ModelPath"];
            var tokenizerPath = cfg["Embedding:TokenizerPath"];

            if (string.IsNullOrWhiteSpace(modelPath))
                throw new InvalidOperationException("Embedding:ModelPath não configurado.");

            if (string.IsNullOrWhiteSpace(tokenizerPath))
                throw new InvalidOperationException("Embedding:TokenizerPath não configurado.");

            _session = new InferenceSession(modelPath);

            // Carrega o tokenizer.json usando o Tokenizers.HuggingFace
            _tokenizer = Tokenizer.FromFile(tokenizerPath);

            // Dimensão do embedding (384 se for MiniLM, 768 se for Nomic v1.5)
            _dimension = int.Parse(cfg["Embedding:Dimension"] ?? "384");
        }

        public float[] Generate(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new float[_dimension];

            // IMPORTANTE:
            // Encode(string text, bool addSpecialTokens, ...)
            // Retorna IEnumerable<Encoding> -> usamos First()
            var encoding = _tokenizer
                .Encode(
                    text,
                    addSpecialTokens: true,
                    includeTypeIds: true,
                    includeAttentionMask: true
                )
                .First();

            var sequenceLength = encoding.Ids.Count;

            // Converte ids, type_ids e attention_mask para tensores long[1, seqLen]
            var inputIds = new DenseTensor<long>(
                encoding.Ids.Select(id => (long)id).ToArray(),
                new[] { 1, sequenceLength }
            );

            var tokenTypeIds = new DenseTensor<long>(
                encoding.TypeIds.Select(id => (long)id).ToArray(),
                new[] { 1, sequenceLength }
            );

            var attentionMask = new DenseTensor<long>(
                encoding.AttentionMask.Select(id => (long)id).ToArray(),
                new[] { 1, sequenceLength }
            );

            var inputs = new[]
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
                NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIds),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask)
            };

            using var results = _session.Run(inputs);

            // Saída típica do modelo estilo MiniLM/Nomic exportado:
            // [batch=1, seq_len, hidden_size(_dimension)]
            var outputFloats = results.First().AsEnumerable<float>().ToArray();

            // Faz average pooling sobre a dimensão de sequência
            var embedding = new float[_dimension];

            for (int i = 0; i < sequenceLength; i++)
            {
                int offset = i * _dimension;
                for (int j = 0; j < _dimension; j++)
                {
                    embedding[j] += outputFloats[offset + j];
                }
            }

            // Média
            var inv = 1f / sequenceLength;
            for (int j = 0; j < _dimension; j++)
                embedding[j] *= inv;

            // Normalizar (norma L2)
            Normalize(embedding);

            return embedding;
        }

        private void Normalize(float[] v)
        {
            float sum = 0f;
            for (int i = 0; i < v.Length; i++)
                sum += v[i] * v[i];

            var norm = MathF.Sqrt(sum);
            if (norm > 0f)
            {
                for (int i = 0; i < v.Length; i++)
                    v[i] /= norm;
            }
        }
    }
}
