namespace Portal.AI.Gateway.Application.DTOs;

public class AskSqlRagResponse
{
    /// <summary>
    /// SQL gerada pelo LLM.
    /// </summary>
    public string Sql { get; set; } = "";

    /// <summary>
    /// Linhas retornadas do Trino (já em formato dicionário).
    /// </summary>
    public object? Rows { get; set; }

    /// <summary>
    /// Resposta em linguagem natural usando os dados como contexto.
    /// </summary>
    public string Answer { get; set; } = "";
}
