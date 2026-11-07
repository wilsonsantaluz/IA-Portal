# Entrega Atlas BI — 07/11/2026

Branch: `feature/atlas-business-intelligence`. A organização padrão e a separação
de Atlas foram autorizadas pelo proprietário. Não houve push, merge, deploy,
reset de banco ou alteração dos volumes da instalação existente.

## Resultado

- Dez documentos v1 com seed opt-in, idempotente e sem sobrescrever edições.
- Dataset determinístico com 500 clientes, 12 meses, 5.896 faturas e 5.561 eventos
  de pagamento/estorno. Todos os registros são sintéticos.
- Organizações e memberships, grupos locais, KB/RAG/conversas/avaliações/consumo
  isolados pela organização validada no servidor.
- Data Connectors com perfis aprovados no servidor, credenciais por ambiente,
  teste, descoberta, tabelas selecionadas, grants por grupo, estado e CAS.
- Chat existente documental, analítico e híbrido, com quatro planos financeiros
  fechados, proveniência, tabela conferível e gráficos. SQL livre bloqueado.
- Mermaid com saída isolada, sem scripts/rede, e fallback para o código original.
  Shell, temas, autenticação, histórico, títulos, consumo e OCR preservados.

O classificador é determinístico, não um agente SQL genérico. A seleção de fonte
e data é explícita; simulações e perguntas não suportadas pedem esclarecimento.
Detalhes: [chat](ATLAS_CHAT.md), [conectores](DATA_CONNECTORS.md).

## Migrations e preservação

| Versão | Alteração | Validação |
| --- | --- | --- |
| 005 | Organizations, OrganizationMemberships e OrganizationId em grupos, KB, sessões e avaliações; legado na organização padrão | v4 real, backup consistente anterior, restauração, novo upgrade, idempotência e falha DDL com rollback |
| 006 | DataConnectors e ConnectorGrants; FK e proteção de grants entre organizações | v5 real, backup/restauração, idempotência e rollback de DDL/versão/dados |

As migrations são aditivas e versionadas com checksum. Nenhuma tabela existente
foi apagada ou recriada. Os testes usam bancos temporários; a instalação atual
não foi migrada durante esta execução. O rollback operacional restaura o backup
completo com a versão anterior em caminho novo, sem downgrade destrutivo de colunas.

## Testes executados

Todos os itens abaixo passaram; comandos completos e escopos também constam no
[diário](../delivery/IMPLEMENTATION_LOG.md).

