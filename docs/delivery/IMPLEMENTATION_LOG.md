# Diário de implementação

> Documento vivo. Atualizar após cada etapa; **não declarar testes não executados**.

## Modelo de registro

### YYYY-MM-DD — Etapa X — Título

- **Branch/commit:**
- **Objetivo:**
- **Estado anterior / evidência:**
- **Arquivos alterados:**
- **Mudanças de API/banco:**
- **Migração/backup/rollback:**
- **Comandos executados:**
- **Testes aprovados:**
- **Testes falhos ou não executados:**
- **Riscos e pendências:**
- **Decisão/revisão do proprietário:**

## Estado inicial conhecido

- O proprietário confirmou execução local via Docker e resposta do DeepSeek.
- A interface atual foi refatorada pelo Codex; menus administrativos aparecem.
- Histórico por usuário, título automático, medição de tokens e múltiplos provedores **ainda exigem verificação técnica**.

### 2026-11-01 — Etapa 0 — Auditoria e proposta para revisão

- **Branch/base:** `feature/chat-sessions-providers`, criada a partir de `ec9d14e`. A implementação de usuários/grupos já está commitada em `f44b803` e incorporada à main pelo PR #4; nenhum commit duplicado dessa entrega foi criado.
- **Objetivo:** confrontar a documentação fornecida pelo proprietário com controllers, entidades, repositórios, frontend, adapters e inicialização reais. Respeitar a parada de revisão explícita do roadmap.
- **Estado anterior:** pacote técnico novo e atualização de AGENTS ainda não rastreados/commitados; código de usuários limpo. Preservados e incorporados ao registro técnico.
- **Diagnóstico:** mensagens persistem em SQLite, mas há uma sessão por navegador/conta, sem CRUD/lista/títulos/uso. Existe vínculo JWT para novas sessões e filtro de leitura, porém faltam verificações em cada escrita e autorização antecipada de OCR/SSE. DeepSeek ativo; OpenAI/Ollama existem, mas não estão selecionados no chat. Credencial literal no Ollama e logging de payloads são bloqueios registrados, sem transcrição/teste do segredo.
- **Arquivos alterados:** `AGENTS.md`, README raiz e índice em `docs/README.md`; auditoria e proposta em `docs/architecture/{AS_IS_AUDIT,DATA_MODEL,API_CONTRACTS}.md`; roadmap/diário; complemento de segurança; notas do design system e marcação da documentação visual antiga como histórico. Documentos conceituais de features e referências visuais fornecidos foram incluídos, sem alterar o frontend.
- **Mudanças de API/banco:** nenhuma. Contratos, tabelas/campos, índices, registro de chamadas e runner SQL versionado são propostas, não implementação. `.env.example` permanece como está porque nenhuma variável foi introduzida neste incremento.
- **Migração/backup/rollback:** somente plano. Não foi aberto/copiado/migrado/restaurado o SQLite diário. Nenhum container foi reconstruído ou recriado; testes usam dados sintéticos temporários.

Comandos executados e resultados:

| Comando | Resultado efetivo |
| --- | --- |
| `git status --short`, `git log -3 --oneline`, leitura dos manifests/fontes/docs | Auditoria realizada; último merge da etapa anterior confirmado |
| `git switch -c feature/chat-sessions-providers` | Branch criada; sem alteração direta na main |
| `dotnet build ai-gateway/AiGateway.csproj --no-restore -v quiet` | Passou, 0 erros e 14 avisos preexistentes |
| `node front/node_modules/typescript/bin/tsc --noEmit -p front/tsconfig.json` | Passou |
| `node node_modules/vite/bin/vite.js build` em `front/` | Passou; aviso preexistente de chunk maior que 500 kB |
| `python scripts/smoke_local.py` | Passou: criação SQLite, bootstrap/login, KB, streaming/histórico, avaliações, rota OCR e persistência após reinício; LLM/OCR simulados |
| `dotnet run --project tests/access/SeedSmoke` | Passou: inicialização idempotente preserva fixture de usuários/senhas/grupos/KB e não eleva não administradores |

- **Não executado:** nova suíte de CRUD de conversas (não implementado); títulos/usage/providers; migração de esquema e restauração; testes reais de OpenAI/Ollama/DeepSeek; suíte de navegador, RAG/ONNX, Docker builds ou builds OCR/ML. Nenhuma chamada paga foi realizada. Builds demonstram compilação, não validação de integração externa.
- **Fontes externas:** documentação oficial de usage DeepSeek, OpenAI e Ollama e `EnsureCreated`/EF Core, vinculada na auditoria; sem adoção de preços ou troca de modelo. Skill OpenAI Docs usada para a consulta de contrato OpenAI; nenhuma chave solicitada ou necessária para leitura de documentação.
- **Riscos e pendências:** resolver segredo literal/logging antes de habilitar adapters alternativos; endurecer posse em OCR/SSE/repositório; revisão do plano de quarentena dos legados, exclusão lógica, backup e migração antes de implementação. Remover segredo do código não revoga seu valor nem o histórico; revogação exige ação do responsável no provedor.
- **Decisão/revisão do proprietário:** pendente. `ROADMAP.md` etapa 0 manda **Parar para revisão**; `CODEX_START_HERE.md` determina primeira tarefa somente auditoria. Não iniciadas etapas 1–5, nem criadas entidades/APIs de fase posterior.

### 2026-11-01 — Refinamento do modelo conceitual

