# Atlas local: entrega incremental

Todos os documentos e registros Atlas são fictícios. Não conectar dados reais.

## Base de conhecimento (Fase A)

No `.env` do Compose, configure `ATLAS_SEED_KNOWLEDGE=true` e inicie o gateway
com `docker compose up --build`. Fora do Docker, defina
`$env:Demo__Atlas__SeedKnowledge='true'` antes de `dotnet run --project ai-gateway`.
O padrão é desativado. O snapshot acompanha o assembly, inclusive em publish/Docker.

Entre com uma conta autorizada e abra `/kb`. Os dez IDs começam com `atlas-`;
`Section` é a categoria e `Subsection` identifica `synthetic-demo / v1`. O aviso
no corpo acompanha a recuperação textual. A consulta exige `kb.view`, a manutenção
`kb.manage`; o seed não concede permissões e não altera `organization_base`.

Reiniciar não duplica nem atualiza documentos existentes, mesmo editados ou
inativos. Desligar o seed não remove registros. Para retirar a demonstração,
desative os dez documentos na tela existente e mantenha o seed desligado.
Não há migration nova nem rollback de esquema. A transação evita seed parcial.
O snapshot v1 é imutável; versões futuras exigem política explícita, sem atualizar
conteúdo humano automaticamente. Não renomeie IDs se precisar preservar a
identidade do documento no seed.

O RAG local encontra documentos ativos usando o mecanismo textual existente.
Com RAG avançado, configure ONNX/Qdrant e execute `POST /api/kb/reindex-all` com
JWT autorizado. Confira `falhas=0`; o seed não promete índices vetoriais prontos.
Sem modelos, o modo local continua disponível. A KB é global desta instalação,
não há segregação por organização; use instalação demonstrativa dedicada.

## Dataset offline (Fase B)

```powershell
python scripts/atlas_dataset.py --output artifacts/atlas-v1
python -m unittest discover -s tests -p test_atlas_dataset.py -v
```

O destino deve ser novo: o gerador recusa sobrescrever arquivos. Seed padrão 42,
referência fixa 2026-11-01, competências 2025-11 até 2026-10. Para reproduzir,
gere em outro diretório e compare `manifest.json` e SHA-256 de cada CSV.
Não usa rede, pacotes extras ou o SQLite do portal. Arquivos ficam em `artifacts/`,
ignorado pelo Git. Não exibir estes valores como indicadores reais de empresas.

| CSV | Chaves e significado |
| --- | --- |
| customers | id; nome explicitamente fictício e região, sem documentos ou contatos |
| plans | id; nome, mensalidade em centavos |
| contracts | id; customer_id, plan_id; início, cancelamento opcional, estado |
| invoices | id; customer_id, contract_id; competência, emissão, vencimento, bruto/desconto/líquido em centavos, estado |
| payments | id; invoice_id; data, valor em centavos, confirmed ou reversed |
| disputes | id; invoice_id; abertura, motivo sintético, estado |
| collection_actions | id; invoice_id; data, ação proposta, estado; não executa cobrança |

São 500 clientes; cancelamentos encerram emissões futuras. Há sazonalidade
em dezembro, concentração de receita, atrasos e pagamentos parciais. Estornos
são eventos positivos com status `reversed`, subtraídos dos pagamentos confirmados;
não somar ambos como caixa. Negociações são propostas sem mudança do saldo.

Fórmulas: líquido = bruto - desconto; pagamento efetivo = confirmed - reversed;
saldo = máximo(líquido - pagamento efetivo, 0), excluindo faturas canceladas;
vencido = saldo com vencimento anterior à referência; taxa = vencido / saldo,
indefinida se denominador zero. Caixa usa data de pagamento, faturamento usa
competência. Converta centavos para DECIMAL/100, nunca use ponto flutuante para
conciliação. A data de referência não é o dia atual.

Os CSV UTF-8 com cabeçalho, IDs inteiros e datas ISO são a camada de intercâmbio
para um catálogo Trino local (por exemplo, importação em tabelas Hive/Iceberg).
Defina montantes como BIGINT, datas como DATE, demais campos como VARCHAR e
trate cancelled_on vazio como NULL. CSV não é um catálogo Trino: nenhum servidor,
catálogo ou carga foi criado/testado nesta entrega. Não há indicadores no chat.
O manifest contém contagens e hashes; os testes verificam todas as FKs,
conciliação, datas, cinco faixas de atraso e reprodutibilidade byte a byte.

## Limite das próximas fases

Data Connectors e chat híbrido não foram habilitados. O portal não tem entidades
de organização/membership. É necessária uma decisão de migração de usuários,
grupos e KB existentes para organizações, antes de garantir isolamento.
O cliente Trino atual também precisa de validação SQL estrutural, allowlists,
limites, cancelamento e validação de destinos de rede. Não conectar fontes reais
pelas rotas SQL legadas. Mermaid precisa de renderização isolada e fallback
textual; o renderizador existente ainda não atende à Fase E.
