-- Demonstração inteiramente sintética. Requer um catálogo memory no Trino.
CREATE SCHEMA IF NOT EXISTS memory.financeiro;
CREATE TABLE IF NOT EXISTS memory.financeiro.recebiveis (
    id BIGINT,
    carteira_id VARCHAR,
    cliente_id VARCHAR,
    vencimento DATE,
    valor_previsto DECIMAL(18,2),
    valor_recebido DECIMAL(18,2)
);
-- Execute estes INSERTs apenas uma vez em uma instância de demonstração vazia.
INSERT INTO memory.financeiro.recebiveis VALUES
    (1, 'CARTEIRA-DEMO-A', 'CLIENTE-DEMO-001', DATE '2026-01-15', 1000.00, 1000.00),
    (2, 'CARTEIRA-DEMO-A', 'CLIENTE-DEMO-002', DATE '2026-02-15', 1500.00, 750.00),
    (3, 'CARTEIRA-DEMO-B', 'CLIENTE-DEMO-003', DATE '2026-03-15', 2000.00, 0.00);