| Comando | Resultado observado |
| --- | --- |
| `dotnet build ai-gateway/AiGateway.csproj --no-restore` | Sem erros; 11 avisos preexistentes após remover o executor legado (antes eram 13) |
| `dotnet run --project tests/AtlasKnowledge/AtlasKnowledge.csproj --no-restore` | Seed, opt-in, rollback, idempotência, edições/inatividade preservadas |
| `dotnet run --project tests/Organizations/Organizations.csproj --no-restore` | Migração, backup/restore/rollback, legado e memberships |
| `dotnet run --project tests/ChatSessions/ChatSessions.csproj --no-restore` | Dono, histórico, paginação, replay, falhas, exclusão lógica e migrations |
| `dotnet run --project tests/ChatTitles/ChatTitles.csproj --no-restore` | Títulos, fallback, edição manual, concorrência e usage auxiliar |
| `dotnet run --project tests/Usage/Usage.csproj --no-restore` | Contabilização, preços opcionais, agregações e isolamento |
| `dotnet run --project tests/Providers/Providers.csproj --no-restore` | Adapters e seleção, sem inferência externa |
| `dotnet run --project tests/access/SeedSmoke/SeedSmoke.csproj --no-restore` | Bootstrap/RBAC/KB existentes preservados |
| `dotnet run --project tests/BusinessChat/BusinessChat.csproj --no-restore` | Intenção, egress explícito e fonte indisponível/ausente sem métricas inventadas |
| `dotnet run --project tests/Connectors/Connectors.csproj --no-restore -- --real-trino` | SQL adversarial, rede/limites, migration v6 e quatro planos no Trino real; conciliação offline e negação real de escrita/system/tabela não autorizada |
| `python -m unittest discover -s tests -p test_atlas_dataset.py -v` | Quatro testes: determinismo, cenários, integridade e arquivos reproduzíveis |
| `python scripts/atlas_trino_load.py` (duas execuções) | Carga real e reexecução sem escrita/duplicação |
| `python scripts/atlas_trino_load.py --verify` | Quatro SELECTs do repositório conferidos no Trino real |
| `python scripts/smoke_local.py --atlas` | API/SQLite reais; Trino/LLM/OCR doubles, incluindo falha/vazio e histórico sem reenvio implícito de SQL |
| `python scripts/smoke_local.py --atlas --real-trino` | Gateway/SQLite e Trino reais; fluxo híbrido SSE persistido; LLM/OCR doubles |
| `npm run typecheck --prefix front` e `npm run build --prefix front` | Passaram; aviso de chunks grandes no Vite |
| `node tests/ui/atlas-security.cjs` | Validação de gráficos e rejeição de Mermaid inseguro |
| `node tests/ui/atlas.cjs` com Playwright/Chrome/axe existentes | Ambos os temas, conectores, seletor fonte/data, gráfico visível, sandbox/fallback, 390/320px, sincronização entre abas; APIs de navegador são fixtures |
| `docker build -t ia-portal-atlas-api:validation ./ai-gateway` | Publish do backend e recursos incorporados |
| `docker build -t ia-portal-atlas-front:validation ./front` | Typecheck/build e imagem Nginx |
| `python scripts/smoke_atlas_docker.py` | Containers descartáveis, SQLite tmpfs, seed/autenticação/organizações, conector e valor exato no Trino real; frontend servido por Nginx |
| `docker compose -f docker-compose.yml -f docker-compose.atlas.yml config --quiet` | Override validado sem iniciar a instalação existente |
| `git diff --check` | Sem erros de whitespace |

Os builds de OCR/ML passaram na Fase A; seus fontes não foram alterados. Os
testes de OCR nesta entrega usam double HTTP, não Tesseract real.

Capturas e resultado visual locais: `artifacts/atlas-ui/` (ignorados pelo Git).
Não representam dados ou credenciais reais. O teste axe cobre o main de chat
mobile nos dois temas, não certifica a aplicação inteira.

## Executar localmente

O [guia local](ATLAS_LOCAL.md) contém os dois caminhos completos: Compose com
override opt-in e gateway nativo com Trino isolado. Para Compose, com `.env`
já configurado:

```powershell
docker compose -f docker-compose.yml -f docker-compose.atlas.yml up --build -d
# Aguarde atlas-trino saudável:
python scripts/atlas_trino_load.py
python scripts/atlas_trino_load.py --verify
```

Abra `http://127.0.0.1:9080`, selecione Atlas, cadastre `atlas_local` em Data
Connectors, escolha `invoices`/`payments`, conceda o grupo apropriado, habilite e
teste. No chat, selecione a fonte e a referência `2026-11-01`.

## Limitações

- Trino real foi validado somente no dataset sintético local. Fontes enterprise
  e públicas precisam de perfis, credenciais, TLS/ACLs e revisão de licença/origem;
  nenhuma foi cadastrada ou alegada como validada.
- ONNX/Qdrant reais e modelos pagos não foram executados. A KB vetorial exige
  reindex explícito com `organizationId`; pontos antigos ficam fora da busca.
- O usuário escolhe a data; datas livres no texto não substituem esse controle.
  Planos financeiros exigem as colunas e semântica Atlas. Não há SQL genérico.
- Trino Memory é descartável; recarregue após reinício. A carga recusa schema
  divergente/parcial e não executa DROP/TRUNCATE. Contas locais sem senha são
  exclusivas da demonstração isolada, não autenticação empresarial.
- Não há UI de criação de organizações ou associação de uma conta existente a
  outra organização. Atlas recebe administradores iniciais no seed; novas contas
  são criadas na organização ativa. Gestão global de provedores fica na padrão.
- O override completo não foi iniciado sobre os volumes existentes: validaram-se
  sua configuração e os containers descartáveis. Nenhum teste de produção foi feito.

## Arquivos alterados

