using System.Text.Json;
using Portal.AI.Gateway.Application.DTOs;
using Portal.AI.Gateway.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AiGateway.Controllers.Chat;

[ApiController]
[Route("api/sql")]
public class SqlController : ControllerBase
{
    private readonly IOllamaService _ollama;
    private readonly ITrinoQueryService _trino;
    private readonly ITrinoMetadataService _trinoMetadata;

    public SqlController(IOllamaService ollama, ITrinoQueryService trino, ITrinoMetadataService trinoMetadata)
    {
        _ollama = ollama;
        _trino = trino;
        _trinoMetadata = trinoMetadata;
    }

    // -------------------------------------------------------------
    //  ENDPOINT SIMPLES
    // -------------------------------------------------------------
    [HttpPost("ask")]
    public async Task<ActionResult<AskSqlResponse>> AskSql([FromBody] AskSqlRequest req, CancellationToken ct)
    {
        var prompt = $@"
Você é um assistente especialista em SQL para TRINO.
Converta a pergunta abaixo em UMA ÚNICA QUERY SQL válida.

Pergunta: {req.Question}

IMPORTANTE:
- Não explique nada, apenas gere a SQL.
- Use LIMIT {req.Limit} quando fizer sentido.
";

        var sql = (await _ollama.GenerateAsync("llama3.1", prompt, ct))
            .Trim()
            .Trim('`');

        var rows = await _trino.QueryAsync(sql, schema: req.Schema);

        return Ok(new AskSqlResponse
        {
            Sql = sql,
            Rows = rows
        });
    }

    // -------------------------------------------------------------
    //  ENDPOINT RAG
    // -------------------------------------------------------------
    [HttpPost("ask-rag")]
    public async Task<IActionResult> AskRag([FromBody] AskSqlRequest req, CancellationToken ct)
    {
        // 0) Puxar metadata real do Trino
        var metadata = await _trinoMetadata.GetCatalogMetadataAsync(req.Schema, ct);

        // 1) Prompt para gerar SQL com catálogo real
        var promptSql = $"""
Você é um assistente especialista em SQL para TRINO.

USE EXCLUSIVAMENTE AS TABELAS E COLUNAS LISTADAS ABAIXO:

CATÁLOGO DO TRINO:
{metadata}

Converta a pergunta abaixo em UMA ÚNICA QUERY SQL válida.

Pergunta: {req.Question}

IMPORTANTE:
- Não invente tabelas.
- Não invente colunas.
- Use somente o schema "{req.Schema}".
- A resposta DEVE ser SQL pura.
- Não escreva explicações.
- Use LIMIT {req.Limit} se fizer sentido.
""";

        // 2) Gera SQL
        var sql = (await _ollama.GenerateAsync("mistral", promptSql, ct))
            .Trim()
            .Trim('`');

        if (!sql.TrimStart().StartsWith("select", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                error = "A SQL gerada não parece um SELECT.",
                sql
            });
        }

        // 3) Executar SQL no Trino
        var rows = await _trino.QueryAsync(sql, schema: req.Schema);

        // Reduz para contexto (caso venha muito registro)
        var rowsForContext = rows.Take(req.Limit).ToList();

        var jsonDados = JsonSerializer.Serialize(
            rowsForContext,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        );

        // 4) Prompt final RAG para resposta em linguagem natural
        var promptRag = $"""
Você é um assistente de análise financeira para diferentes setores.

Use EXCLUSIVAMENTE os dados abaixo (JSON) para responder:

DADOS:
{jsonDados}

PERGUNTA ORIGINAL:
{req.Question}

INSTRUÇÕES:
- Responda em português.
- Seja técnico e objetivo.
- Não invente valores.
- Só utilize números existentes nos dados acima.
""";

        var answer = await _ollama.GenerateAsync("mistral", promptRag, ct);

        return Ok(new AskSqlRagResponse
        {
            Sql = sql,
            Rows = rows,
            Answer = answer
        });
    }
}
