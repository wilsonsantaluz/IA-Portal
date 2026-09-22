using System.Collections.Generic;

namespace AiGateway.Dtos
{
    /// <summary>
    /// Resposta do endpoint de RAG da base de conhecimento.
    /// </summary>
    public class AskKbRagResponse
    {
        /// <summary>
        /// Texto final respondido pelo modelo LLM.
        /// </summary>
      
        public string texto { get; set; } = string.Empty;

        /// <summary>
        /// Lista de chaves (KeyName) dos itens da base usados como contexto.
        /// </summary>
        public string Sources { get; set; }

        public List<string> DebugInfo { get; set; } = new();


        
    }
}
