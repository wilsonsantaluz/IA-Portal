using System;
using System.Text.Json;

public class SqlPlanResult
{
    /// <summary>Tipo de plano: "kb", "sql", "hybrid", "unknown", "error"</summary>
    public string Type { get; set; } = "unknown";

    /// <summary>SQL gerada pelo planner (se Type = sql ou hybrid)</summary>
    public string? Sql { get; set; }

    /// <summary>Tópico conceitual, ex: "vp", "inadimplencia" (para KB)</summary>
    public string? Topic { get; set; }

    /// <summary>Descrição humana da consulta (opcional)</summary>
    public string? Description { get; set; }

    /// <summary>Conteúdo bruto de erro/resposta não parseada</summary>
    public string? Error { get; set; }

    /// <summary>
    /// Faz o parse do JSON devolvido pelo LLM no formato:
    /// {
    ///   "type": "...",
    ///   "topic": "...",
    ///   "query": "SELECT ...",
    ///   "description": "..."
    /// }
    /// </summary>
    public static SqlPlanResult Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new SqlPlanResult
            {
                Type = "error",
                Error = "JSON vazio retornado pelo planner."
            };
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string type = "unknown";
            if (root.TryGetProperty("type", out var typeEl) &&
                typeEl.ValueKind == JsonValueKind.String)
            {
                type = typeEl.GetString() ?? "unknown";
            }

            string? topic = null;
            if (root.TryGetProperty("topic", out var topicEl) &&
                topicEl.ValueKind != JsonValueKind.Undefined &&
                topicEl.ValueKind != JsonValueKind.Null)
            {
                topic = topicEl.ToString();
            }

            string? sql = null;
            if (root.TryGetProperty("query", out var queryEl) &&
                queryEl.ValueKind != JsonValueKind.Undefined &&
                queryEl.ValueKind != JsonValueKind.Null)
            {
                sql = queryEl.ToString();
            }

            string? description = null;
            if (root.TryGetProperty("description", out var descEl) &&
                descEl.ValueKind != JsonValueKind.Undefined &&
                descEl.ValueKind != JsonValueKind.Null)
            {
                description = descEl.ToString();
            }

            return new SqlPlanResult
            {
                Type = type,
                Topic = topic,
                Sql = sql,
                Description = description,
                Error = null
            };
        }
        catch (Exception ex)
        {
            return new SqlPlanResult
            {
                Type = "error",
                Error = $"Erro ao fazer parse do JSON do planner: {ex.Message}\nConteúdo bruto:\n{json}"
            };
        }
    }
}