- **Branch:** `feature/chat-sessions-providers`.
- **Entrada do proprietário:** diagrama Users → ChatSessions → ChatMessages/LlmUsage e LlmUsage → AI Providers, com orientação de adaptar às entidades existentes.
- **Alteração:** `docs/architecture/DATA_MODEL.md` recebeu diagrama Mermaid, mapeamento de nomes e limites entre metadados da sessão, conteúdo das mensagens e consumo por chamada. Especificados vínculo opcional de mensagem, identidade estável de provedor, preservação de consumo após desabilitação e testes administrativos sem conversa fictícia.
- **Implementação/API/banco:** nenhuma alteração; não foram criadas tabelas paralelas. A associação à configuração será adicionada na etapa correspondente, com migração dos identificadores previamente registrados.
- **Validação:** revisão documental e `git diff --check`. Nenhum build ou teste funcional repetido para esta alteração exclusivamente documental.
- **Revisão:** diagrama incorporado como orientação conceitual; não representa aprovação presumida das decisões de migração, exclusão ou contratos ainda em revisão.

### 2026-11-01 — Etapa 1 — Conversas persistentes por usuário

- **Branch:** `feature/chat-sessions-providers`.
- **Aprovação:** o proprietário confirmou “esta aprovado, pode proseguir”, após revisão do diagnóstico e inclusão do diagrama Users → ChatSessions → ChatMessages/LlmUsage → AI Providers. O refinamento documental pendente foi preservado.
- **Escopo:** sessões criadas no servidor, isolamento por JWT em cada operação, CRUD, histórico paginado, interação persistida com estados/idempotência e interface integrada ao design existente. Títulos automáticos, usage, seleção de providers e configurações continuam para as próximas etapas.
- **Banco:** reutilizados `ChatSession`/`ChatMessage`; adicionados dono tipado, metadados, versão, exclusão lógica, request/fingerprint/status e lease de geração. Runner v1 com SQL embutido, checksum normalizado, validação de baseline/chaves e registro em `SchemaVersions`. Migração legada faz backup consistente em diretório privado antes do DDL; transação protege DDL/versão. Backup com conexão sem pooling. Sem inferência de dono para legados; nenhuma tabela nova de consumo ou provider neste incremento.
- **API:** `/api/chatBot/sessions` e subrotas de metadados/mensagens/renomeação/exclusão. SSE e OCR existentes foram evoluídos; exigem sessão previamente criada. IDs ausentes de interação são gerados por compatibilidade, mas replay exige requestId explícito. História alheia/inexistente/excluída retorna 404, inclusive no adaptador de history antigo. Administração não permite ler sessão alheia.
- **Persistência:** reserva/lease curta antes de rede; pergunta preservada em falha; resposta/estado/atividade na mesma transação. Reenvio concluído com mesmo ID e fingerprint não chama LLM novamente; conflitos retornam 409. Cursor de mensagens usa âncora validada na sessão e ordenação por horário + ID. Cancelamento de stream DeepSeek foi corrigido sem trocar o adapter.
- **Frontend:** lista agrupada por períodos, busca, seleção, nova conversa, diálogos de renomeação/exclusão, paginação e recuperação entre navegadores. Última seleção por conta no localStorage, fonte dos dados no servidor. IDs reais no histórico. Avisos de interação incompleta e preservação da entrada quando envio não é aceito. Painel de conversas com drawer em telas menores; temas preservados.
- **Arquivos principais:** `PortalDbContext.cs`, `ChatSchemaMigrator.cs`, `Migrations/001_ChatSessions.sql`, `DatabaseInitializer.cs`, `IChatRepository.cs`/`ChatRepository.cs`, DTOs de chat/sessões, controllers ChatBot/SmartAsk/OCR/SQL, helper de fingerprint, tratamento de erro no Program e leitura cancelável no DeepSeek; `useChatLogic.ts`, `ConversationPanel.tsx`, `ChatPage.tsx`, estado visual em `MessageItem.tsx`; `.env.example`, Compose, ignore de caches Python, testes e documentação.
- **Configuração nova:** `SQLITE_BACKUP_DIRECTORY`, padrão Compose `/app/data/backups` em volume persistido. Nenhuma chave nova introduzida nem `.env` real lido/exibido. O texto extraído para análise/persistência é limitado a 50.000 caracteres; análise de documentos maiores é parcial.

Validações executadas neste incremento:

| Comando | Resultado |
| --- | --- |
| `dotnet build ai-gateway/AiGateway.csproj --no-restore -v quiet` | Passou, 14 avisos preexistentes e 0 erros |
| `node front/node_modules/typescript/bin/tsc --noEmit -p front/tsconfig.json` | Passou |
| `docker build -t ia-portal-sessions-api C:/source/IA-Portal/ai-gateway` | Passou; publish Release |
| `docker build -t ia-portal-sessions-front C:/source/IA-Portal/front` | Passou; typecheck e Vite |
| `dotnet run --project tests/ChatSessions --no-restore` | Passou: stream bloqueado cancelável sem rede, migração/restauração/rollback, quarentena, checksum, isolamento do repositório, paginação com horários iguais, replay/conflito, lease expirada/cancelamento, versão de título e exclusão lógica |
| `dotnet run --project tests/access/SeedSmoke --no-restore` | Passou: usuários/senhas/grupos/KB preservados e seed idempotente |
| `python scripts/smoke_local.py` | Passou: CRUD/search, HTTP A/B inclusive OCR/SQL antes de chamadas externas, replay sem nova chamada, erro sanitizado, chat/OCR simulado e persistência após restart |
| `dotnet run --project tests/RagSmoke -- C:/source/IA-Portal` com Qdrant descartável | Passou: ONNX, busca/indexação Qdrant, reranking reais e stream com LLM simulado; segunda conta recebe 404 |
| `node tests/ChatSessions/ui.cjs` em ambiente Docker descartável | Passou nas imagens finais: conversas independentes, recarga, navegador novo, 52 mensagens paginadas, entrada preservada em envio recusado, exclusão confirmada, axe e 320/390 px nos dois temas |

