namespace Portal.AI.Gateway.Infrastructure.Trino;

// No API accepts arbitrary SQL. Only these closed, server-compiled plans can execute.
// This is deliberately narrower than general SQL parsing, including SELECT functions.
public sealed class ApprovedTrinoQuery
{
    public string Sql { get; }
    public int RowLimit { get; }
    public string Kind { get; }
    private ApprovedTrinoQuery(string sql, int limit, string kind) { Sql = sql; RowLimit = limit; Kind = kind; }
    public static ApprovedTrinoQuery Probe() => new("SELECT 1 AS connected", 1, "probe");
    public static ApprovedTrinoQuery Metadata(ConnectorProfile p)
    {
        ValidateNames(p, p.Tables);
        var tables = string.Join(",", p.Tables.Select(t => $"'{t}'"));
        return new($"SELECT table_name, column_name, data_type FROM \"{p.Catalog}\".information_schema.columns WHERE table_schema='{p.Schema}' AND table_name IN ({tables}) ORDER BY table_name, ordinal_position LIMIT 1000", 1000, "metadata");
    }
    private static void ValidateNames(ConnectorProfile p, IEnumerable<string> tables)
    {
        if (!ConnectorProfiles.Identifier(p.Catalog) || !ConnectorProfiles.Identifier(p.Schema) || tables.Any(t => !ConnectorProfiles.Identifier(t)))
            throw new ArgumentException("Identificador SQL invalido.");
    }
    public static ApprovedTrinoQuery Analytics(ConnectorProfile p, string kind, DateOnly reference, IReadOnlyCollection<string> selected)
    {
        if (kind is not ("aging" or "aging_trend" or "cashflow" or "top_balances")) throw new ArgumentException("Plano analitico nao suportado.");
        if (reference.Year is < 2000 or > 2100) throw new ArgumentException("Data invalida.");
        ValidateNames(p, selected);
        if (!new[] { "invoices", "payments" }.All(t => selected.Contains(t) && p.Tables.Contains(t)))
            throw new ArgumentException("Selecione invoices e payments autorizadas para este plano.");
        string Table(string name) => $"\"{p.Catalog}\".\"{p.Schema}\".\"{name}\"";
        var date = reference.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var from = new DateOnly(reference.Year, reference.Month, 1).AddMonths(-6).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        if (kind == "aging_trend")
        {
            var first = new DateOnly(reference.Year,reference.Month,1);
            var dates = string.Join(",",Enumerable.Range(1,6).Reverse().Select(i => $"(DATE '{first.AddMonths(1-i).AddDays(-1):yyyy-MM-dd}')"));
            return new($"""
                WITH refs(ref) AS (VALUES {dates}), paid AS (
                  SELECT r.ref,p.invoice_id,sum(CASE WHEN p.status='confirmed' THEN p.amount_cents WHEN p.status='reversed' THEN -p.amount_cents ELSE 0 END) AS cents
                  FROM refs r JOIN {Table("payments")} p ON CAST(p.paid_on AS DATE)<=r.ref GROUP BY r.ref,p.invoice_id
                ), balances AS (
                  SELECT r.ref,greatest(i.net_cents-coalesce(p.cents,0),0) AS balance_cents,CAST(i.due_on AS DATE) AS due
                  FROM refs r CROSS JOIN {Table("invoices")} i LEFT JOIN paid p ON p.ref=r.ref AND p.invoice_id=i.id
                  WHERE i.status<>'cancelled' AND CAST(i.issued_on AS DATE)<=r.ref
                )
                SELECT CAST(ref AS VARCHAR) AS mes,sum(balance_cents) AS saldo_centavos,
                  sum(CASE WHEN due<ref THEN balance_cents ELSE 0 END) AS vencido_centavos,
                  100.0*sum(CASE WHEN due<ref THEN balance_cents ELSE 0 END)/nullif(sum(balance_cents),0) AS taxa_percentual
                FROM balances GROUP BY ref ORDER BY ref LIMIT 6
                """,6,kind);
        }
        var balances = $"""
            WITH paid AS (
              SELECT invoice_id, sum(CASE WHEN status='confirmed' THEN amount_cents WHEN status='reversed' THEN -amount_cents ELSE 0 END) AS cents
              FROM {Table("payments")} WHERE CAST(paid_on AS DATE) <= DATE '{date}' GROUP BY invoice_id
            ), balances AS (
              SELECT i.id, i.customer_id, greatest(i.net_cents-coalesce(p.cents,0),0) AS balance_cents,
                date_diff('day',CAST(i.due_on AS DATE),DATE '{date}') AS days_late
              FROM {Table("invoices")} i LEFT JOIN paid p ON p.invoice_id=i.id
              WHERE i.status<>'cancelled' AND CAST(i.issued_on AS DATE) <= DATE '{date}'
            )
            """;
        if (kind == "aging") return new(balances + """
             SELECT CASE WHEN days_late<=0 THEN 'a vencer' WHEN days_late<=15 THEN '01-15' WHEN days_late<=30 THEN '16-30'
               WHEN days_late<=60 THEN '31-60' WHEN days_late<=90 THEN '61-90' ELSE '>90' END AS faixa,
               count(*) AS faturas, sum(balance_cents) AS saldo_centavos
             FROM balances WHERE balance_cents>0 GROUP BY 1 ORDER BY 1 LIMIT 6
            """, 6, kind);
        if (kind == "top_balances") return new(balances + """
             SELECT customer_id AS cliente_id, sum(balance_cents) AS saldo_centavos,
               100.0*sum(balance_cents)/nullif(sum(sum(balance_cents)) OVER (),0) AS percentual_total
             FROM balances WHERE balance_cents>0 GROUP BY customer_id ORDER BY saldo_centavos DESC, cliente_id LIMIT 10
            """, 10, kind);
        return new($"""
            WITH billed AS (
              SELECT date_trunc('month',CAST(issued_on AS DATE)) AS month, sum(net_cents) AS cents
              FROM {Table("invoices")} WHERE status<>'cancelled' AND CAST(issued_on AS DATE)>=DATE '{from}' AND CAST(issued_on AS DATE)<DATE '{date}' GROUP BY 1
            ), cash AS (
              SELECT date_trunc('month',CAST(paid_on AS DATE)) AS month,
                sum(CASE WHEN status='confirmed' THEN amount_cents WHEN status='reversed' THEN -amount_cents ELSE 0 END) AS cents
              FROM {Table("payments")} WHERE CAST(paid_on AS DATE)>=DATE '{from}' AND CAST(paid_on AS DATE)<DATE '{date}' GROUP BY 1
            )
            SELECT CAST(coalesce(b.month,c.month) AS VARCHAR) AS mes, coalesce(b.cents,0) AS faturado_centavos, coalesce(c.cents,0) AS caixa_centavos
            FROM billed b FULL OUTER JOIN cash c ON b.month=c.month ORDER BY 1 LIMIT 7
            """, 7, kind);
    }
}
