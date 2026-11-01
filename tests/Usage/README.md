# Consumo de IA — etapa 3

Execute na raiz:

```powershell
dotnet run --project tests/Usage
dotnet run --project tests/ChatSessions
dotnet run --project tests/ChatTitles
dotnet run --project tests/access/SeedSmoke
python scripts/smoke_local.py
npm --prefix front run typecheck
npm --prefix front run build
docker build -t ia-portal-usage-api ./ai-gateway
```

`Usage` cria somente SQLite temporário com usuários e registros sintéticos. Constrói fixture v2, injeta falha DDL v3, verifica rollback/backup/checksum, preservação, idempotência, custos com preços sintéticos versionados, cache e tokens indisponíveis, vigência futura, permissões e consultas SQLite. Exercita o executor com mock, replay, falhas/timeouts, retenção após exclusão e autorização anterior ao provedor. Testa o adapter real com handler HTTP em memória: métricas no último chunk com conteúdo/choices vazio, modelo resolvido, EOF prematuro e resposta sem métricas. Nenhuma chamada remota ou banco diário é usado.

O smoke HTTP executa gateway e doubles locais com banco temporário; verifica totalização por usuário/conversa, grupos dia/mês/modelo/provider/finalidade/usuário, paginação, rejeição A/B/admin, custo nulo sem preço e persistência após reinício. `model-prices.json` continua vazio: os números de teste são fixtures de aritmética, não preços do provedor.

Nesta etapa não foram criadas telas ou alterado código frontend. O navegador visual e ONNX/Qdrant não precisam ser acionados para testar essas consultas; o smoke usa a API real. A integração externa real, sua fatura e a qualidade do modelo não são validadas por estes testes.
