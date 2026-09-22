namespace AiGateway.Dtos
{
    public class UpsertKbItemRequest
    {
        /// <summary>Id do registro; nulo = criar novo.</summary>
        public int? Id { get; set; }

        /// <summary>Identificador curto e único da entrada.</summary>
        public string KeyName { get; set; } = string.Empty;

        /// <summary>Seção principal do conhecimento (ex: "Referência 1 — Valor Presente").</summary>
        public string Section { get; set; } = string.Empty;

        /// <summary>Subseção dentro da seção (ex: "Cálculo do Valor Presente").</summary>
        public string? Subsection { get; set; }

        /// <summary>Título amigável mostrado na tela.</summary>
        public string? Title { get; set; }

        /// <summary>Conteúdo em Markdown referente à subseção.</summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>Categoria opcional (mantido por compatibilidade).</summary>
        public int? CategoryId { get; set; }

        /// <summary>Indica se o item está ativo na base.</summary>
        public bool IsActive { get; set; } = true;
    }
}
