namespace AiGateway.Dtos
{
    /// <summary>
    /// Requisição para o endpoint de RAG da base de conhecimento.
    /// </summary>
    public class AskKbRagRequest
    {
        /// <summary>
        /// Pergunta do usuário (texto livre).
        /// </summary>
        public string Prompt { get; set; } = string.Empty;

        public string SessionId { get; set; } = string.Empty;

        public string? DocumentContext { get; set; } = string.Empty;

        public string? UserId { get; set; } = string.Empty;

        /// <summary>
        /// (Opcional) Categoria da base de conhecimento a ser usada como filtro.
        /// </summary>
        public int? CategoryId { get; set; }

        /// <summary>
        /// (Opcional) Nome do modelo a ser utilizado no Ollama remoto.
        /// Se nulo, será utilizado o modelo padrão configurado em OLLAMA_DEFAULT_MODEL.
        /// </summary>
        public string? Model { get; set; }
    }
}