- **Evidências:** capturas e resumo em `docs/design/screenshots/sessions/`; roteiro de reprodução em `tests/ChatSessions/README.md`. Contas e documentos sintéticos apenas. O mock de erro de envio do navegador é uma fixture HTTP 409; os demais fluxos administrativos de sessão usam API real e o LLM é um double HTTP local.
- **Problemas encontrados/corrigidos:** seletores do teste de UI agiam antes de a lista nova chegar; passaram a esperar o item correto. Rótulos com codificação incorreta foram corrigidos. Um teste adicional detectou que sondar EndOfStream fazia leitura síncrona; o adapter ativo agora usa ReadLineAsync com cancelamento, sem essa sondagem.
- **Limites:** sem testes de carga/múltiplas réplicas; implantação da migração exige um gateway e ausência de outro escritor durante startup. Não foram feitos OCR real, chamadas a provedores externos, títulos automáticos, preços/usage ou testes de OpenAI/Ollama. Seus bloqueios de segredo/log permanecem antes da habilitação futura. Builds conservam avisos preexistentes C#/chunk Vite acima de 500 kB. Não houve refatoração do Trino nem mudança de armazenamento.
- **Revisão seguinte:** etapa 2 (títulos) somente após revisão desta entrega, conforme roadmap incremental. Ela deverá registrar o consumo auxiliar em tabela própria, sem misturá-lo com sessões/mensagens.
- **Aplicação local:** aguardando conclusão da conferência final das imagens; será registrado abaixo o resultado efetivo, sem presumir implantação pelo build.


Resultado da aplicação local da etapa 1:

- As imagens finais de gateway/frontend foram aplicadas com `docker compose up -d --no-deps --no-build ai-gateway portal-front`, após `docker compose config --quiet` passar. Tags locais `ia-portal-ai-gateway:before-chat-sessions` e `ia-portal-portal-front:before-chat-sessions` preservam as imagens anteriores para procedimento de rollback com backup.
- Inicialização concluída e Swagger respondeu com as rotas de sessão. Um backup `pre-chat-v1-*.sqlite3` foi confirmado no volume privado, sem abrir ou imprimir seu conteúdo. A migração terminou antes de disponibilizar a API. Não foi feita restauração sobre o banco diário.
- Frontend em http://127.0.0.1:9080/login respondeu HTTP 200; APIs de sessões e mensagens responderam HTTP 401 sem autenticação pelo Nginx. Validação autenticada/funcional usou apenas a instalação descartável; não foram lidas credenciais, conversas ou documentos diários para esse teste.
- Qdrant, OCR e ML mantiveram IDs e horários de início. Qdrant retornou `all shards are ready`. Nenhum volume foi removido, nem histórico legado atribuído automaticamente. Nenhuma chamada real ao provedor foi feita na instalação diária.
- O último smoke HTTP também verificou tentativa de forjar dono/ID no POST de sessão e rejeição anterior a qualquer chamada dos doubles LLM/OCR; Trino foi explicitamente apontado a loopback indisponível para evitar acessos acidentais durante testes.
- `git diff --check` passou; links relativos de documentos foram conferidos antes do commit. Sem merge ou push automático. A etapa 2 continua não iniciada, aguardando revisão deste incremento.

### 2026-11-01 — Etapa 2 — Títulos automáticos e consumo auxiliar

