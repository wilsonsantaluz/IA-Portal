using AiGateway.Application.Interfaces;
using System;

namespace AiGateway.Application.Interfaces
{
    /// <summary>
    /// Serviço responsável por gerar embeddings (vetores numéricos) a partir de um texto.
    /// </summary>
    public interface IOnnxEmbeddingService
    {
        /// <summary>
        /// Gera o embedding (vetor de floats) para o texto informado.
        /// O vetor retornado deve, preferencialmente, estar normalizado (norma 1).
        /// </summary>
        float[] Generate(string text);
    }
}