Inventário relativo à `main` sincronizada no início da tarefa, incluindo os
commits das fases A/B e da continuação. A lista abaixo é gerada do Git.

<!-- atlas-file-inventory -->

103 arquivos:

- [.env.example](../../.env.example)
- [.gitignore](../../.gitignore)
- [README.md](../../README.md)
- [ai-gateway/.dockerignore](../../ai-gateway/.dockerignore)
- [ai-gateway/AiGateway.csproj](../../ai-gateway/AiGateway.csproj)
- [ai-gateway/Application/Interfaces/ChatGrounding.cs](../../ai-gateway/Application/Interfaces/ChatGrounding.cs)
- [ai-gateway/Application/Interfaces/IChatRepository.cs](../../ai-gateway/Application/Interfaces/IChatRepository.cs)
- [ai-gateway/Application/Interfaces/ITrinoMetadataService.cs](../../ai-gateway/Application/Interfaces/ITrinoMetadataService.cs)
- [ai-gateway/Application/Interfaces/ITrinoQueryService.cs](../../ai-gateway/Application/Interfaces/ITrinoQueryService.cs)
- [ai-gateway/Application/SeedWork/AtlasKnowledgeSeed.cs](../../ai-gateway/Application/SeedWork/AtlasKnowledgeSeed.cs)
- [ai-gateway/Application/SeedWork/atlas-v1.json](../../ai-gateway/Application/SeedWork/atlas-v1.json)
- [ai-gateway/Controllers/Auth/AuthController.cs](../../ai-gateway/Controllers/Auth/AuthController.cs)
- [ai-gateway/Controllers/Chat/ChatBotController.cs](../../ai-gateway/Controllers/Chat/ChatBotController.cs)
- [ai-gateway/Controllers/Chat/SmartAskController.cs](../../ai-gateway/Controllers/Chat/SmartAskController.cs)
- [ai-gateway/Controllers/Chat/SqlController.cs](../../ai-gateway/Controllers/Chat/SqlController.cs)
- [ai-gateway/Controllers/Connectors/DataConnectorsController.cs](../../ai-gateway/Controllers/Connectors/DataConnectorsController.cs)
- [ai-gateway/Controllers/KnowledgeBase/KnowledgeBaseController.cs](../../ai-gateway/Controllers/KnowledgeBase/KnowledgeBaseController.cs)
- [ai-gateway/Controllers/Organizations/OrganizationsController.cs](../../ai-gateway/Controllers/Organizations/OrganizationsController.cs)
- [ai-gateway/Controllers/Roles/RolesController.cs](../../ai-gateway/Controllers/Roles/RolesController.cs)
- [ai-gateway/Controllers/Users/UsersController.cs](../../ai-gateway/Controllers/Users/UsersController.cs)
- [ai-gateway/Dockerfile](../../ai-gateway/Dockerfile)
- [ai-gateway/Dtos/AskSmartRequest.cs](../../ai-gateway/Dtos/AskSmartRequest.cs)
- [ai-gateway/Infrastructure/EF/ChatSchemaMigrator.cs](../../ai-gateway/Infrastructure/EF/ChatSchemaMigrator.cs)
- [ai-gateway/Infrastructure/EF/ChatTitleService.cs](../../ai-gateway/Infrastructure/EF/ChatTitleService.cs)
- [ai-gateway/Infrastructure/EF/Context/DataConnector.cs](../../ai-gateway/Infrastructure/EF/Context/DataConnector.cs)
- [ai-gateway/Infrastructure/EF/Context/PortalDbContext.cs](../../ai-gateway/Infrastructure/EF/Context/PortalDbContext.cs)
- [ai-gateway/Infrastructure/EF/DatabaseInitializer.cs](../../ai-gateway/Infrastructure/EF/DatabaseInitializer.cs)
- [ai-gateway/Infrastructure/EF/Migrations/005_Organizations.sql](../../ai-gateway/Infrastructure/EF/Migrations/005_Organizations.sql)
- [ai-gateway/Infrastructure/EF/Migrations/006_DataConnectors.sql](../../ai-gateway/Infrastructure/EF/Migrations/006_DataConnectors.sql)
- [ai-gateway/Infrastructure/EF/Repository/ChatRepository.cs](../../ai-gateway/Infrastructure/EF/Repository/ChatRepository.cs)
- [ai-gateway/Infrastructure/EF/Repository/KnowledgeBaseRepository.cs](../../ai-gateway/Infrastructure/EF/Repository/KnowledgeBaseRepository.cs)
- [ai-gateway/Infrastructure/Qdrant/QdrantClient.cs](../../ai-gateway/Infrastructure/Qdrant/QdrantClient.cs)
- [ai-gateway/Infrastructure/Qdrant/Services/Chats/BusinessChatContextService.cs](../../ai-gateway/Infrastructure/Qdrant/Services/Chats/BusinessChatContextService.cs)
- [ai-gateway/Infrastructure/Qdrant/Services/Chats/KnowledgeBaseRagService.cs](../../ai-gateway/Infrastructure/Qdrant/Services/Chats/KnowledgeBaseRagService.cs)
- [ai-gateway/Infrastructure/Qdrant/Services/Chats/LocalChatContextService.cs](../../ai-gateway/Infrastructure/Qdrant/Services/Chats/LocalChatContextService.cs)
- [ai-gateway/Infrastructure/Qdrant/Services/Chats/VectorChatContextService.cs](../../ai-gateway/Infrastructure/Qdrant/Services/Chats/VectorChatContextService.cs)
- [ai-gateway/Infrastructure/Qdrant/Services/KnowledgeBaseService.cs](../../ai-gateway/Infrastructure/Qdrant/Services/KnowledgeBaseService.cs)
- [ai-gateway/Infrastructure/trino/ApprovedTrinoQuery.cs](../../ai-gateway/Infrastructure/trino/ApprovedTrinoQuery.cs)
- [ai-gateway/Infrastructure/trino/ConnectorProfiles.cs](../../ai-gateway/Infrastructure/trino/ConnectorProfiles.cs)
- [ai-gateway/Infrastructure/trino/DataConnectorService.cs](../../ai-gateway/Infrastructure/trino/DataConnectorService.cs)
- [ai-gateway/Infrastructure/trino/TrinoMetadataService.cs](../../ai-gateway/Infrastructure/trino/TrinoMetadataService.cs)
- [ai-gateway/Infrastructure/trino/TrinoQueryService.cs](../../ai-gateway/Infrastructure/trino/TrinoQueryService.cs)
- [ai-gateway/Infrastructure/trino/TrinoRestClient.cs](../../ai-gateway/Infrastructure/trino/TrinoRestClient.cs)
- [ai-gateway/Program.cs](../../ai-gateway/Program.cs)
- [ai-gateway/Security/OrganizationScope.cs](../../ai-gateway/Security/OrganizationScope.cs)
- [ai-gateway/Security/PortalAccess.cs](../../ai-gateway/Security/PortalAccess.cs)
- [ai-gateway/config/trino-sources.example.json](../../ai-gateway/config/trino-sources.example.json)
- [docker-compose.atlas.yml](../../docker-compose.atlas.yml)
- [docker-compose.yml](../../docker-compose.yml)
- [docs/README.md](../../docs/README.md)
- [docs/architecture/API_CONTRACTS.md](../../docs/architecture/API_CONTRACTS.md)
- [docs/architecture/AS_IS_AUDIT.md](../../docs/architecture/AS_IS_AUDIT.md)
- [docs/architecture/DATA_MODEL.md](../../docs/architecture/DATA_MODEL.md)
- [docs/delivery/IMPLEMENTATION_LOG.md](../../docs/delivery/IMPLEMENTATION_LOG.md)
- [docs/demo/ATLAS_CHAT.md](../../docs/demo/ATLAS_CHAT.md)
- [docs/demo/ATLAS_DELIVERY_REPORT.md](../../docs/demo/ATLAS_DELIVERY_REPORT.md)
- [docs/demo/ATLAS_LOCAL.md](../../docs/demo/ATLAS_LOCAL.md)
- [docs/demo/DATA_CONNECTORS.md](../../docs/demo/DATA_CONNECTORS.md)
- [docs/security/SECURITY_REQUIREMENTS.md](../../docs/security/SECURITY_REQUIREMENTS.md)
- [front/.dockerignore](../../front/.dockerignore)
- [front/src/App.tsx](../../front/src/App.tsx)
- [front/src/auth/AuthProvider.tsx](../../front/src/auth/AuthProvider.tsx)
- [front/src/components/BusinessSourcePicker.tsx](../../front/src/components/BusinessSourcePicker.tsx)
- [front/src/components/FinancialChart.tsx](../../front/src/components/FinancialChart.tsx)
- [front/src/components/MarkdownRenderer.tsx](../../front/src/components/MarkdownRenderer.tsx)
- [front/src/components/MermaidRenderer.tsx](../../front/src/components/MermaidRenderer.tsx)
- [front/src/components/financialChartData.ts](../../front/src/components/financialChartData.ts)
- [front/src/hooks/useChatLogic.ts](../../front/src/hooks/useChatLogic.ts)
- [front/src/layout/DashboardLayout.tsx](../../front/src/layout/DashboardLayout.tsx)
- [front/src/layout/HeaderBar.tsx](../../front/src/layout/HeaderBar.tsx)
- [front/src/layout/Sidebar.tsx](../../front/src/layout/Sidebar.tsx)
- [front/src/pages/ChatPage.tsx](../../front/src/pages/ChatPage.tsx)
- [front/src/pages/DataConnectorsPage.tsx](../../front/src/pages/DataConnectorsPage.tsx)
- [front/src/services/api.ts](../../front/src/services/api.ts)
- [scripts/atlas_dataset.py](../../scripts/atlas_dataset.py)
- [scripts/atlas_trino_load.py](../../scripts/atlas_trino_load.py)
- [scripts/smoke_atlas_docker.py](../../scripts/smoke_atlas_docker.py)
- [scripts/smoke_local.py](../../scripts/smoke_local.py)
- [tests/AtlasKnowledge/AtlasKnowledge.csproj](../../tests/AtlasKnowledge/AtlasKnowledge.csproj)
- [tests/AtlasKnowledge/Program.cs](../../tests/AtlasKnowledge/Program.cs)
- [tests/BusinessChat/BusinessChat.csproj](../../tests/BusinessChat/BusinessChat.csproj)
- [tests/BusinessChat/Program.cs](../../tests/BusinessChat/Program.cs)
- [tests/ChatSessions/ChatSessions.csproj](../../tests/ChatSessions/ChatSessions.csproj)
- [tests/ChatSessions/Program.cs](../../tests/ChatSessions/Program.cs)
- [tests/Connectors/Connectors.csproj](../../tests/Connectors/Connectors.csproj)
- [tests/Connectors/MigrationChecks.cs](../../tests/Connectors/MigrationChecks.cs)
- [tests/Connectors/Program.cs](../../tests/Connectors/Program.cs)
- [tests/Connectors/RealTrinoChecks.cs](../../tests/Connectors/RealTrinoChecks.cs)
- [tests/Fixtures/PreOrganizationContext.cs](../../tests/Fixtures/PreOrganizationContext.cs)
- [tests/Organizations/Organizations.csproj](../../tests/Organizations/Organizations.csproj)
- [tests/Organizations/Program.cs](../../tests/Organizations/Program.cs)
- [tests/access/SeedSmoke/Program.cs](../../tests/access/SeedSmoke/Program.cs)
- [tests/test_atlas_dataset.py](../../tests/test_atlas_dataset.py)
- [tests/ui/atlas-security.cjs](../../tests/ui/atlas-security.cjs)
- [tests/ui/atlas.cjs](../../tests/ui/atlas.cjs)
- [trino/atlas/access-control.properties](../../trino/atlas/access-control.properties)
- [trino/atlas/compose.yml](../../trino/atlas/compose.yml)
- [trino/atlas/config.properties](../../trino/atlas/config.properties)
- [trino/atlas/expected-v1.json](../../trino/atlas/expected-v1.json)
- [trino/atlas/memory.properties](../../trino/atlas/memory.properties)
- [trino/atlas/rules.json](../../trino/atlas/rules.json)
- [trino/atlas/sources.compose.json](../../trino/atlas/sources.compose.json)
- [trino/examples/atlas-verification.sql](../../trino/examples/atlas-verification.sql)