- **Autorização/base:** após o merge informado pelo proprietário e “por favor prossiga”, árvore limpa em `main`, merge `5a3fd01`. Branch `feature/chat-sessions-providers` atualizada por fast-forward; sem edição/commit direto em main.
- **Escopo:** geração de título após primeira resposta persistida em chat SSE, chat sem streaming e OCR. Reutilizado o adapter DeepSeek via capacidade `IChatTitleGenerator`, sem ativar OpenAI/Ollama ou criar uma segunda pilha de chat. Até 1.000 elementos de texto da primeira pergunta, 64 tokens de saída, timeout de 10 segundos, título até 80 elementos. Modelo da primeira resposta; metadados finais recarregados pelo frontend existente.
- **Confiabilidade/segurança:** fallback e tentativa durável reservados em transação antes da chamada. Uma tentativa por sessão; sem retry pago automático. Filtro de dono/visibilidade em leitura/atualização; edição manual e exclusão durante geração vencem. Falha de título não desfaz a resposta. Interrupção após reserva deixa fallback e possível status `started` de resultado desconhecido. Conteúdo e segredos não vão para telemetria/logs.
- **Dados:** migração aditiva `002_TitleUsage.sql`, checksum e backup v2 para instalações v1. `LlmUsage` separada de mensagens/sessões, vinculada ao dono, sessão e primeira resposta, com finalidade `title`, provider/model, horário, duração, status, contadores reportados opcionais e fonte. Cache não é somado ao total. Não foram inventados preços/custos nem criado dashboard. Scripts v1 preservados.
- **Arquivos principais:** `ChatTitleService`, `IChatTitleGenerator`, método auxiliar no `DeepSeekService`, `PortalDbContext`, `ChatSchemaMigrator`, controllers de chat/OCR e registros DI. `GetSessionAsync` passou a ler sem tracking para não devolver metadados antigos após atualização condicional. Documentos de conversas, dados, APIs, uso, índice, roadmap e README atualizados. Novos testes em `tests/ChatTitles`; mocks/smoke anteriores adaptados. Sem nova variável de ambiente.
- **Validação executada:** `dotnet build ai-gateway/AiGateway.csproj --no-restore` passou (0 erros, 14 avisos preexistentes); `dotnet run --project tests/ChatTitles --no-restore` passou (6 grupos); `dotnet run --project tests/ChatSessions --no-restore` passou (4 grupos); `dotnet run --project tests/access/SeedSmoke --no-restore` passou; `python scripts/smoke_local.py` passou (3 grupos); `npm run typecheck` e `npm run build` em `front` passaram, com aviso conhecido de chunk acima de 500 kB.
- **Correções durante validação:** leitura de sessão com tracking retornava título antigo no mesmo escopo; corrigida com query sem tracking. Fixture SeedSmoke criava o modelo mais novo sem ledger, que o runner corretamente rejeitou; passou a usar o runner na preparação do banco vazio. Adapter tolera contadores ausentes/inválidos, preserva tokens de resposta vazia e usa Accept JSON na chamada auxiliar após streaming. Não foi relaxada a validação de esquema para acomodar fixtures.
- **Limites:** testes sem créditos externos, sem medir qualidade/disponibilidade do provedor remoto. Timeout de título acrescenta até 10 segundos à finalização da primeira interação; sem job em background. Sem migração de donos legados, títulos retroativos em lote, métricas completas de chat, agregações, custos ou seleção/configuração de providers. Não repetida regressão ONNX/Qdrant nesta etapa porque o caminho RAG não foi alterado.
- **Aplicação local/navegador:** resultados finais serão registrados abaixo após execução efetiva. Etapa 3 continua pendente de revisão desta entrega.

Resultado final da etapa 2:

- `docker build -t ia-portal-titles-api ./ai-gateway` concluiu na imagem final, mantendo avisos preexistentes de publicação. `node tests/ChatTitles/ui.cjs` passou nessa imagem com gateway/banco descartáveis, frontend existente e double HTTP local. A navegação inicial do navegador excedeu 30 segundos em uma tentativa com Docker ocupado; login direto nas duas portas respondeu 200 e o teste passou após usar timeout de navegação de 60 segundos/DOMContentLoaded, sem alteração no login da aplicação. Capturas sintéticas em `docs/design/screenshots/titles/`.
- Teste adicional de dois escopos concorrentes comprovou uma chamada de título. Upgrade v2 preservou IDs, dono e conteúdo de uma conversa v1, além dos usuários. Última execução de `ChatTitles` passou nos seis grupos. Nenhum crédito externo foi utilizado.
- Imagem anterior preservada em `ia-portal-ai-gateway:before-automatic-titles`. Após `docker compose config --quiet`, aplicada somente a API por `docker compose up -d --no-deps --no-build ai-gateway`. Startup/migração concluídos: Swagger HTTP 200; um backup `pre-chat-v2-*` confirmado no volume privado, sem ler conteúdo. Nenhum banco foi apagado/recriado.
- Em `http://127.0.0.1:9080`, login respondeu 200 e sessões sem JWT responderam 401. Verificação autenticada e de títulos foi feita na instalação descartável; não foram lidas conversas/chaves/credenciais diárias nem disparadas chamadas reais para validar o deploy. Qdrant/OCR/ML conservaram os mesmos IDs e horários de início; frontend não precisou ser reconstruído em Docker porque seu código permaneceu inalterado.
- Containers/rede de teste e helper HTTP removidos; arquivo temporário de credenciais sintéticas excluído. Links locais Markdown e `git diff --check` passaram. Sem push ou merge automático. Próxima entrega, após revisão: métricas completas por chamada/chat/usuário/período (etapa 3).

### 2026-11-01 — Etapa 3 — Consumo dos fluxos persistentes e consultas protegidas

