using System.Net.Http.Headers;
using System.Text.Json;
using AiGateway.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Portal.AI.Gateway.Infrastructure.Trino;

public class TrinoRestClient: ITrinoRestClient
{
    private readonly HttpClient _http;
    private readonly ILogger<TrinoRestClient> _logger;
    private readonly string _catalog;
    private readonly string _user;

    public TrinoRestClient(HttpClient http, IConfiguration cfg, ILogger<TrinoRestClient> logger)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromMinutes(5);
        _logger = logger;

        _http.BaseAddress = new Uri(cfg["TRINO_URL"]!);  // ex: http://localhost:8080

        _catalog = cfg["TRINO_CATALOG"] ?? "memory";
        _user = cfg["TRINO_USER"] ?? "portal";
    }

    // -----------------------------------------------------------
    // FUNÇÃO PRINCIPAL: EXECUTA UMA QUERY SQL NO TRINO
    // -----------------------------------------------------------
    public async Task<TrinoResponseData> ExecuteQueryAsync(
        string sql,
        string schema = "financeiro",
        CancellationToken ct = default)
    {
        string cleanSql = SanitizeSql(sql);
        _logger.LogInformation("SQL Sanitizado para execução:\n{Sql}", cleanSql);

        var req = new HttpRequestMessage(HttpMethod.Post, "/v1/statement")
        {
            Content = new StringContent(cleanSql)
        };
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        req.Headers.Add("X-Trino-User", _user);
        req.Headers.Add("X-Trino-Catalog", _catalog);
        req.Headers.Add("X-Trino-Schema", schema);

        _logger.LogInformation("Enviando consulta inicial ao Trino...");

        var resp = await _http.SendAsync(req, ct);
        var raw = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Erro inicial Trino: {raw}");

        var packet = JsonSerializer.Deserialize<TrinoRawResponse>(raw)!;

        var final = new TrinoResponseData();

        if (packet.Data != null)
            final.AddBatch(packet.Columns, packet.Data);

        string? next = packet.NextUri;
        string state = packet.Stats?.State ?? "UNKNOWN";

        // ------------------------------
        // LOOP DE POLLING OFICIAL
        // ------------------------------
        while (true)
        {
            if (ct.IsCancellationRequested)
                throw new TaskCanceledException("Operação Trino cancelada pelo token.");

            // FINALIZOU
            if (next == null && state == "FINISHED")
            {
                _logger.LogInformation("Trino finalizou corretamente.");
                break;
            }

            // FALHOU
            if (state == "FAILED")
            {
                throw FormatTrinoError(raw, cleanSql);
            }

            // Não tem next ainda → aguardar
            if (next == null)
            {
                _logger.LogInformation("Aguardando próximo batch... state={State}", state);
                await Task.Delay(80, ct);
                continue;
            }

            // Chamar nextUri
            _logger.LogInformation("Polling nextUri: {Next}", next);
            var nextResp = await _http.GetAsync(next, ct);
            var nextTxt = await nextResp.Content.ReadAsStringAsync(ct);

            var nextObj = JsonSerializer.Deserialize<TrinoRawResponse>(nextTxt)!;

            if (nextObj.Data != null)
                final.AddBatch(nextObj.Columns, nextObj.Data);

            raw = nextTxt;
            next = nextObj.NextUri;
            state = nextObj.Stats?.State ?? "UNKNOWN";

            if (state == "FAILED")
                throw FormatTrinoError(nextTxt, cleanSql);

            if (state == "FINISHED")
                break;
        }

        _logger.LogInformation("Trino retornou {Count} linhas.", final.Rows.Count);
        return final;
    }

    // -----------------------------------------------------------
    // SANITIZAÇÃO DO SQL — ESSENCIAL
    // -----------------------------------------------------------
    private string SanitizeSql(string sql)
    {
        string s = sql.Trim();

        // Remove ; no final
        if (s.EndsWith(";"))
            s = s[..^1];

        // Remove comentários "-- ..."
        s = RemoveLineComments(s);

        // Remove comentários /* ... */
        s = RemoveBlockComments(s);

        // Remove múltiplos espaços
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ");

        return s;
    }

    private string RemoveLineComments(string sql)
    {
        var lines = sql.Split('\n');
        var clean = new List<string>();

        foreach (var line in lines)
        {
            int i = line.IndexOf("--");
            clean.Add(i >= 0 ? line[..i] : line);
        }
        return string.Join("\n", clean);
    }

    private string RemoveBlockComments(string sql)
    {
        while (true)
        {
            int start = sql.IndexOf("/*");
            if (start < 0) break;

            int end = sql.IndexOf("*/", start + 2);
            if (end < 0) break;

            sql = sql.Remove(start, end - start + 2);
        }
        return sql;
    }

    // -----------------------------------------------------------
    // FORMATA O ERRO DO TRINO EM UMA EXCEÇÃO SUPER ÚTIL
    // -----------------------------------------------------------
    private Exception FormatTrinoError(string json, string sql)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var error = root.GetProperty("error");
            string msg = error.GetProperty("message").GetString() ?? "Erro desconhecido";

            int? line = error.TryGetProperty("errorLocation", out var loc)
                ? loc.GetProperty("lineNumber").GetInt32()
                : null;

            int? col = error.TryGetProperty("errorLocation", out var loc2)
                ? loc2.GetProperty("columnNumber").GetInt32()
                : null;

            string formatted = $"""
                ❌ ERRO NO TRINO
                Mensagem: {msg}
                Linha: {line}
                Coluna: {col}

                SQL ENVIADO:
                {sql}

                JSON ORIGINAL:
                {json}
                """;

            return new Exception(formatted);
        }
        catch
        {
            return new Exception($"Erro Trino bruto: {json}");
        }
    }
}
