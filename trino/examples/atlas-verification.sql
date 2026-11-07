-- Synthetic Atlas v1 only. Run after importing the CSV into an authorized
-- local catalog/schema. These statements are read-only and not an importer.
-- Expected: customers=500, plans=3, contracts=500, invoices=5896,
-- payments=5561, disputes=587, collection_actions=2071 (seed 42).
SELECT 'customers' AS source, count(*) AS rows FROM customers
UNION ALL SELECT 'plans', count(*) FROM plans
UNION ALL SELECT 'contracts', count(*) FROM contracts
UNION ALL SELECT 'invoices', count(*) FROM invoices
UNION ALL SELECT 'payments', count(*) FROM payments
UNION ALL SELECT 'disputes', count(*) FROM disputes
UNION ALL SELECT 'collection_actions', count(*) FROM collection_actions;

-- Expected zero orphan invoices.
SELECT count(*) AS orphan_invoices
FROM invoices i LEFT JOIN contracts c ON c.id = i.contract_id
LEFT JOIN customers cu ON cu.id = i.customer_id
WHERE c.id IS NULL OR cu.id IS NULL OR c.customer_id <> i.customer_id;

-- Expected zero inconsistent net amounts.
SELECT count(*) AS inconsistent_amounts FROM invoices
WHERE net_cents <> gross_cents - discount_cents OR net_cents <= 0;

-- Aging snapshot; BRL cents remain integral until final display conversion.
WITH paid AS (
    SELECT invoice_id,
           sum(CASE WHEN status = 'confirmed' THEN amount_cents
                    WHEN status = 'reversed' THEN -amount_cents ELSE 0 END) AS cents
    FROM payments WHERE CAST(paid_on AS DATE) <= DATE '2026-11-01'
    GROUP BY invoice_id
), balances AS (
    SELECT i.id, greatest(i.net_cents - coalesce(p.cents, 0), 0) AS balance_cents,
           date_diff('day', CAST(i.due_on AS DATE), DATE '2026-11-01') AS days_late
    FROM invoices i LEFT JOIN paid p ON p.invoice_id = i.id
    WHERE i.status <> 'cancelled'
), aging AS (
    SELECT *, CASE WHEN days_late <= 0 THEN 'a vencer'
         WHEN days_late <= 15 THEN '01-15' WHEN days_late <= 30 THEN '16-30'
         WHEN days_late <= 60 THEN '31-60' WHEN days_late <= 90 THEN '61-90'
         ELSE '>90' END AS bucket
    FROM balances WHERE balance_cents > 0
)
SELECT bucket, count(*) AS invoices, sum(balance_cents) AS balance_cents
FROM aging GROUP BY bucket ORDER BY bucket;