- **Autorização/base:** proprietário informou o merge da etapa 2 e autorizou avançar. Árvore limpa em `main`, merge `8aadff7`; branch `feature/chat-sessions-providers` atualizada por fast-forward, sem commit direto em main.
- **Escopo:** metadados por chamada nos fluxos SSE, chat sem streaming, documentos e título. Reutilizado o adapter DeepSeek, com capacidade de métricas da instância por requisição, sem ativar provedores alternativos. Stream solicita usage, suporta métricas com conteúdo/choices vazio e rejeita EOF antes de DONE. Modelo efetivo retornado fica na telemetria. Removidos erros com bodies brutos do caminho não streaming ativo.
- **Persistência:** `ChatUsageService` valida dono e pergunta pendente antes da reserva e do acesso externo. Reserva durável; replay não duplica consumo; finalização independente do cancelamento registra estado, duração e contadores efetivamente recebidos. Processo interrompido/falha de gravação final pode conservar `started`/resultado desconhecido. Registros de chat/documento referenciam a pergunta, incluindo chamadas sem resposta. Sem prompts, respostas, chaves ou bodies de erro na telemetria.
- **Banco:** migração `003_ChatUsage.sql` aditiva, checksum/backup v3, custo opcional e índices por mensagem/finalidade, sessão/data, finalidade/data e data/ID. Preservados scripts anteriores, tokens existentes, contas, ownership e mensagens. Sem reset, inferência de dono ou preenchimento fictício de registros antigos.
- **Preço/custo:** catálogo local versionável `config/model-prices.json` inicialmente `[]`; `LLM_PRICES_FILE` documentada em `.env.example` e Compose. Campos explícitos, vigência por modelo/provider, validação de preços negativos/ausentes/duplicados; custo desconhecido não vira zero. Cache é parte da entrada. Inteiro em nanodólares por chamada permite soma exata; resposta API em USD decimal, com versão persistida. Valores sintéticos existem exclusivamente nos testes. Sem estimador heurístico ou tabela paralela de preços.
- **API/permissão:** resumo pessoal, grupos e chamadas paginadas em `/api/usage`; agregados administrativos em `/api/usage/admin/*` exigem `usage.admin`, adicionada ao catálogo existente. Consultas pessoais por JWT; qualquer filtro de sessão revalida dono/exclusão. Administradores não obtêm conteúdo ou chamadas detalhadas de terceiros. Paginação/agregação no SQLite; contagens de erro/resultado desconhecido e indicadores de total/custo parcial. Nenhum frontend novo neste incremento.
- **Arquivos principais:** `ILlmMetrics`, `DeepSeekService`, `ChatUsageService`, `UsageQueryService`, `UsageController`, DTOs de uso, `ModelPriceCatalog`, `PortalDbContext`/runner/SQL v3, `ChatTitleService`, controllers chat/OCR, `Program`, catálogo RBAC e configurações. Documentos de uso, conversas, contratos, modelo, segurança, roadmap, índice e README atualizados. Testes novos em `tests/Usage`, smoke HTTP ampliado e fixture de títulos ajustada para remover versões posteriores ao simular v1.
- **Validação executada:** `dotnet build ai-gateway/AiGateway.csproj --no-restore` passou (0 erros, 14 avisos preexistentes). `dotnet run --project tests/Usage --no-restore` passou (4 grupos: migração, preços, executor/consultas/permissões e parsing real do adapter com handler sem rede). `ChatSessions` passou (4 grupos), `ChatTitles` passou (6 grupos), `tests/access/SeedSmoke` passou. `python scripts/smoke_local.py` passou (4 grupos, incluindo HTTP de consumo, todos os agrupamentos, paginação, negação A/B/admin, cache/totais, custo desconhecido, falhas/replay e reinício). `npm run typecheck`/`npm run build` em `front` passaram; aviso conhecido de chunk acima de 500 kB.
- **Ajuste de teste:** o smoke anterior assumia uma única linha de título por conversa; passou a filtrar `Purpose='title'`, pois agora a mesma conversa corretamente também registra chat/documentos. As respostas simuladas passaram a incluir contadores conhecidos, sem rede externa.
- **Docker/aplicação local:** `docker build -t ia-portal-usage-api ./ai-gateway` concluiu. Imagem anterior preservada em `ia-portal-ai-gateway:before-chat-usage`. Após `docker compose config --quiet`, somente o gateway foi aplicado com `docker compose up -d --no-deps --no-build ai-gateway`. Swagger HTTP 200 com rotas de uso; login via frontend HTTP 200; resumo pessoal/admin sem autenticação HTTP 401. Um backup `pre-chat-v3-*` confirmado no volume privado sem abrir seu conteúdo; inicialização/migração concluídas. Qdrant/OCR/ML conservaram IDs e horários de início. Frontend permaneceu na imagem anterior porque não houve alteração em seu código.
- **Limites e revisão:** nenhuma chamada paga, leitura de dados/credenciais diárias ou consulta autenticada ao consumo real para validar implantação. OCR remoto, fatura/qualidade do modelo, carga/múltiplas réplicas, navegador visual e RAG ONNX/Qdrant não foram exercitados nesta etapa. Rotas experimentais SQL/Trino fora do chat persistente continuam sem contabilização; não tratar os totais como toda a fatura da conta externa. Tela/gráficos e configuração de provedores permanecem para etapas 4/5, assim como os bloqueios dos adapters inativos. Sem merge/push automático; revisar esta entrega antes da próxima etapa de provedores.

### 2026-11-01 — Etapa 4 — Provedores e seleção explícita

- **Autorização:** proprietário pediu continuidade e informou que fará merge somente ao final das etapas. Mantida `feature/chat-sessions-providers`, partindo de `f8d515b`, sem alterações pendentes iniciais. A autorização mais recente permite seguir para a etapa 5 após validar este incremento, sem nova pausa para merge.
- **Inspeção:** adapters antigos forçavam modelos; Ollama continha endpoint/credencial literal e logs brutos; OpenAI escrevia payloads em arquivos. Valores sensíveis não foram impressos/reutilizados. Aplicada OpenAI Docs para confirmar Chat Completions/usage/limite de título; Ollama conferido na referência oficial.
- **Implementação:** reutilizados três adapters com contrato comum/capacidade de tokens/títulos e router explícito. DeepSeek/OpenAI compartilham transporte compatível; Ollama conserva NDJSON e contadores próprios, sem retornar thinking. Modelos selecionados são respeitados. Erros HTTP categorizados/sanitizados e cliente de IA sem redirects. Nenhum fallback/retry pago automático. Removidos literal e logs de conteúdo dos adapters corrigidos; rotação da credencial antiga continua ação externa do responsável, não executada.
- **Dados/segurança:** SQL v4 aditivo/checksummed com backup antes de banco v3; configurações sem chaves, preferências por usuário, padrão global e classificação de cobrança. Endpoints limitados por allowlist do ambiente. Fallback de configuração por `.env` preservado; OpenAI/Ollama desabilitados inicialmente. Ollama local é explicitamente ausência de taxa de API, sem custo de infraestrutura calculado; remoto não é presumido gratuito.
- **API/UI:** provedores/modelos permitidos, preferências próprias, troca versionada de modelo por proprietário sem apagar histórico e sem geração ativa. Modelo solicitado permanece na conversa/pergunta; resposta/uso registram efetivo. Seletor no chat com confirmação, identificação por resposta, cabeçalho responsivo. Administração/configurações e consumo visual continuam para etapa 5.
- **Validação:** build .NET passou (13 avisos preexistentes após remover o adapter antigo que gerava um dos avisos); `Providers` passou (3 grupos), `ChatSessions` passou (4), `ChatTitles` passou (6), `Usage` passou (4). Smoke HTTP passou (5 grupos), incluindo OpenAI/Ollama simulados, preferência após restart, troca/ownership, uso/títulos e cobrança local. Typecheck/build frontend passaram; aviso de chunk Vite permanece. Fixtures de migrações antigas foram adaptadas para remover estruturas mais novas somente em bancos temporários.
- **Pendências:** testes visuais/deploy Docker serão executados sobre a integração final das etapas 4/5. Sem inferência de donos legados, chamadas remotas pagas ou testes de disponibilidade/qualidade de modelos reais. Sem merge/push automático.

### 2026-11-01 — Etapa 5 — Configurações, preferências e consumo visual

- **Autorização/branch:** continuação já autorizada pelo proprietário na mesma `feature/chat-sessions-providers`, após commit `232eac3` da etapa 4. Merge ao final será feito pelo proprietário; nenhum merge/push automático.
- **Backend:** novos endpoints administrativos para listar/salvar provedores, padrão global versionado e teste de metadados, todos com `ai.manage`. Essa permissão foi adicionada ao catálogo/seed idempotente sem concedê-la a grupos comuns. Teste GET models/tags com timeout de 10 segundos e corpo limitado a 256 KiB, sem inferência/download/model pull e sem tokens cobrados pelo app. Erros/status sanitizados, nenhuma chave retornada ou armazenada. Configuração inválida por mudança de allowlist recebe diagnóstico seguro para correção. Concorrência retorna 409. Snapshot de endpoint/classificação de cobrança evita redirecionar chamada aceita se a configuração for alterada durante sua execução; teste específico confirmou origem local na chamada aceita e nova origem/`unknown` na próxima seleção.
- **Frontend:** `/settings/ai` (preferência pessoal, administração condicional por permissão, padrão global, configuração individual e conectividade) e `/usage` (dados reais por período/finalidade/provider/modelo/conversa, detalhes paginados próprios e agregados administrativos). Navegação, rota inicial por capacidade e títulos integrados ao shell, incluindo papéis que só administram IA ou consumo. Botão no chat abre consumo da conversa. Valores nulos/parciais e infraestrutura local são explicitados; nenhum dado de demonstração no produto. Tabelas estreitas têm rolagem própria, foco visível e navegação por teclado.
- **Arquivos principais:** `AiSettingsController`, catálogo/RBAC/registro HTTP, proteção do snapshot nos adapters/router, páginas `AiSettingsPage`/`UsagePage`, rotas/guards/nav/header/chat. Roteiro `scripts/test_ai_ui.py` + `tests/Providers/ui.cjs`, doubles e smoke ampliados; fixture RAG distingue a chamada mínima de título. Documentos de configurações, dados, APIs, segurança, provedores, consumo, roadmap e README atualizados. Sem nova tabela/migração após v4; segredos seguem somente no ambiente.
- **Testes executados:** build do gateway passou com 0 erros/13 avisos preexistentes; `Providers` passou (3 grupos, incluindo edição concorrente de destino); `ChatSessions` (4), `ChatTitles` (6), `Usage` (4), `SeedSmoke` passaram. `python scripts/smoke_local.py` passou (6 grupos), inclusive autorização admin/A-B, CAS, origens proibidas, preferências/settings após restart, canário de chave ignorado/não persistido e teste de conexão sem inferência. Variáveis de provedores dos testes são sobrescritas com fixtures, sem herdar configuração que possa disparar chamadas reais.
- **Docker/browser:** imagens finais de API e frontend construídas com sucesso (`docker build -t ia-portal-ai-final-api ./ai-gateway`, `docker build -t ia-portal-ai-final-front ./front`); frontend executou typecheck e build no Docker, com aviso preexistente de chunk Vite acima de 500 kB. `python scripts/test_ai_ui.py --skip-build` passou nos cinco grupos sobre as imagens finais: configurações/defaults/preferência, troca preservando mensagens, consumo/paginação de 34 chamadas mais falha posterior, isolamento de usuário comum/admin, axe e overflow em 1440/390/320 px nos dois temas. Sem erros JavaScript. Capturas e resumo em `docs/design/screenshots/ai-settings/`; apenas dados sintéticos. Runner removeu seus containers/rede, helper e arquivo temporário de credenciais; criação é separada do start para permitir limpeza mesmo se o start falhar.
- **Problemas encontrados/corrigidos:** readiness por Swagger via frontend era fallback SPA (200 prematuro), podendo iniciar login antes da API; teste agora espera 401 de uma rota API protegida. Seletores MUI incluem o valor selecionado no nome acessível e foram ajustados. Axe detectou contraste de chips no claro e regiões de tabela sem foco no mobile; texto semântico/foco foram corrigidos e toda a suite repetida com sucesso. Dados de falha/diagnóstico do navegador não foram mantidos como evidência final.
- **RAG real:** `dotnet run --project tests/RagSmoke -- C:/source/IA-Portal`, com Qdrant descartável na porta 16333, passou (ownership A/B + ONNX embedding/indexação/busca/reranking/SSE reais; LLM simulado, sem Trino). A primeira execução revelou que o double substituía o prompt observado do chat pela chamada posterior de título; corrigida a fixture para responder JSON ao título e observar apenas o chat. A nova execução confirmou o contexto recuperado no prompt. Provedor/default/origens/chaves de teste foram fixados explicitamente para impedir herança de ambiente real.
- **Aplicação local:** imagens anteriores preservadas como `ia-portal-ai-gateway:before-ai-settings` e `ia-portal-portal-front:before-ai-settings`. `docker compose config --quiet` passou e somente gateway/frontend foram aplicados com `docker compose up -d --no-deps --no-build ai-gateway portal-front`. Startup/migração v4 concluídos; Swagger HTTP 200 com as novas rotas. Um backup `pre-chat-v4-*` confirmado no volume privado sem abrir conteúdo. `/settings/ai` e `/usage` responderam 200; APIs públicas de modelos/admin/consumo sem JWT responderam 401 pelo Nginx. Qdrant/OCR/ML mantiveram os mesmos IDs e horários de início. Nenhum volume/banco foi apagado/recriado, nem credencial/conversa diária lida nos testes autenticados. Nenhuma chamada paga foi feita para validar implantação.
- **Limites externos:** OpenAI/Ollama continuam dependentes de habilitação, modelos e credenciais reais quando aplicável; inferência/qualidade/disponibilidade/fatura remotas não foram testadas. Rotação/revogação da credencial literal antiga removida precisa ser confirmada pelo responsável no emissor. Sem limpeza do histórico Git, exportação/purga de dados, medição de infraestrutura, teste de carga/múltiplas réplicas ou auditoria completa de produção. Rotas experimentais fora do chat persistente não foram adicionadas aos totais de consumo. Documentação descreve esses limites, não promete segurança/contabilização universal.
# Correção de diagnóstico — Ollama Cloud (2026-11-01)

Chave nova confirmada no container por comparação em memória, sem imprimir credenciais. Duas chamadas sintéticas mínimas ao modelo configurado, nas rotas generate/chat, retornaram HTTP 402: restrição de créditos/plano, distinta do erro anterior de autenticação. Nenhum histórico do usuário foi enviado. A liberação da conta/modelo permanece uma pendência externa; não houve troca automática de provedor.

`ChatCompletionAdapter` classifica 402 como `payment_required`; `SmartAskController` e `ChatBotController` preservam a mensagem fixa e segura. O corpo do erro externo continua oculto. Testes de Providers passaram para os três adapters em chamadas comuns e streaming, incluindo ausência do corpo sensível sintético. `python scripts/smoke_local.py` passou nos seis grupos com provedores simulados e SQLite temporário. A execução dos testes recompilou o gateway com avisos preexistentes; frontend não alterado. Nenhuma mudança de esquema ou dados locais.

# Ajuste antes do merge — entrada do chat vazia (2026-11-01)

O hook `useChatLogic` deixa de restaurar a seleção local ou selecionar a primeira conversa. Entrar, retornar à rota ou recarregar mantém a lista do servidor disponível, sem abrir mensagens. “Nova conversa” apenas limpa a seleção, o campo e as mensagens; o primeiro envio de pergunta ou documento cria a sessão usando a preferência existente. A seleção explícita do histórico continua carregando e permitindo continuar a conversa. O cabeçalho vazio deixa de apresentar DeepSeek como se fosse o modelo padrão de todas as contas. Sem alterações no backend, contratos ou banco.

Validações executadas: `npm --prefix front run typecheck`, `npm --prefix front run build`, `docker build -t ia-portal-ai-final-front ./front` (inclui TypeScript/build) e `python scripts/test_ai_ui.py --skip-build`, todos aprovados. A suíte de navegador passou nos seis grupos com banco temporário e provedores simulados, incluindo criação no primeiro envio, conversa distinta, recarga vazia, recuperação por seleção e botão sem criação de sessão; nenhuma chamada paga. O build mantém o aviso existente sobre chunks grandes. Frontend aplicado localmente por tag da imagem e `docker compose up -d --no-deps --no-build portal-front`; backend e dados preservados. Sem merge automático.

# Indicador de processamento do chat (2026-11-01)

`ChatStatus` foi reaproveitado como cartão com indicador indeterminado acima do campo de envio, sempre visível durante `loading`. Usa tokens do tema, região de status acessível e interrompe animações sob preferência de movimento reduzido. O estado começa antes da criação da sessão, cobre perguntas e documentos e é limpo em falha de criação ou conclusão. Status do servidor informa a consulta à base; o primeiro trecho recebido muda o texto para recebimento da resposta. Sem percentuais, etapas fictícias ou mudanças no backend.

Executados e aprovados: `npm --prefix front run typecheck`; `docker build -t ia-portal-ai-final-front ./front` (TypeScript e build de produção); `python scripts/test_ai_ui.py --skip-build` (seis grupos, provedores simulados e banco temporário). O helper de envio pausa explicitamente a requisição antes do streaming e verifica aviso visível, texto não vazio, botão desabilitado e remoção do aviso após concluir. Demais verificações de histórico, modelos, autorização, acessibilidade e temas permaneceram aprovadas. Aviso de chunks grandes no build permanece. Frontend aplicado localmente sem recriar backend nem alterar banco; sem merge automático.



## Atlas - 06/11/2026: auditoria e Fase A

Branch feature/atlas-business-intelligence; main limpa e sincronizada por
`git fetch origin` e `git merge --ff-only origin/main`. Auditoria registrada em
AS_IS_AUDIT.md. Nenhuma instalacao, volume ou banco existente foi alterado.

Implementado snapshot incorporado de dez documentos v1 e seed transacional,
opt-in Demo:Atlas:SeedKnowledge, por IDs estaveis. Reutiliza KnowledgeBaseItem,
rotas, tela /kb, RBAC e recuperacao local existentes. Categoria em Section,
origem/versao em Subsection e corpo; nao requer migration. Sem overwrite de
chaves existentes, edicoes humanas ou estado inativo. Sem indexacao vetorial
implicita; reindex autenticado documentado. KB continua global, nao multi-tenant.

Validacoes efetivamente executadas:
- `dotnet run --project tests/AtlasKnowledge/AtlasKnowledge.csproj --no-restore`:
  PASS opt-in, dez itens/metadados, idempotencia, edicao/inativacao, conteudo
  legado e candidatos da busca textual. Falha de INSERT injetada via trigger:
  rollback integral e repeticao bem-sucedida. Testes em SQLite em memoria.
- `dotnet run --project tests/ChatSessions/ChatSessions.csproj`: PASS migracao
  legada, backup/restauracao, checksum, rollback DDL, isolamento e repositorio.
- `dotnet run --project tests/access/SeedSmoke/SeedSmoke.csproj`: PASS seed
  preexistente preserva usuarios, hashes, grupos, KB e permissoes.
- `dotnet build ai-gateway/AiGateway.csproj --no-restore`: PASS, zero erros.
  Compilacao inicial por dotnet run reportou avisos preexistentes; build incremental
  final reportou zero avisos. Nao foram refatorados trechos fora do escopo.
- Em front: `npm run typecheck` e `npm run build`: PASS; Vite alerta chunks >500 kB.
- `dotnet build ocr-service/OcrService.csproj` e
  `dotnet build ml-service/MlService.csproj`: PASS, zero erros/avisos.
- `python scripts/smoke_local.py`: PASS gateway HTTP real/SQLite temporario,
  login, KB, SSE, OCR simulado, ownership A/B, historico/restart, usage e providers.
  LLM/OCR doubles; nao valida inferencia paga ou extracao Tesseract real.
- `docker compose config --quiet`: PASS; nao iniciou containers ou alterou volumes.
- `git diff --check`: PASS.

`python scripts/smoke_local.py --atlas`: PASS suite HTTP completa com seed ativo,
dez documentos acessiveis por /api/kb/all e /api/kb/{id}, origem no conteudo e
contagens preservadas apos restart (sem duplicacao).
Sem smoke de navegador/temas, Qdrant/ONNX ou Trino nesta entrega. O frontend nao
foi alterado. Runbook: docs/demo/ATLAS_LOCAL.md.


## Atlas - 06/11/2026: Fase B offline

Gerador scripts/atlas_dataset.py, apenas biblioteca padrao Python, seed 42 e
referencia fixa 2026-11-01. Sete CSV e manifest com SHA-256 em diretorio novo;
recusa sobrescrita e nao acessa SQLite/rede. Testes usam diretorios temporarios.
500 clientes, 3 planos, 500 contratos, 5896 faturas, 5561 eventos de pagamento,
587 contestacoes e 2071 propostas de acao. Dinheiro em centavos inteiros;
parciais, atrasos em todas as cinco faixas, estornos, cancelamentos, sazonalidade,
concentracao e propostas de negociacao sinteticas. Nenhuma cobranca executada.

- `python -m unittest discover -s tests -p test_atlas_dataset.py -v`: PASS 4 testes
  (determinismo/seeds, cenarios/aging, corrupcao referencial/financeira rejeitada,
  arquivos byte a byte e recusa de sobrescrita).
- `python scripts/atlas_dataset.py --output artifacts/atlas-v1`: PASS; geracao
  local validada, arquivos ignorados pelo Git. Contagens acima sao observadas.
- Agregacao offline de aging conferida e registrada no runbook. Queries SELECT
  de verificacao em trino/examples/atlas-verification.sql; NAO executadas no Trino.

Sem migration. CSV e dicionario preparados para importacao; catalogo/carga e
conectividade Trino NAO concluidos. Nao ha novo menu, indicadores Atlas ou chat
hibrido. Nao houve deploy, push, merge ou alteracao de volumes/instalacao local.

Fases C/D/E pendentes: inexiste modelo de organizacao/membership e enforcement
SQL/rede suficiente. Solicitada decisao arquitetural sobre migracao de dados
existentes para organizacao padrao versus instalacao demo separada, conforme
excecao do usuario para decisoes arquiteturais significativas. Nao conceder
acesso global silenciosamente nem apresentar isolamento nao implementado.
