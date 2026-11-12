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


## 07/11/2026 - fundacao multi-organizacao aprovada

Migration 005_Organizations.sql, backup consistente/checksum/transacao no runner
existente. Dados atuais na organizacao padrao; seed Atlas separado e acesso
inicial so para administradores existentes. Roles locais, KB/sessoes/avaliacoes,
mensagens/usage por sessao e RAG filtrados. Lista de organizacoes autorizadas e
seletor no shell. Sem banco real/volumes alterados; sem deploy/push/merge.

Resultados efetivamente executados:
- dotnet run --project tests/Organizations/Organizations.csproj: PASS v4 real ->
  v5, preservacao de usuarios/hash/KB/roles/sessoes, membership, idempotencia,
  backup anterior a mudanca, restauracao+upgrade e rollback DDL/versao/dados.
- dotnet run --project tests/ChatSessions/ChatSessions.csproj --no-restore: PASS;
  fixture atualizada para representar schema anterior as organizacoes.
- dotnet run --project tests/access/SeedSmoke/SeedSmoke.csproj --no-restore: PASS;
  fixture atual declara memberships explicitamente.
- dotnet run --project tests/AtlasKnowledge/AtlasKnowledge.csproj --no-restore:
  PASS; seed/rollback/edicoes preservados na organizacao Atlas.
- dotnet run --project tests/Usage/Usage.csproj --no-restore: PASS.
- dotnet run --project tests/ChatTitles/ChatTitles.csproj --no-restore: PASS.
- dotnet run --project tests/Providers/Providers.csproj --no-restore: PASS.
- python scripts/smoke_local.py: PASS regressao HTTP/SQLite temporario.
- python scripts/smoke_local.py --atlas: PASS isolamento KB (ler/save/upsert),
  grupos, sessao do mesmo dono em outra organizacao, contexto RAG sem trechos
  alheios, membership recusada a usuario comum, reset de conta compartilhada
  negado e configuracoes globais protegidas. Execucao inicial encontrou
  tratamento 500 para documento alheio no upsert: corrigido para 404. Outra
  assercao dependia de provider deliberadamente desligado pela propria fixture;
  corrigido o setup antes da execucao final aprovada.
- dotnet build ai-gateway/AiGateway.csproj --no-restore: PASS, 13 avisos
  preexistentes; npm run typecheck --prefix front e npm run build em front: PASS;
  Vite mantem aviso de chunks >500kB.

Sem teste de navegador, modelos ONNX/Qdrant reais ou Trino neste incremento.
Rollback operacional e politica de migracao/provisionamento em ATLAS_LOCAL.md.


## 07/11/2026 - Fase C: conectores e executor fechado

SQL v6 aditiva, DataConnectors e ConnectorGrants por organizacao; RBAC
connectors.manage/data.query e concessao explicita por grupo. /data-connectors
reutiliza shell/MUI/temas. Perfis de rede/credenciais aprovados no servidor,
segredos apenas via ambiente, descoberta de tabelas/colunas e teste/status.
Reutilizados TrinoRestClient/QueryService/MetadataService; removido executor
inseguro de SQL livre e logs brutos. Planos fechados de aging/cashflow/top;
SQL arbitrario gerado por modelo nao e aceito. Limites e protecoes descritos
em docs/demo/DATA_CONNECTORS.md.

Executado:
- dotnet build ai-gateway/AiGateway.csproj --no-restore: PASS.
- dotnet run --project tests/Connectors/Connectors.csproj --no-restore: PASS
  migracao v5->v6, backup/restauracao/rollback, politicas/SQL adversarial/nextUri,
  byte/row limits, erros sanitizados e cancelamento.
- dotnet run --project tests/Organizations/Organizations.csproj --no-restore e
  dotnet run --project tests/ChatSessions/ChatSessions.csproj --no-restore: PASS
  (legado agora evolui ate v6).
- npm run typecheck em front: PASS.
- python scripts/smoke_local.py --atlas: PASS suite existente + conectores
  cadastro/perfil/grupo/tabela, teste, descoberta, analytics, CAS, desabilitacao
  e negacao cross-tenant. Trino/LLM/OCR eram doubles HTTP; o handler de socket
  aprovado foi exercitado por rede loopback real.

Build inicial teve aviso novo de parametro HttpClient nao utilizado; removido
com registro scoped do cliente. Build final voltou aos 13 avisos preexistentes. Trino real, browser e chat hibrido ainda
nao validados. Imagem trinodb/trino:484 obtida para validacao local seguinte;
nenhuma instancia existente ou banco real foi alterado.


## 07/11/2026 - Fase D: chat hibrido no fluxo existente

BusinessChatContextService envolve os contextos existentes. Classificador
pt-BR deterministico distingue documentos/analytics/hibrido/esclarecimento;
fonte/data explicitas e quatro planos financeiros fechados, incluindo trend
mensal. SQL nao vem do modelo. Conector/grants sao revalidados no servidor.
O modelo recebe somente agregacoes autorizadas e politicas recuperadas, com
citacoes/versao; tabela, periodo, filtros, SQL e chart payload sao anexados pelo
servidor. SourceId/referenceDate entram no fingerprint de replay.

AllowExternalLlm e opt-in para envio de resultados nao-demo. Sem permissao de
egress ou dados ausentes/vazios/indisponiveis, resposta local sem inferencia,
consumo ficticio ou chamada paga de titulo. Fallback de titulo preserva edicao
manual. Resultados SQL persistidos continuam no historico de exibicao mas nao
voltam automaticamente ao prompt de IA/OCR/rotas legadas: nova analise precisa
reautorizar e consultar, inclusive apos revogacao de fonte/egress.

Validacoes finais executadas:
- dotnet build ai-gateway/AiGateway.csproj --no-restore: PASS, 13 avisos
  preexistentes, nenhum erro.
- dotnet run --project tests/BusinessChat/BusinessChat.csproj --no-restore: PASS
  classificacao, fonte ausente, gate de egress enterprise, grounding/proveniencia,
  fonte indisponivel/nao autorizada sem inventar metricas.
- dotnet run --project tests/Connectors/Connectors.csproj --no-restore -- --real-trino:
  PASS v5->v6 backup/restore/rollback, SQL adversarial/rede/limites e quatro planos
  no Trino 484 real. Aging confere cinco totais exatos; cashflow/trend conferem
  valores em centavos contra agregacoes offline independentes em expected-v1.json.
  Descoberta real autorizada; reader nao escreve nem acessa system ou manifest.
- python scripts/smoke_local.py --atlas: PASS suite completa com gateway/SQLite
  reais e doubles de Trino/LLM/OCR; hybrid SSE, citacoes, tabela/chart/proveniencia
  persistidos, fonte/ref no replay, historico de exibicao preservado e exclusao
  dos resultados numericos do contexto subsequente. Ausencia/falha/vazio nao
  chamam LLM; fallback local sem usage ficticio (requests=0).
- python scripts/smoke_local.py --atlas --real-trino: PASS a mesma integracao com
  Trino real local e valores reais do dataset sintetico; apenas LLM/OCR doubles.
  Casos injetados de falha/vazio sao cobertos na execucao com double.
- Regressoes .NET reexecutadas com dotnet run --project <projeto> --no-restore:
  tests/AtlasKnowledge, Organizations, ChatSessions, ChatTitles, Usage, Providers
  e access/SeedSmoke: todas PASS. Migrations antigas, isolamento, titulos,
  provedores e contabilizacao preservados.
- npm run typecheck --prefix front: PASS.

Sem modelos pagos, embeddings/reranker reais ou fonte empresarial/publica real.
Classifier/plans sao limitados e documentados em docs/demo/ATLAS_CHAT.md; datas
na linguagem natural nao substituem silenciosamente a referencia do controle.


## 07/11/2026 - Fase E: visualizacoes e shell preservado

Recharts existente recebe atlas-chart com schema/tipos/valores finitos/limites
validados, tabela alternativa e valores em BRL. Sem animacao obrigatoria dos
valores. Mermaid antigo com securityLevel loose e diagrama substituto foi
removido: parsing strict, diretivas/callbacks rejeitados e SVG isolado em iframe
sem scripts/same-origin/rede. Falha mostra codigo original. Markdown sem HTML
raw e sem carregamento automatico de imagens externas.

Fonte/data e perguntas sugeridas reutilizam chat, hook e SSE existentes. Sugestoes
sao expansiveis para nao ocupar a conversa. Mudanca de tenant entre abas tambem
atualiza identidade e remonta o workspace, limpando estado de fontes/historico;
rotulos acessiveis em pt-BR corrigidos. Design system claro/escuro preservado.

Executado:
- node tests/ui/atlas-security.cjs: PASS tipos/finite/limites/prototype keys e
  rejeicao de Mermaid invalido/diretivas/callback/scripts.
- node tests/ui/atlas.cjs com IA_PLAYWRIGHT_MODULE, IA_CHROME_PATH e IA_AXE_MODULE
  apontando para as ferramentas existentes em TEMP: PASS Chrome headless,
  cabecalho de organizacao, seletor de fonte/referencia, sugestao no input,
  grafico com barra numerica visivel, iframe sandbox+CSP e fallback de diagramas,
  tela de conectores/descoberta/edicao em ambos os temas, layouts 390/320px sem
  overflow e troca de tenant entre abas limpando workspace. APIs deste teste
  sao fixtures de rota, nao credenciais/dados da instalacao.
- axe-core: PASS WCAG A/AA no main de chat mobile com controles de fonte em ambos
  os temas. Nao e certificacao de acessibilidade de toda a aplicacao.
- Capturas inspecionadas em artifacts/atlas-ui/chat-dark.png e demais temas/telas;
  checks.json registra escopo. Esses artefatos locais sao ignorados pelo Git.
- npm run typecheck --prefix front e npm run build --prefix front: PASS;
  Vite mantem aviso de chunks >500kB. Nenhuma dependencia nova.

Durante o teste foram corrigidos rotulos de organizacao com codificacao
inadequada. Assercoes de drawer fechado/tooltip e initScript em frame sandbox
foram ajustadas na fixture; execucao final sem pageerrors ou rotas inesperadas.


### Fechamento dos caminhos legados - 07/11/2026

/api/sql/ask e ask-rag agora rejeitam SQL livre antes de chamar qualquer modelo,
conservando 404 de conversa alheia. /api/chatBot/ask permanece documental;
perguntas classificadas como analiticas recebem esclarecimento local indicando
fonte/data, sem inferencia nem indicadores inventados. Isso evita que uma rota
antiga contorne o fluxo de grounding ou gere chamadas pagas sem plano autorizado.

Executado dotnet build ai-gateway/AiGateway.csproj --no-restore: PASS com 11
avisos preexistentes (dois avisos anteriores desapareceram com a remocao do
executor legado). python scripts/smoke_local.py --atlas: PASS, incluindo calls
counters sem LLM para JSON analitico/rotas SQL e todas as regressoes anteriores.


## 07/11/2026 - integracao local reproduzivel e fechamento

Adicionados trino/atlas (imagem 484 por digest, Memory, ACLs reader/loader),
docker-compose.atlas.yml opt-in e perfil com IP fixado. O carregador Python so
alcanca 127.0.0.1:18081, sem proxy, usa dados gerados localmente e recusa schema
parcial/divergente. Reexecucao com mesmo manifest/contagens nao escreve. Nenhum
DROP/TRUNCATE, credencial externa ou mudanca no SQLite da instalacao.

Efetivamente executado:
- docker pull trinodb/trino:484: PASS; digest
  sha256:2704eb53935af5e582d63d6033e9bf99d20c325e9b0e30b1016ef59a4773979f.
- docker compose -f trino/atlas/compose.yml -p ia-atlas-validation up -d: PASS;
  servidor real isolado, saudavel, publicado so em loopback.
- python scripts/atlas_trino_load.py: PASS carga de sete tabelas, 500 clientes,
  5896 faturas e 5561 pagamentos/estornos; segunda execucao PASS sem escrita.
- python scripts/atlas_trino_load.py --verify: PASS quatro SELECTs do repositorio,
  contagens, FKs, consistencia de liquido e aging exato. Primeira tentativa do
  helper nao definia schema para SQL nao qualificado: corrigido o contexto do
  helper de verificacao, sem alterar tabelas/dados.
- dotnet run --project tests/Connectors/Connectors.csproj --no-restore -- --real-trino:
  PASS quatro planos, conciliacao independente de centavos e protecoes reais.
- docker build -t ia-portal-atlas-api:validation ./ai-gateway (tambem repetido com
  -q no fechamento): PASS; publish agora aponta explicitamente para o csproj,
  removendo o aviso de output da solution. Dockerignore exclui backups/SQLite e
  config local. Exclusoes de material local aplicadas ao contexto de imagem.
- docker build -t ia-portal-atlas-front:validation ./front: PASS typecheck/build,
  imagem Nginx, Vite com aviso de chunks grandes.
- python scripts/smoke_atlas_docker.py: PASS com imagens finais; gateway e frontend
  descartaveis, SQLite tmpfs, bootstrap/login/org/KB seed, metadados autorizados
  e aging exato no Trino real via rede de containers/IP pinning. Nginx serviu UI.
  Nao chama LLM, nao monta portal_data e remove seus containers no finally.
- docker compose -f docker-compose.yml -f docker-compose.atlas.yml config --quiet:
  PASS. O override completo nao foi iniciado nos volumes existentes.
- git diff --check: PASS.

Cleanup executado: docker compose -f trino/atlas/compose.yml -p
ia-atlas-validation down, removendo apenas servidor/rede de teste; helpers Vite
proprios encerrados quando presentes. Nenhum container ia-atlas ficou ativo.
Imagens de validacao e artefatos locais sinteticos podem ser reutilizados.

Relatorio final, inventario de arquivos, migrations, comandos, limitacoes e
instrucoes: docs/demo/ATLAS_DELIVERY_REPORT.md e ATLAS_LOCAL.md. Sem push, merge,
deploy publico/producao ou alteracao de dados da instalacao. Modelos pagos,
RAG vetorial real e fontes enterprise/publicas reais continuam nao validados.


## 07/11/2026 - reset local autorizado, sem backup, e Atlas ativado

Pedido posterior explicito do proprietario: excluir todos os dados locais,
deixar Atlas e aplicar no ambiente local, sem necessidade de backup. Essa
instrucao autoriza a excecao pontual as regras anteriores de preservar os dados.
Nenhum bypass de backup foi introduzido no runner de migrations.

Precondicoes: branch feature/atlas-business-intelligence limpa; inspecao de
Compose, mounts, labels e containers comprovou que os unicos volumes a apagar
eram ia-portal_portal_data (SQLite/backups) e ia-portal_qdrant_data (vetores).
Owners/usuarios/contas antigas nao foram copiados. Credenciais/env/configuracao,
codigo e modelos instalados foram preservados. Nao havia data/SQLite nativo no
caminho padrao inspecionado. Subnet Atlas verificada sem sobreposicao.

Ajustes opt-in: OnlyKnowledge evita organization_base generico quando o seed
Atlas esta habilitado; PreferOrganization usa a mesma selecao de membership
ativa no login e nos requests sem header. Org tecnica padrao permanece disponivel
para configuracao global de IA, sem conhecimento ou dados de negocio. Sem flags,
o modo generico conserva a selecao anterior. Override Atlas fixa RAG textual,
pois o ambiente anterior tinha RAG avancado ativo com Qdrant parado.

Executado:
- dotnet run --project tests/AtlasKnowledge/AtlasKnowledge.csproj --no-restore:
  PASS casos existentes e novos testes de dez documentos exclusivos, repeticao,
  preferencia Atlas e fallback sem ultrapassar membership desativada.
- docker compose -p ia-portal -f docker-compose.yml -f docker-compose.atlas.yml
  config --quiet e build ai-gateway portal-front: PASS, imagens atuais publicadas
  somente no Docker local. Backend manteve 11 avisos existentes.
- docker compose -p ia-portal --profile rag -f docker-compose.yml -f
  docker-compose.atlas.yml down: removeu somente os servicos/rede do projeto.
- docker volume rm ia-portal_portal_data ia-portal_qdrant_data: removidos os
  dados anteriores, incluindo contas, conversas, usage, KB e backups/vetores.
  Sem backup, conforme instrucao expressa. Nao usado prune global.
- docker compose -p ia-portal -f docker-compose.yml -f docker-compose.atlas.yml
  up -d --no-build: PASS, portal/frontend/OCR/ML/Trino ativos em loopback.
- python scripts/atlas_trino_load.py e verificacao: PASS sete tabelas carregadas,
  500 clientes/5896 faturas/5561 pagamentos-estornos e quatro SELECTs conferidos.
- Bootstrap/login real usando valores do .env apenas em memoria, sem exibi-los:
  PASS Atlas selecionada por padrao; dez docs e conector atlas_local registrado,
  sete tabelas selecionadas e grant admin. Teste/metadata/aging PASS via API real.
  Plataforma tecnica padrao sem KB, conexoes ou conversas antigas.
- Chrome headless no frontend servido por Nginx: login real, fonte Atlas,
  referencia 2026-11-01 e pergunta hibrida geraram resposta DeepSeek real,
  modelo efetivo deepseek-flash. Mensagens persistidas (2), citacao da politica,
  tabela e grafico conferidos; usage real de resposta/titulo: 2 calls, 3610 tokens.
  Uma espera inicial pelo botao Enviar nao se aplicava ao input vazio; verificacao
  subsequente confirmou resposta concluida, historico e renderizacao sem nova
  chamada paga. Captura apenas do main em artifacts/atlas-local/chat-live.png.
- .env local atualizado apenas para COMPOSE_FILE/COMPOSE_PATH_SEPARATOR; comandos
  comuns docker compose agora mantem o override Atlas. Segredos preservados.
- docker compose restart ai-gateway: PASS; API apos restart confirmou mesmos dez
  documentos, uma conexao e uma conversa Atlas, sem duplicacao; login default Atlas.
- SQLite lido por helper Docker com volume read-only e rede none: schema_versions
  [1,2,3,4,5,6], users=1, KB apenas org Atlas (2,10), connectors=1, sessions=1,
  usage=2; nenhum arquivo de backup. Helper efemero removido automaticamente.

Frontend ativo: http://127.0.0.1:9080. Usuario deve atualizar/reautenticar com
administrador do .env. Instalacao deixada em execucao para validacao; nao houve
push, merge, publicacao na internet ou deploy de producao. Trino Memory perde
os dados em reinicio e requer recarga do gerador local; detalhes em ATLAS_LOCAL.md.

## 2026-11-07 — Verificação dos gráficos no chat e cache do frontend

Relato: JSON de atlas-chart visível como código na captura. Repositório limpo
na branch feature/atlas-business-intelligence antes da alteração. A resposta
persistida contém fences atlas-chart/SQL válidos. Login local em navegador novo,
com a conversa existente, confirmou um gráfico numérico renderizado e apenas
o plano SQL como pre. Não foi reproduzida a falha da captura; frontend antigo
na aba/cache é hipótese, não causa comprovada. Não foi necessária chamada LLM.

Alterações: front/nginx.conf exige revalidação do HTML da SPA (inclusive /chat)
e mantém cache immutable para bundles com hash, retornando 404 para bundles
inexistentes. Isso favorece atualização após recarregar; abas abertas ainda
precisam ser recarregadas. tests/ui/atlas.cjs agora verifica também SVG Mermaid
com altura positiva dentro do iframe. Novo tests/ui/frontend-cache.cjs cobre
os cabeçalhos HTTP e ausência de fallback HTML para bundle inexistente.

Validação efetiva:
- node tests/ui/atlas-security.cjs: PASS.
- docker compose build portal-front: PASS (yarn typecheck e yarn build;
  aviso já existente de bundle maior que 500 kB).
- docker compose up -d --no-deps portal-front: PASS; somente frontend recriado.
- docker compose exec -T portal-front nginx -t: PASS.
- node tests/ui/frontend-cache.cjs: PASS contra localhost:9080.
- node tests/ui/atlas.cjs com IA_UI_BASE_URL=http://127.0.0.1:9080,
  Playwright/Chrome locais: PASS; APIs sintéticas interceptadas para os testes
  visuais, gráfico numérico e SVG Mermaid, sandbox/CSP, fallback inválido,
  temas claro/escuro, larguras 390/320 e troca de organização.
- Conversa real existente verificada separadamente no navegador: gráfico visível.
  Evidência local ignorada: artifacts/atlas-local/chart-before.png.

Nenhuma migration, alteração no backend, reset de dados ou reinício do Trino.
Backend .NET não recompilado pois não houve alteração nele. Para validar na aba
antiga, usar Ctrl+F5 e reabrir a conversa; não é necessário gerar nova resposta.

## 2026-11-07 — Chat com cabeçalho recolhido e entrada compacta

Pedido: ampliar espaço disponível para mensagens e gráficos. Branch existente
feature/atlas-business-intelligence, inicialmente limpa. Inspecionados ChatPage,
ChatInput, ConversationModelPicker e BusinessSourcePicker; reutilizados React,
MUI e controles existentes, sem alterações de contratos ou autorização.

- ChatPage.tsx: título e expander nativo no topo; modelo, consumo e fonte dentro
  de details recolhido por padrão, reiniciado ao trocar de conversa. Conteúdo
  expandido limitado a 40dvh com rolagem para preservar espaço em telas pequenas.
- ChatInput.tsx: uma linha inicial, crescimento até cinco linhas, anexo e envio
  ao lado do texto; preservados Enter, Shift+Enter e proteção de composição IME.
- tests/ui/atlas.cjs: regressão para controles inicialmente ocultos, abertura
  por teclado, fechamento, altura de uma linha e crescimento com Shift+Enter.

Validação:
- npm run typecheck --prefix front: PASS.
- docker compose build portal-front: PASS (typecheck/build; aviso existente de
  chunks acima de 500 kB).
- docker compose up -d --no-deps portal-front: PASS, versão local atualizada.
- node tests/ui/atlas.cjs contra http://127.0.0.1:9080, Playwright/Chrome e
  axe-core locais: PASS; fixtures de API sintéticas, temas claro/escuro,
  teclado, gráfico numérico/Mermaid, sandbox, layout 390/320px e organização.
- node tests/ui/frontend-cache.cjs: PASS.
- Capturas locais artifacts/atlas-ui/chat-dark.png e chat-light.png atualizadas;
  captura escura inspecionada visualmente, com cabeçalho e composer compactos.

Sem migrations, mudanças no backend ou operações sobre dados. Testes .NET não
reexecutados pois o incremento altera somente frontend. Preview: localhost:9080;
recarregar a aba para receber a versão nova. Não houve push/merge/publicação.

## 2026-11-07 — OCI: diagnóstico e provisionamento somente dry-run

Pedido autoriza consultas remotas e arquivos locais; exige confirmação antes
de qualquer mudança de rede, criação de VM/recurso ou deploy. Nenhuma ação de
escrita remota foi executada. Branch feature/oci-always-free, criada de main
limpa. AGENTS, docs/README e ROADMAP lidos antes da implementação.

Entregues:
- infra/oracle/provision-always-free.ps1: PowerShell 5.1/OCI CLI, DryRun padrão,
  perfil/config local, Home Region fixa, A1 2 OCPUs/12 GB, imagem oficial Ubuntu
  24.04 aarch64, boot default com 10 VPU/GB, IPv4 público, chave apenas .pub.
  Sem fallback de região/shape, recursos adicionais ou edição de .env.
- Preflight faz inventário da tenancy/subcompartments, quotas/disponibilidade,
  uso A1 mensal reportado, armazenamento e política completa de ingress da
  subnet. Falha de leitura/IAM bloqueia; não presume recurso ausente por erro.
- Retry exclusivamente para capacity OCI explícita, intervalo 1.800 segundos,
  sem retries automáticos da CLI; lock local, journal antes do POST e reconciliação
  somente leitura em resposta ambígua. Erros definitivos encerram imediatamente.
- infra/oracle/provision-always-free.tests.ps1: testes offline sem Pester/OCI,
  com fixtures sintéticas do preflight e da máquina de estados.
- infra/oracle/README.md: operação, proposta de rede, recuperação, custos,
  limites da garantia de gratuidade e diagnóstico Compose/ARM64.
- docs/architecture/AS_IS_AUDIT.md: achados reais desta inspeção.

Consultas efetivamente realizadas (OCI CLI 3.96.0): region-subscription,
availability-domain, compartments subtree/ANY, instance list, shapes, imagens e
image-shape compatibility; limits value/resource-availability compute/storage,
quotas, boot/block volumes, duas VCNs/subnets/route tables, Internet Gateway e
security lists; NSGs da VCN 2; usage-api mês atual. Comandos de list --all vazios
retornam stdout vazio e exit 0; tratado explicitamente. Primeira consulta de uso
rejeitou precisão de hora; corrigida para limites de dia UTC e retornou items [].
Nenhuma inferência/modelo externo foi chamado.

Resultados: autenticação PASS; nenhuma VM/volume encontrados. A1 elegível como
LIMITED_FREE; teto local imposto de 2 OCPUs/12 GB, apesar dos maiores limites de
Trial. 200 GB free storage livres. Imagem Canonical Ubuntu 24.04 aarch64 publicada
em 30/10/2026, tamanho 47.694 MiB, compatível. Chave pública SSH localizada em
Downloads e validada com ssh-keygen -l (RSA 2048); nenhuma chave privada lida ou
copiada. IP administrativo detectado por HTTPS, guardado apenas no estado local.

VCN 1: route-rules vazia. VCN 2: route default/IGW/public IPv4 PASS; SSH mundial,
80/443 ausentes. Proposta local de ingress/ETag em artifacts/oci-audit (ignorado
pelo Git), sem update OCI. Alteração proposta preserva ICMP e egress, restringe
22 ao /32 administrativo e abre somente web 80/443.

Verificações executadas:
- powershell -NoProfile -ExecutionPolicy Bypass -File
  infra/oracle/provision-always-free.tests.ps1: PASS, 74 assertions offline;
  sem API OCI, sem VM e sem espera real. Inclui portas internas bloqueadas,
  quota/histórico A1/VM parada, dry-run, aprovação, lock, journal, erro fatal,
  timeout, reconciliação e capacity -> sucesso simulado.
- powershell -NoProfile -ExecutionPolicy Bypass -File
  infra/oracle/provision-always-free.ps1 -Mode DryRun: execução real somente
  leitura contra OCI, repetida; resultado Blocked com quatro motivos esperados
  da security list. Exit 2 confirmado por subprocess Python, sem launch.
- docker buildx imagetools inspect --raw: PASS manifests linux/arm64 de
  dotnet aspnet/sdk 8.0, node22-alpine, nginx1.25-alpine, qdrant1.20.0 e digest
  Trino484 fixado. Não foi feito build/runtime ARM64; NuGet MKL só x64 é ressalva.
- git diff --check: PASS.

Não executados: provisionamento real, alteração de rede, scheduler, deploy,
login SSH remoto, build da aplicação (fontes de backend/frontend inalteradas),
inferência ARM64 ou benchmark. Sem migrations, reset de banco, upgrade da conta,
push ou merge. Logs/estado em LOCALAPPDATA, sem credenciais; inventário bruto
local ignorado pode conter tags pessoais e não deve ser publicado. A próxima
etapa depende da confirmação explícita para a mudança concreta de rede e/ou
início do provisionamento. Gratuidade continua condicionada aos termos OCI e
uso agregado; limite/estimativa de preço não equivale a garantia de fatura zero.

## 2026-11-07 — Rede OCI autorizada e loop real iniciado

O proprietário respondeu "sim autorizo" ao resumo que propôs restringir SSH ao
/32 administrativo, liberar 80/443 na VCN 2 e, após dry-run aprovado, iniciar o
provisionamento A1 2 OCPUs/12 GB/boot padrão. Autorização não inclui deploy.
Branch feature/oci-always-free inicialmente limpa; AGENTS relido.

Executado sequencialmente:
- IP público de saída relido por HTTPS e confirmado igual ao /32 aprovado.
- Subnet/route table/IGW da VCN 2 relidos: públicos/rota default/IGW habilitado
  da mesma VCN. Security list relida e ingress/egress comparados ao diagnóstico
  anterior antes de qualquer update. Nenhuma mudança concorrente encontrada.
- OCI network security-list update usando payload local previamente revisado e
  --if-match com ETag recém-lido: PASS. Somente ingress alterado. Nova leitura
  confirmou TCP 22 restrito, 80/443 públicos stateful, ICMP e egress preservados.
  VCN 1, rotas, IGWs, conta e outros recursos não foram alterados.
- powershell -NoProfile -ExecutionPolicy Bypass -File
  infra/oracle/provision-always-free.ps1 -Mode DryRun -AdminCidr <aprovado>:
  PASS, Ready/blockers=0. Inventário/limites/imagem/armazenamento/rede aprovados.
- Runner PowerShell iniciado com Start-Process -WindowStyle Hidden, imagem
  fixada ao ID aprovado, -Mode Provision -ProvisionApproved e o mesmo /32.
  Estado/PID/início e stdout/stderr somente no LOCALAPPDATA, fora do Git.
- Preflight do modo real: Ready. Primeiro launch em 07/11/2026 às 18:41:24
  America/Sao_Paulo; resposta capacity explícita às 18:41:27. Journal=Capacity;
  processo vivo, aguardando intervalo de 1.800 segundos; stderr vazio.
- Verificação somente leitura após o launch: zero instâncias não terminadas,
  zero boot volumes e zero block volumes. Identidade do processo (PID/início)
  conferida. Nenhuma VM criada nesta primeira tentativa.

README atualizado com estado histórico/operacional, acompanhamento e interrupção
segura do runner oculto. Ele não é tarefa agendada nem serviço persistente após
reinício; requer PC ligado e sem suspensão. Script aguarda capacity e se encerra
em sucesso ou em erro definitivo, conforme as travas implementadas. Não iniciar
outro processo em paralelo. Monitorar logs/journal para o resultado futuro.

Nenhuma chave privada lida/copiadada, nenhum .env/banco/app alterado, sem deploy,
SSH remoto ou inferência. Estado bruto/revisão de rede ficam em artifacts/oci-audit
(ignorado), sem dados pessoais no commit. Alterações deste incremento no repo
são apenas documentação operacional; não houve novo build de backend/frontend.

## 2026-11-07 — Shared Community/Cloud core: bounded workspace increment

Audit completed before application edits in
[CURRENT_STATE.md](../architecture/CURRENT_STATE.md). New branch
feature/community-organizations from clean 01d33bc. The previous local OCI retry
runner was stopped by verified PID/start time to honor the new no-OCI-change
instruction; no OCI API called. No production deployment, license choice, paid
inference/dependency, .env edit or existing database reset/migration performed.

Existing tenant middleware, membership validation, query/write guards, KB/vector
revalidation, conversations and Trino grants were reused. Delivered organization
creation, explicit Owner/Admin/Member/Viewer policies (legacy custom groups
preserved), owner-only membership changes with CAS/final-owner protection,
organization model defaults/preferences, a workspace screen and the explicit
header selector fix. Credentials/endpoints remain installation-owned, not a
plaintext tenant secret editor. Added one insert-only, versioned support template
using the existing KB/index service and existing chat; wizard UX specified with
only the first reusable form implemented. English community/architecture/security/
  self-hosting/roadmap documentation added; Apache/AGPL decision left pending.
Two historical employer identifiers generalized; vector question/title logging
removed from the audited retrieval path.

Migration 007: additive membership policy/version and organization AI tables;
legacy model preference copied only to organization 1. Existing admins keep Admin,
lowest active admin becomes exactly one initial Owner per organization, existing
custom grants stay unchanged. Old migration text/checksums untouched. Backup,
restore/re-upgrade, idempotency and injected late DDL rollback tested on real v6
fixtures (including multiple administrators), never on the existing portal DB.
During failure testing, SQLite 8 ExecuteNonQuery suppressed later batch errors;
runner now explicitly consumes each reader result before recording success.
Upstream reference: https://github.com/dotnet/efcore/issues/32740.

Actual verification:
- Backend gateway build PASS (11 preexisting warnings); OCR/ML builds PASS (0).
- .NET suites Workspace, Organizations, AtlasKnowledge, Connectors, Providers,
  ChatSessions, ChatTitles, Usage, BusinessChat and access/SeedSmoke PASS.
- Workspace additionally sends a scoped vector filter and injects foreign,
  missing-scope, mismatched-row and inactive candidates; rejects them before
  reranking and loads only authorized relational context: PASS.
- python scripts/smoke_organizations.py PASS: real HTTP/JWT/temporary SQLite,
  tenant creation/switch/forged IDs; defaults/preferences; Member/Admin/Viewer
  denials and existing-JWT revocation; final Owner/stale versions; cross-tenant
  KB/history/usage/connectors; template idempotency, human edits and actual
  retrieval into the existing streaming prompt. LLM HTTP double, no paid calls.
- python scripts/smoke_local.py --atlas PASS: complete existing API regression,
  temporary SQLite and LLM/OCR/Trino HTTP doubles, restart persistence.
- RagSmoke first required its explicit disposable endpoint. Then PASS with new
  empty Qdrant container, real local ONNX embeddings/reranking, fake LLM, SQLite
  temp and SSE. Container removed in finally. A wrapper console encoding error
  happened after test exit 0; saved test log was read safely to confirm success.
  This validates the Windows host path, not ARM64. No local user index touched.
- npm run typecheck/build --prefix front PASS (existing large-chunk warning).
- Playwright organizations.cjs PASS: actual create/header switch/reload/default/
  template UI with API fixtures, themes, 390/320px and axe main checks. Atlas UI
  regression and atlas-security.cjs PASS. Light/dark captures inspected locally.
- Python Atlas dataset tests PASS (4). git diff --check PASS.

Initial failures were fixed and rerun: schema max version, old synthetic fixtures,
late batch error suppression, deterministic owner update, expected Owner label,
test HTTP/SSE route assumptions, Python SQLite connection cleanup on Windows,
required form labels and stale hover-tooltip timing during browser resize.
No authorization bypass or production data reset was used.

Full file inventory, command outcomes, limits and next steps:
[COMMUNITY_WORKSPACE_REPORT.md](COMMUNITY_WORKSPACE_REPORT.md).
Not delivered: invitations, tenant credential vault/model allowlists, multiple KB
libraries/history, agents, official WhatsApp/other channels, billing/quotas, CI
workflow or production readiness. Recommended next: consenting invitations and
ownership UX, then tenant credential/model policy. Existing running containers
were not upgraded; SELF_HOSTING.md describes preserving the current Compose
settings and applying the reviewed images/migration with backup locally.

## 2026-11-08 — Consenting invitations and ownership transfer

Continued from the delivered Community workspace in a dedicated branch after
fetch showed origin/main still at 90b5b86. Initial tree was clean; previous work
was retained. Reused JWT/membership policies and tenant scope rather than creating
a second identity system. Added Owner-issued existing-account Member/Viewer links
with explicit consent, 24-hour single use, revocation, SHA-256-only persistence,
issuer-version invalidation and no global account lookup. Links are bearer
capabilities, not verified-address invitations; the UI states this clearly.

Ownership transfer atomically promotes the active target and demotes the actor
to Admin using both versions. Confirmation warns about installation administration
for organization 1. Generic role changes no longer promote a new Owner. Migration
008 is additive; older checksums and live data/environment are unchanged.

PASS: gateway build; .NET Invitations, Workspace, Organizations, AtlasKnowledge,
Connectors, Providers, ChatSessions, ChatTitles, Usage, BusinessChat and SeedSmoke;
real HTTP organization smoke; complete local Atlas HTTP smoke; frontend typecheck
and production build; invitation/organization/Atlas/security browser tests including
themes, mobile, axe, numeric charts and Mermaid sandbox/fallback. Existing compiler
and frontend chunk-size warnings remain. Temporary v7 upgrade/repeat/backup restore
and injected late-DDL rollback passed. External LLM/OCR/Trino were doubles; browser
APIs were fixtures. No new ARM64/native OCR/vector or live deployment validation.

Full file inventory, exact commands, migration/rollback and local operation:
[INVITATIONS_REPORT.md](INVITATIONS_REPORT.md). Next recommended increment is tenant
model allowlists and encrypted/vault-backed credentials. Email-bound onboarding,
recovery/MFA, audit retention, quotas/CI and production hardening remain pending.
Existing containers/SQLite were not reset or upgraded; .env was not modified.
The separately authorized OCI retry runner was left alone; no remote changes.

## 2026-11-08 — IA-Portal Service channel MVP

Branch feature/service-whatsapp from fetched origin/main a9ec52c, clean baseline.
Read the attached request, AGENTS and delivery docs; inspected existing identity,
tenant filters, conversations, RAG, providers, React and Compose before edits.
Official Meta research encountered429/login gates, explicitly documented with
primary references. No eligibility, free allowance or live integration is claimed.

Implemented scoped multi-number channels, connection-local contacts/history,
durable deduplicated inbox/outbox, constrained extractive agent using the shared
chat textual retriever, source hashes, opt-in simulator, handoff, 24-hour window,
per-number/organization caps, content redaction retention and bounded transient
retry. Official-envelope signed text reception is operator-bound to WABA/phone/
organization; unsupported app echoes/history cannot trigger replies. Cloud
auto-send is rejected until a later reviewed real sender/onboarding increment.

Migration009 adds five tables/indexes, preserving prior schema checksums/data.
Temporary v8 upgrade/repeat/backup/restore and late-DDL rollback passed. PASS:
gateway build; Channels plus existing Workspace/Invitations/Organizations/
AtlasKnowledge/Connectors/Providers/ChatSessions/ChatTitles/Usage/BusinessChat/
SeedSmoke; actual channel/JWT/webhook/worker smoke; organization and full local
Atlas smoke; frontend typecheck/build; channels browser themes/mobile/axe and
Atlas chart/Mermaid/security regression. Existing 11 compiler and chunk-size
warnings remain. No real Meta/LLM/ARM64/native OCR/vector tests in this delivery.

Initial browser checkbox timing checks were changed to await persisted controlled
state rather than assume synchronous API updates. Historical fixtures exclude
new tables before testing old migrations. No authorization bypass or live reset.
Full file inventory, commands and local operation: CHANNELS_REPORT.md and
../service/README.md. Pending: live send/reconciliation/status/pricing, account-
specific Embedded Signup/Coexistence/history, general agents/human manual inbox,
full erasure, public ingress/abuse controls and distributed operations. Existing
containers, .env and SQLite were not modified; no OCI change, deploy or paid resource.

## 2026-11-08 — Explicitly authorized OCI Micro minimal deployment

User requested frontend/API/SQLite/textual retrieval/external LLM deployment on
the existing1GB Micro VM, then explicitly approved the local DeepSeek credential
and supplied santaluz.ddns.net. Created infra/oci-micro-minimal from the clean
published service branch. No main merge or local database/.env replacement.

Built linux/amd64 images off-VM; added opt-in backend resource gates/health,
login/API rate limits and global two-chat cap, minimal UI controls, standalone
Compose validation and dedicated VM scripts. Public Caddy HTTPS issued/verified;
API binds host loopback, OCI/firewalld expose80/443 only in addition to the
owner-approved public SSH rule. Data/config/certificates persist privately.

DNF preparation demonstrated a memory limit and SSH became unresponsive; controlled
OCI soft reset recovered it. Installed official static Docker29.9.3 instead of
repeating package metadata processing. Dedicated systemd socket/service, capped
containers, no OCR/ML/vector/Trino/Ollama services. No new paid resource/VM/volume.

PASS: builds/typecheck, MinimalHosting/BusinessChat tests, existing real temporary
organization/full Atlas smoke, actual minimal-container login/KB/gates429, public
HTTPS/login/KB/24 reads at concurrency3, real grounded DeepSeek answer and title,
idempotent replay, persistence after controlled API restart, real Chrome dark/light
minimal UI. Eight samples: API177.9–193MiB, Caddy34.29–71.19MiB, host available
369–404MiB, no OOM/automatic restart. Test SSE chunk assertion was corrected to
check persisted assembled output; no extra inference was needed for the replay.

Full files/commands, image IDs, protected login location, recovery and limits:
[MICRO_DEPLOYMENT.md](../../infra/oracle/MICRO_DEPLOYMENT.md). New cloud SQLite is
independent of the local installation. No new migration; existing001–009 initialize
the new DB. Long-duration load, full post-install host reboot, off-VM recovery and
production/privacy/SLO hardening remain unverified. No extra component should be
added based solely on the short demonstration measurements.

## 2026-11-09 — Full-stack capacity attempt and Micro memory recovery

User requested connectors/Qdrant/full stack. Clean branch
infra/oci-full-capacity-trial from the minimal delivery; no application code or
schema changes. Public portal/SSH already timed out before remote expansion.
Requested controlled OCI soft reboot; recovered existing containers/data.

Found confirmed448MiB crashkernel reservation, leaving498MiB usable RAM. Stopped/
disabled kdump, freed the reservation and set crashkernel=0 in installed boot
entries. RAM returned to946MiB immediately. Crash diagnostics are intentionally
unavailable on this Micro; default boot setting verified, not yet reboot-tested.

Local disposable real-model RAG OOM:768MiB and1536MiB caps both exit137/OOM=true.
Embedding succeeded in the larger trial, retrieval/reranking did not complete.
Trino512MiB: original query settings rejected; reduced query/headroom settings
proceeded to plugin loading but also OOM. Existing full local Trino measured
1.371GiB. Real isolated Qdrant collection/upsert/query passed around19–22MiB;
this is not evidence that application RAG fits. No real LLM call/private data used.

Created verified consistent on-VM SQLite/runtime snapshot, counts preserved and
integrity/FKs ok. Public HTTPS page/assets/readiness200; API/web healthy, no
automatic restart/OOM. Initial bootstrap-password authenticated smoke401; existing
active account remains, no password reset/bypass or authenticated-pass claim.
All disposable trial containers/network cleaned up; full local stack untouched.

Complete cloud deployment is blocked by memory and was not declared done.
Only recovery/backup helpers and a reproducible local capacity probe were added.
No paid/new cloud resource, replacement model, extra VM service or migration.
Full evidence, limits and next environment:
[FULL_CAPACITY_TRIAL.md](../../infra/oracle/FULL_CAPACITY_TRIAL.md).

## 2026-11-09 — Authorized A1 retry interval change

User changed capacity retries from30 to15minutes. Added validated
`-IntervalMinutes` (15–1440, default15), dynamic interval logging and updated the
existing timing test. PASS:74 offline assertions, no test OCI calls/sleeps.
Restarted only the verified sleeping provisioning process, preserving the lock
directory/journal, same2OCPU/12GB target, image, subnet and public key. The new
hidden runner explicitly uses15minutes; no concurrent OCI launch was interrupted
and no second provisioning runner was intentionally left active. Existing
capacity timestamp still determines the remaining wait after restart.

## 2026-11-09 — WhatsApp Meta test setup and actual local update

User requested continuing WhatsApp, updating local runtime and explaining Meta
registration/frontend parameters; selected the Meta test number. Clean baseline,
new feature/whatsapp-local-setup branch. Read AGENTS/service docs and inspected
current channel code/Compose/RBAC before editing. Meta developer guide remained
login-gated; primary reference links and account-specific limitations documented.

Added GET api/channels/whatsapp/setup: permission-protected, tenant-scoped approved
numbers, public IDs/callback and secret-presence booleans only. Unsafe callback
origins rejected; no secrets serialized or global configuration returned. New
WhatsAppSetupCard in existing page explains app/test-number setup and current
receive-only limitation. Optional Compose overlay/private blank env example prepared,
validated synthetically, not applied with fake/real Meta credentials.

Actual local build/up replaced only gateway/frontend. Verified consistent backup
and previous image IDs first. Existing schema7→9; fingerprints of users/password
hashes/KB/history match (1/10/1/2), integrity/FKs ok. Trino/OCR/ML remained running.
PASS: gateway/frontend image builds/typecheck, Channels unit/migration tests,
real temporary signed-webhook/worker smoke plus safe setup metadata401/403/scoping/
non-disclosure, existing organization/full Atlas smokes, channels browser fixtures
themes/mobile/axe and real updated local browser login/setup card/light/dark.
An initial live browser assertion used a PowerShell-piped accented string and was
rerun from a UTF-8 test file successfully. No password reset/local .env edit.

No Meta account created, token generated, tunnel started, real message sent or cloud
deployment changed. Live Cloud sending/Embedded Signup/Coexistence still pending;
missing app assets, private secrets and publicly routed callback block actual
receipt verification. See ../service/META_TEST_SETUP.md for next concrete steps.
# 2026-11-09 — Server-side WhatsApp Web bridge, local validation

User explicitly changed direction from Meta onboarding to a server-side browser
bridge and authorized implementation and sizing tests on the local 24-GB host.
Started from clean `feature/whatsapp-local-setup` in dedicated branch
`feature/whatsapp-browser-bridge`. No Oracle, DNS, cloud deployment, `.env`, license
or existing volume changes. The separately authorized A1 retry runner was left
running, not duplicated.

Reused organization-scoped channel entities, authorized document retrieval,
extractive agent, history, handoff, caps and retention. Added optional authenticated
Node/Chromium bridge, durable inbox/outbox, profile persistence, private QR,
connection states, reconnect/backoff/heartbeat and pause/logout. One account cannot
be bound to multiple portal connections. No arbitrary script/host input is exposed.
Added manual responses requiring handoff and opt-in automatic documentary replies.
Remote side effects occur after portal commit. Stable UUIDs deduplicate dispatch;
ambiguous delivery is shown as unknown and not automatically resent.

PASS, executed:

- `npm test --prefix whatsapp-bridge`: 4 tests, lifecycle, QR expiry, tenant/account
  separation, duplicate inbox/outbox, restart, ambiguity, stale events and heartbeat.
- `npm audit --prefix whatsapp-bridge`: 0 known vulnerabilities after locked
  dependency overrides; patched Puppeteer also passed actual QR validation.
- `dotnet run --project tests/Channels --no-restore`: migration9 idempotency,
  backup/restore/rollback, existing channel tests, browser IDs/default-off,
  manual handoff/idempotency, automatic outbox and foreign-tenant denial.
- `python scripts/smoke_channels.py`: actual JWT/HTTP/SQLite/worker, synthetic
  bridge routing and inbox acknowledgment, manual and automatic documentary
  output, unknown-send non-replay, tenant/Member denial; no real message sent.
- `python scripts/smoke_organizations.py`: organization/invitation authorization,
  isolation and lifecycle regression checks passed.
- `python scripts/smoke_local.py --atlas`: baseline sessions/usage/providers/
  settings/tenant/connectors/hybrid chat/SQLite passed with HTTP doubles, not
  external inference or actual Trino analytics.
- `npm run typecheck --prefix front` and production frontend Docker build passed.
- Docker builds for gateway/frontend/bridge passed; gateway compile warnings and
  frontend chunk-size warnings remain. No claim of warning-free build.
- `node tests/ui/channels.cjs`: API fixtures, simulator regression, browser
  QR/pause/manual reply, light/dark/mobile and axe checks passed.
- `node tests/ui/browser-channels-live.cjs`: real local login, one explicit test
  channel, automatic replies off, real Chromium opened WhatsApp Web and generated
  a QR. No QR screenshot/trace/token was saved, no phone was paired.
- `node tests/ui/channels-live.cjs`: actual updated local page/login/light/dark
  passed; screenshots remain ignored artifacts. Actual private bridge returned
  401 for missing/wrong secrets and no-store for authorized status.

Preservation: consistent private backup in
`C:\oci\ia-portal\local-browser-backups` before updating. After updating only
gateway/frontend and adding bridge, fingerprints of users/password hashes,
KB and chat history match exactly (1 user/10 documents/1 session/2 messages),
schema9->9, integrity/FKs passed. New private session volume is separate.
Trino/OCR/ML containers were not recreated; existing Trino health was unhealthy
during inspection and was not diagnosed/repaired as part of this channel task.

Sizing: host reports about 24 GB RAM, Docker 11.55 GiB budget. A first unpaired QR
session read 684.9 MiB. Three later samples at 18:50 UTC measured bridge 626.3–647.7MiB
(2.08–47.50% CPU, transient activity) and gateway 130.7–136.1MiB. Samples are in
ignored `artifacts/whatsapp-browser-resources.csv`, produced by
`python scripts/measure_browser_bridge.py --samples 3 --interval 5`.
This is not a paired-session/load/100-customer sizing result.

Initial validation issues corrected: Windows-generated dependency lock omitted
Linux optional dependencies (regenerated lock under the same Linux Node image);
HTTP fixture initially lacked chunked request parsing; an early UI run stalled
under build/memory pressure and was rerun successfully against a static frontend
fixture container. That fixture and the temporary Vite process were stopped;
the updated local stack remains running. No force-fix/downgrade was used.

Remaining gates: user QR pairing, real inbound/outbound/ack verification, paired
restart/reconnect and soak tests; media/groups/history import, external LLM agent,
distributed workers and production per-session container/sandbox hardening are
outside this increment. [Operating guide](../service/WHATSAPP_BROWSER.md).
# 2026-11-09 — Meta registration entry-point clarification

Read the user-supplied Cloud API Get Started guide. Corrected the optional Meta
setup card and guide to separate developer registration (`/async/registration/`)
from app management (`/apps`). The previous dashboard link was insufficient as
registration guidance. No credentials, Meta account actions, cloud changes or
browser-bridge behavior changes. Live documentation checks were login/429 gated;
do not claim authenticated dashboard validation. This optional card is not mounted
in the current browser-channel page, so no local container update is required.

Validation: `npm run typecheck --prefix front` passed.
# 2026-11-09 — Local Windows HTTPS edge preparation

Owner authorized moving the public hostname to this local host and configuring
HTTPS. Created clean branch `infra/local-https`; no uncommitted user changes.
Added native Caddy reverse proxy, pinned official Windows installer with SHA512
verification and private runtime ACLs, validation-first DNS/start script, owned-
process stop script, and DNS/TLS tests. This preserves loopback Docker bindings,
all Compose overrides, API authorization and current SQLite. No migration or
application code change.

PASS: Windows Caddy2.11.9 installed outside Git with published checksum match;
real Caddyfile validation; stale-A/missing-and-foreign-AAAA/CGNAT guards and no-
listener dry-run; loopback TLS with local CA trusted only in test SSL context;
actual portal HTML, security header, anonymous API401 and error-log redaction.
The smoke stops its own process and never installs a Windows trusted root. The
initial checksum parser expected SHA256/text; corrected to the release's actual
SHA512/byte-content format before extraction. A DNS mock scope issue was corrected
and the valid fixture now explicitly asserts an empty blocker list.

Public validation reports the selected hostname still targets Oracle IPv4 and
has no AAAA. Public Caddy has not been started, no public certificate requested,
no firewall/modem/DNS/Oracle resources changed and no auto-start task installed.
The owner must update AAAA to the current non-temporary local global IPv6 and
permit TCP80/443 at the modem. Actual public TLS and Meta callback remain pending.
No backend/frontend build is claimed for this edge-only change. Existing local
containers and the separately authorized OCI retry runner were preserved.
See [local HTTPS operation](../../infra/local/README.md).
# 2026-11-09 — Oracle HTTPS to local workstation through reverse SSH

Owner explicitly authorized the existing VM as HTTPS ingress to this workstation,
superseding earlier no-remote-change restrictions for this bounded task. Started
clean branch `infra/ssh-local-edge`. Implemented persistent hidden reverse SSH
runner, strict known-host verification, loopback-only19080/19089 forwards,
keepalive/ExitOnForwardFailure, singleton mutex and identity-checked stop. Added
reversible Caddy cutover helper and a bounded authenticated verification worker.
No modem/DNS changes, extra VM, shape change or paid service/resource.

VM was initially OCI RUNNING but SSH banner/HTTPS timed out. Owner approved one
SOFTRESET through the clarification tool. Waited for the OCI graceful shutdown
window without submitting another reset. On recovery, verified the ED25519 host
key against full console history captured through authenticated OCI API before
enrolling known_hosts. Never disabled host verification or copied private keys.

Actual tunnel established and remote loopback binds checked; Caddy switched to
forward the entire frontend/API to Windows, with health forwarded separately.
Original web `ia-portal-web-vm-20261110T012417Z` and original cloud API/database
preserved; old API stopped only after public/local equivalence and authorization
checks. Certificate volumes reused. No automatic fallback to the old database.

PASS: PowerShell validation/loopback/host/keepalive/invalid-host/PID-stop checks;
duplicate runner denial; Bash syntax and isolated non-loopback/unreachable-tunnel
denial, successful/idempotent cutover, launch/HTTPS failure rollback once;
actual Caddy validation; frontend Docker build and Nginx syntax; wrong webhook
verification401 in both slash forms with no synthetic token in Nginx/gateway logs.
The new Cloud edge filters request URI/headers in error logs. Local webhook
access/error logging is suppressed; backend default logging is Error.

PASS public normal TLS/readiness, exact local/public frontend HTML hash, local
JWT/organization equivalence through public HTTPS, anonymous API401, and actual
Chrome public login/Canais/light/dark using local credentials without model/Meta
calls. Private callback origin updated and applied to local gateway while preserving
all Compose overrides. Actual public verification challenge200 passed; token/URL
query contents omitted. Existing Cloud receive config remains incomplete without
a fresh App Secret, and real Cloud sending is still unimplemented.

No application schema/.NET changes or database migrations; no .NET rebuild claimed.
Existing data/users/channels and local Docker volumes preserved. One PowerShell validation
test initially used a non-public exception type and was corrected; stop/relaunch
of the owned runner exercised actual singleton recovery. Completion worker phase
is complete and exited; tunnel runner and original OCI capacity runner stay active.
No scheduled task installed, so workstation reboot/logon needs Docker/runner
startup. See [operation and rollback](../../infra/oracle/LOCAL_TUNNEL.md).
# 2026-11-09 — Recurrent Micro DNF metadata cache OOM recovery

User reported apparent DNS/public outage. DNS still resolved the expected Oracle
IPv4, local gateway ready200, remote SSH banner/HTTPS timed out while OCI remained
RUNNING. Monitoring showed memory54%->95%/CPU spike before missing samples.
Authenticated serial-console history showed recurring global OOM kills of dnf in
dnf-makecache.service using about696–736MiB anonymous RSS on a945MiB guest. No
general Oracle outage or DNS fault was inferred. Queued read-only OCI agent command
remained ACCEPTED and was not used as proof of live guest health.

Created clean `fix/oracle-dnf-cache-memory`. Owner explicitly approved limiting
the cache job and another soft restart if necessary. One SOFTRESET submitted for
this incident. A bounded singleton Windows worker waited for strict SSH recovery,
copied only the public guard script, applied it via sudo, and verified service
properties and public HTTP. No forced/reset loop, extra VM or storage change.

Guard validates the unit is makecache, preserves original state privately,
disables only its timer, and caps cache RAM256MiB/swap64MiB/CPU20%/runtime120s.
Other update/install units and packages remain untouched. The helper does not
kill arbitrary dnf/rpm processes. Operator overrides cause refusal, not overwrite.
Actual service inactive, timer disabled, memory caps/CPU/runtime confirmed.

PASS Bash container tests: dry-run no mutations, precise cache target, backup and
idempotency, override preservation and package-upgrade unit rejection. Python
worker syntax passed. Actual normal public TLS/readiness200, exact frontend hash
against local, anonymous API401 and three post-recovery memory samples passed.
Guest available312–320MiB/Caddy14–30MiB; short sampling only. Recovery worker
finished complete; existing SSH/provisioning runners preserved. Old cloud API and
web backup remain stopped/intact. No DNS/modem/.env/schema/user/database changes.
No application build/tests claimed for this OS-only correction. Detailed evidence,
operation and limitations: [DNF cache incident](../../infra/oracle/DNF_CACHE_INCIDENT.md).
# 2026-11-09 — Owner-authorized removal of stopped cloud app containers

Verified both `ia-portal-api` and `ia-portal-web-vm-20261110T012417Z` were stopped
before removal, and verified the remaining web carries the local-tunnel edge label.
Removed exactly those two containers without force/volume/image pruning. Preserved
the active HTTPS proxy, original database/certificate directories and images.
Moved the historical rollback marker to `previous-web-container.removed`; recovery
now needs container recreation rather than restarting the deleted backup.
Actual public HTTPS readiness200 passed. No application code, schema or build
changes. Stopped containers were not consuming RAM; the active required proxy
was around34MiB. Two initial shell/line-ending attempts exited before mutation;
the successful SSH stdin used LF bytes and rechecked state before removal.
# 2026-11-10 — Persistent Windows logon startup

Owner authorized automatic recovery after workstation restart. Started clean
`infra/windows-autostart`. Existing Oracle edge already had unless-stopped and
enabled Docker systemd; verified read-only. Locally gateway/frontend/OCR/ML/Atlas
Trino lacked durable restart policies. Added unless-stopped to base/Atlas Compose
and applied it to all six existing running containers with docker update; no
recreation, data reset, private-env changes or optional RAG activation.

Registered current-user interactive limited-privilege task IA-Portal-ReverseTunnel,
logon trigger, hidden PowerShell, unlimited duration, IgnoreNew, task-failure retry.
No password stored and no Windows automatic login enabled. Docker Desktop registry
startup was disabled; the task explicitly starts the existing installed executable
if missing, waits for gateway readiness, then runs the original strict SSH loop.
Installed public bootstrap/runner/stop scripts outside Git under local AppData so
changing branches does not remove startup files. No SSH key contents copied.

PASS actual task registration, canonical username->SID ownership validation,
idempotent re-registration, current-user task execution, installed runner PID/SSH
child, task Running/267009, public normal TLS/readiness200. A repeated start was
ignored without duplicate runner. During action replacement, Scheduler terminated
the parent while leaving its SSH child; stopped that verified old PID/start time
and corrected the helper's terminated-parent race/path validation. Existing
reverse-tunnel safety tests passed afterward. Foreign tasks are refused by marker
and owner checks. Combined Compose and PowerShell syntax checks passed; all six
running container restart policies verified. No machine/Docker reboot was performed
and cold-start Docker launch remains to be verified at the next actual logon.

No .NET/frontend business-code changes or builds claimed for this startup-only
change. No DB migration, credential reset, cloud restart, paid resource or model
call. Docker Desktop still requires user login; the PC must stay powered/awake.
Details and maintenance commands: [Windows startup](../../infra/local/WINDOWS_AUTOSTART.md).
# 2026-11-10 — GPU compatibility diagnosis without changing live inference

User asked whether WSL/Docker could use the workstation GPU. Clean branch
`infra/gpu-diagnostics`. Inspected actual adapters, driver/WSL and inference code:
MX4502GiB/driver581.80; WSL2.5.9.0/kernel6.6.87.2. Gateway uses CPU ONNX package
and default sessions; chat currently uses textual retrieval/external LLM, OCR
Tesseract and ML simple .NET arithmetic. No CUDA provider is selected in these
services. GPU exposure alone would not offload general CPU/RAM/DB operations.

Added reproducible isolated CUDA driver probe and operating findings. Pulled
python:3.12-slim, then ran the probe with --gpus all, --network none and only the
read-only script mount. PASS actual allocation/JIT/kernel/synchronization/copyback:
GeForce MX450,2047MiB,SM7.5,41.0->42.0. Resources freed on exit; no model downloads
or private mount/API call. This proves Docker GPU compute access, not ONNX model
compatibility, inference speed or sustained utilization.

No live container, driver, WSL configuration, .env, data, provider or advanced-RAG
mode changed. Existing CUDA-consuming application inference is not claimed. No
application build/tests required or claimed for this standalone diagnostic; the
actual kernel result is the executed validation. Further CUDA inference work must
select compatible runtime libraries and measure the2GiB budget with real models.
See [GPU findings](../../infra/local/GPU.md).

## 11/11/2026 — Cadastro público Atlas e colaboração sem exclusão

Branch `feature/atlas-public-signup`, origem `infra/gpu-diagnostics`, árvore limpa
antes das edições. Pedido inicial de Admin foi corrigido pelo proprietário para
consulta, criação e edição sem exclusão. Implementados:

- Grupo Atlas — Colaboração: chat, KB, edição, dashboard e dados demonstrativos
  concedidos; sem usuários/grupos, configurações, desativação ou reindexação geral.
- `kb.edit` no catálogo/política/controller/UI, mantendo `kb.manage` anterior.
  Payload de upsert não permite contornar a restrição via IsActive.
- Cadastro opt-in, SMTP STARTTLS, confirmação explícita com validade de 30 minutos,
  conta/membership criadas somente após confirmação. Token somente como hash no
  SQLite; senha com BCrypt; SMTP por variável privada. Reuso/conta existente não
  altera privilégios ou reativa contas. Limitadores de POST e status separados.
- Migração aditiva v10, backup anterior, checksums e transação; fixtures antigas
  ajustadas para não incluir a tabela nova em esquemas de versões anteriores.
- Tela pt-BR usando identidade/temas atuais, link no login, fragmento removido e
  confirmação sem consumo automático. Corrigida navegação na mesma aba detectada
  por Chromium. `.env.example` e Compose contêm apenas opções sem credenciais.

Comandos/resultados efetivos:

- `dotnet build ai-gateway --no-restore -v quiet`: PASS, 0 erros; 12 avisos em
  arquivos anteriores à entrega (nulabilidade, métodos async, variáveis/atributos).
- `dotnet run --project tests/Signup --no-restore`: PASS. Banco temporário,
  migração v9→v10, idempotência/preservação, backup/restauração, falha de DDL com
  rollback, ausência de conta antes de confirmar, senha/token não literais,
  cooldown, grupos/fontes, expiração/replay, cadastro desligado e falha SMTP double.
- `python scripts/smoke_signup.py`: PASS. Gateway real isolado, login/confirmação,
  destino injetado ignorado, create/edit permitidos, IsActive forjado neutralizado,
  toggle/reindex/admin bloqueados, isolamento de organização/conversa e 429.
- `dotnet run --project tests/Invitations --no-restore`, `tests/Workspace`,
  `tests/Channels`, `tests/Organizations`, `tests/Usage`, `tests/Connectors`:
  PASS em todos (mesmo comando com cada caminho de projeto).
- `npm run typecheck` e `npm run build` em `front`: PASS. Vite avisa sobre chunks
  acima de 500 kB; sem alteração de dependências do produto.
- `node tests/ui/signup.cjs`, com Playwright instalado em diretório ignorado e
  Chrome local: PASS, formulário, confirmação explícita em React StrictMode,
  remoção do fragmento, temas claro/escuro e 390px, sem erros JS.
  Capturas em `artifacts/signup-ui/confirm-{dark,light,mobile}.png` (fora do Git).
- `docker compose config --quiet` e `git diff --check`: PASS.

Ocorrências corrigidas durante validação: fixture v7 inicialmente incluía a nova
tabela, teste HTTP inicialmente não tinha modelo habilitado e mantinha conexão
SQLite aberta no cleanup do Windows; seletores de campos obrigatórios e navegação
de fragmento no navegador foram corrigidos. Testes citados acima passaram após
as correções; nenhuma falha foi tratada como validação positiva.

Limites: SMTP real, entrega Gmail, túnel/Oracle e atualização dos containers não
executados. Falta URL HTTPS pública e senha de app configurada pelo operador no
ambiente local; a senha de conta fornecida na conversa não foi usada nem gravada.
Nenhuma leitura de `.env`, alteração na base viva, deploy, commit ou merge.
Sem chamadas LLM ou mensagens reais. Os testes de navegador usam fixtures HTTP;
os testes HTTP usam token semeado sintético, não prova de entrega de e-mail.
Rate limiting é local/em memória; proxies podem compartilhar limite por IP;
recuperação de senha/CAPTCHA não incluídos. Para ativação e preview, veja
[cadastro Atlas](../features/PUBLIC_ATLAS_SIGNUP.md).

## 11/11/2026 — Chat com somente Ollama na instalação local

Pedido do proprietário: deixar apenas Ollama disponível no chat. Alteração
operacional pelas APIs autenticadas/versionadas existentes, sem modificar código,
reconstruir containers ou aplicar as alterações pendentes do cadastro Atlas.
O ambiente em execução aponta para `https://ollama.com/` (Cloud); consulta local
a `127.0.0.1:11434/api/tags` não respondeu. Nenhum modelo local foi instalado.

Metadados reais do endpoint configurado confirmaram os modelos disponíveis.
Ollama habilitado com essa lista, padrão `gpt-oss:20b`; DeepSeek e OpenAI
desabilitados. Padrão global, padrões das organizações administráveis pela conta
atual e preferências dessa conta foram atualizados para Ollama. Configuração
persistida no SQLite via APIs, sem manipular diretamente tabelas, credenciais,
mensagens ou históricos. Snapshot dos ajustes anteriores de provedores/padrão
global em `artifacts/ollama-only/previous-provider-settings.json`, fora do Git;
não contém chaves ou tokens. Credenciais preexistentes usadas só em memória,
sem leitura/impressão de `.env` e sem logs de autenticação.

PASS: teste de metadados pela API do gateway retornou sucesso; listagem de
provedores selecionáveis retornou somente `ollama`; tentativa de selecionar
DeepSeek pela API de preferências retornou 409. Não houve inferência real,
chamada a modelos pagos ou criação de conversas de teste. Builds/tests de código
não repetidos nesta alteração exclusivamente operacional. Conversas/preferências
de outros usuários previamente vinculadas a provedor desabilitado não foram
migradas silenciosamente: selecionar Ollama explicitamente no fluxo existente.
Administradores da instalação ainda podem reabilitar provedores pela configuração.

## 11/11/2026 — Aviso de uso do Ollama na tela de login

Pedido: informar os visitantes sobre acesso gratuito à demonstração e limites do
Ollama. Acrescentado Alert informativo em `front/src/pages/LoginPage.tsx`, antes
do formulário, em pt-BR: demonstração gratuita, chat exclusivamente com Ollama
Cloud e orientação de aguardar/tentar mais tarde quando o limite for atingido.
Preservadas as alterações pendentes do cadastro Atlas e o shell existente.

Validação: `npm run typecheck` e `npm run build` em `front` PASS; build mantém o
aviso de chunks grandes. `docker compose build portal-front` PASS (typecheck/build
também dentro da imagem); `docker compose up -d --no-deps portal-front` PASS.
Atualizado apenas o container do frontend; gateway/SQLite/SMTP não reconstruídos
ou migrados nesta atualização. Compose avisou sobre bridge órfã por ausência do
override na chamada; nenhum container órfão foi removido.

Chromium real contra `http://127.0.0.1:9080/login`: PASS, aviso e botão Entrar
visíveis; viewport 390px sem overflow horizontal. Capturas ignoradas em
`artifacts/login-ollama/{desktop,mobile}.png`. Primeira verificação teve seletor
com acentos alterados pela codificação do shell; repetição com regex ASCII passou.
`git diff --check` PASS. Não repetidos testes .NET por se tratar de texto/frontend.
Não houve verificação da URL externa Oracle nem teste de login autenticado nesta
alteração. Sem commit/merge.

## 11/11/2026 — Origem pública para confirmação de cadastro

O proprietário informou `https://santaluz.ddns.net`. Configurada como
`PORTAL_PUBLIC_URL` no `.env` privado e configurado o remetente SMTP informado
anteriormente; nenhuma senha foi inserida ou exposta. Verificação de presença
de configuração constatou ausência de `SMTP_APP_PASSWORD`, inclusive no gateway
em execução. Cadastro mantido desligado; backend não foi reconstruído/recriado.

PASS: GET HTTPS de `/login` no domínio público retornou 200 com HTML do portal,
com validação normal de certificado; `docker compose config --quiet` aceitou a
configuração; `git check-ignore .env` confirmou exclusão do Git. Isso confirma
acesso público à tela, não entrega SMTP nem confirmação real de e-mail.
Pendente: operador configurar senha de app Google diretamente em
`SMTP_APP_PASSWORD` no ambiente privado para concluir ativação/teste de envio.

## 11/11/2026 — Ativação pública do cadastro Atlas

Após o operador informar que terminou, verificada presença de senha de app no
ambiente privado (nenhum valor impresso). PASS: autenticação real em
`smtp.gmail.com:587` com STARTTLS e certificado validado, sem enviar mensagem.
Habilitado `ATLAS_PUBLIC_SIGNUP=true` e mantida origem pública informada.

`docker compose` com todos os arquivos/overrides da instalação: build do
`ai-gateway` PASS, incluindo restore/publish .NET Release, com avisos anteriores.
Imagem anterior etiquetada `ia-portal-ai-gateway:pre-signup-20261111` para retenção.
Validação inicial detectou variável do bridge ausente no terminal; recuperadas
em memória as variáveis WhatsApp do container existente, sem imprimir/persistir
novas cópias de segredos. Comparação da configuração resolvida confirmou que
todas as opções anteriores do gateway foram preservadas; acrescentadas apenas
opções Signup/SMTP. Nenhuma reconfiguração/removal do bridge ou Trino.

Atualizado somente gateway por `up -d --no-deps ai-gateway`, com os mesmos
volumes/overrides; readiness PASS. Reload do nginx no frontend para atualizar
resolução do upstream. Migração v10 aplicada pelo startup; confirmado um arquivo
`pre-chat-v10-*` no volume privado de backups, criado antes da evolução do schema.
Sem apagar/recriar banco ou volume. Rollback exige também cópia pré-v10, conforme
guia, pois a imagem anterior não aceita ledger 10.

PASS em verificação real local e pública: `/api/auth/signup/status` habilitado;
cadastro inválido rejeitado com 400; login existente funciona; provedores
selecionáveis continuam somente Ollama. Chromium contra HTTPS público confirmou
botão de cadastro no login, navegação e formulário; captura ignorada em
`artifacts/signup-public/form.png`. `git diff --check` PASS. Testes isolados de
cadastro/migração já registrados nesta entrega não foram repetidos, sem mudança
de código desde sua aprovação.

Limite: nenhum e-mail real foi enviado e nenhuma conta foi criada pelos checks
desta ativação. Autenticação SMTP real não prova entrega na caixa de entrada;
primeiro cadastro válido deverá confirmar recebimento/link e concluir o fluxo.
Sem exposição de senha, tokens ou informações de contas em relatórios; sem
commit/merge.

## 11/11/2026 — Nome do aplicativo no cadastro

Correção solicitada pelo proprietário: nome do produto IA-Portal; Atlas é a base
demonstrativa. Ajustados botão no login, título e descrição do cadastro, corpo
do e-mail e mensagem de confirmação. Fixture de navegador atualizada para o
novo texto. Identificadores internos, permissões e dados da Atlas preservados.

`docker compose build ai-gateway portal-front`: PASS, incluindo typecheck/build
Vite e restore/publish .NET Release; mantidos avisos prévios de compilação/chunks.
Publicados gateway/frontend usando os mesmos overrides/volumes, com comparação
da configuração resolvida confirmando ambiente inalterado. Readiness PASS.
Chromium real em HTTPS público confirmou “Criar conta no IA-Portal” no login e
“Crie sua conta no IA-Portal” no cadastro; status permaneceu habilitado.
`git diff --check`: PASS. Sem novo schema, envio real, teste de entrega ou testes
unitários adicionais nesta alteração de textos. Sem commit/merge.

## 11/11/2026 — Log de ações e refinamento visual do login

Pedidos: histórico pesquisável por administradores, incluindo login; concluir e
atualizar a stack local; refinar o login mantendo layout/temas. Branch
`feature/action-audit`, criada a partir da branch de cadastro com suas alterações
pendentes preservadas. Inspeção e escopo comunicados antes das edições.

Implementados middleware e tabela append-only de auditoria, migração v11 com
backup/checksum/validação de colunas/índices/triggers, permissão `audit.read`, API
de pesquisa e tela pt-BR `/audit`. Registra requisições `/api`, inclusive leituras,
login correto/incorreto, saída, alterações, falhas e acessos recusados. IDs de
documentos/sessões criados registrados explicitamente quando conhecidos. Template
de rota em vez de URL bruta; sem corpo/query/segredos/conteúdo de conversa. Falhas
SSE e diagnósticos negativos de conectividade recebem resultado de falha mesmo
com HTTP 200. Identidade vem do login correto ou do contexto autenticado validado,
nunca de IDs de cliente. API de saída apenas registra ação; não muda revogação JWT.

Leitura limitada à organização; visão global e eventos sem autenticação apenas
pela organização 1 com a permissão. Atlas — Colaboração não recebe acesso aos
logs. Filtros de período/usuário/pesquisa/resultado e paginação por cursor,
limites de período e tamanho; sem edição/exclusão de registros pela interface/API.
Escrita independente com timeout e diagnóstico sanitizado em falha, preservando
ações já concluídas. Limites de cobertura, retenção e durabilidade documentados em
[ACTION_AUDIT](../features/ACTION_AUDIT.md).

Login preserva duas colunas, marca aprovada, tokens, temas/default escuro e fluxos
de autenticação, convite, cadastro e aviso Ollama. Usada a imagem de fundo
fornecida/aprovada pelo proprietário, com CSS próprio, cartões translúcidos, animações leves, sombras e
visibilidade de senha acessível; movimento reduzido respeitado. A referência
externa foi aberta publicamente e inspecionada visualmente; nenhum código, asset,
marca, dados ou regra de negócio da referência foi importado. Sem dependências
novas de produto. Captura de referência somente em artifacts ignorados.

Resultados locais antes do deploy:

- `dotnet run --project tests/Audit --no-restore`: PASS (primeira execução sem
  `--no-restore` restaurou o projeto). v10→v11, preservação/idempotência, backup
  anterior/restauração, falha DDL e rollback, triggers append-only, escrita,
  identidades, falhas HTTP/SSE/exceção, ausência de segredos e pesquisa/escopos.
- `python scripts/smoke_audit.py`: PASS com gateway real e SQLite temporário.
  Login correto/incorreto, saída, busca/cursor/limites, escopo, admin permitido,
  membro 403, IDs forjados ignorados e segredos/queries/documento não persistidos.
- `python scripts/smoke_signup.py`: PASS após inclusão do middleware.
- `dotnet run --project tests/Signup --no-restore`, `tests/Invitations`,
  `tests/Workspace`, `tests/Organizations`, `tests/Channels`, `tests/Usage`,
  `tests/Providers`: PASS, usando o mesmo comando com cada caminho. Fixtures de
  versões antigas ajustadas para omitir a tabela nova. Sem serviços externos.
- `npm run typecheck` e `npm run build` em `front`: PASS. Mantido aviso de chunks
  grandes; sem alteração de pacote/lockfile.
- `node tests/ui/audit-login.cjs`: PASS, com Playwright/Chrome locais. Login nos
  dois temas e mobile, modos animado/reduzido, mostrar/ocultar senha e erro de
  credenciais; audit pesquisa/paginação/vazio, temas/mobile, menu/rota recusados
  para viewer e requisição autenticada de logout antes da navegação.
- `node tests/ui/signup.cjs`: PASS, mantendo confirmação explícita e formulário.
- `git diff --check`: PASS.

Falhas corrigidas na validação: namespace de RoutePatternFactory no teste;
seletor exato de célula que também contém subtítulo; espera pelo breakpoint após
resize do navegador (sem overflow persistente). Capturas em
`artifacts/audit-login-ui/`, com identidades sintéticas e API fixtures.
Nenhuma chamada SMTP/LLM real ou conta criada nesses testes.

Antes da atualização, registrados somente contadores de documentos/memberships
das organizações autorizadas em artifact ignorado, sem dados pessoais, e retida
imagem do gateway como `ia-portal-ai-gateway:pre-audit-20261111`. Runtime/deploy e
checks públicos são registrados abaixo após sua execução.

Durante a entrega, apareceu o arquivo do proprietário
`Portal de IA em Neon Futurista.png`; confirmado explicitamente seu uso no fundo.
Fonte original preservada; cópia sem alteração de pixels em
`front/public/images/login-neon-portal.png` (aprox. 1,7 MB). Sobreposição por CSS
ajusta contraste nos dois temas, mantendo a marca aprovada no cabeçalho. Essa
imagem substitui a ilustração geométrica inicial, conforme a escolha do proprietário.

### Atualização local e verificação pública

`docker compose build ai-gateway portal-front`: PASS (publish .NET Release e
typecheck/build Vite dentro das imagens). Após aprovação da imagem de fundo,
`docker compose build portal-front`: PASS novamente com o asset definitivo.
Usados os mesmos arquivos/overrides/volumes da instalação. Variáveis privadas de
WhatsApp recuperadas apenas em memória do container anterior; comparação da
configuração resolvida confirmou ambiente do gateway inalterado. Atualizados por
`up -d --no-deps ai-gateway` e depois `up -d --no-deps portal-front`; nginx
recarregado após a troca de upstream. Nenhuma recriação de volume/banco.

Readiness do gateway PASS. Startup aplicou v11; confirmado um backup
`pre-chat-v11-*` no volume privado. Contadores de documentos e membros das
organizações autorizadas coincidem com os anteriores. Login existente funciona,
cadastro público permanece habilitado e somente Ollama permanece selecionável.
Sem criação de conta, envio SMTP ou inferência LLM nestes checks.

PASS em APIs reais: permissão administrativa, pesquisa, login correto e incorreto
registrados, leitura anônima 401, escopos organização/instalação, resultado sem
credenciais do ambiente/token. Tentativa incorreta de login usou identidade
sintética sem conta. Nenhuma falha `ActionAuditAppendFailed` encontrada nos logs
do novo gateway; conteúdo dos logs não foi impresso.

PASS com Chromium em `https://santaluz.ddns.net`: login público, botão de cadastro,
asset de fundo (200/tamanho esperado), temas claro/escuro e 390px sem overflow;
busca real administrativa por Auth.Login; saída pelo botão, retorno ao formulário
de login e confirmação de **novo** registro Auth.Logout por ID superior ao
anterior. Apenas login público fotografado, em
`artifacts/audit-login-public/login-{dark,light,mobile}.png`; nenhuma captura de
metadados reais de usuários/logs privados. Screenshots de audit da suíte continuam
usando somente fixtures sintéticas.

Na verificação pública foram ajustados seletores de teste para a codificação do
shell e o título repetido no header/conteúdo. Aguardar uma resposta de fetch
keepalive depois da navegação não é confiável em Playwright; o check definitivo
verificou emissão da requisição, tela de saída e novo registro persistido no
servidor. Testes corrigidos passaram; não houve falha funcional de logout.

Estado dos demais serviços preservado: WhatsApp/ML/OCR continuam em execução;
Trino mantém `unhealthy` que já existia antes desta entrega, sem alteração ou
afirmação de validação da análise de dados. Auditoria cobre operações das APIs a
partir desta versão, não histórico anterior/cliques locais/todas as tarefas de
workers. Sem retenção automática nem garantia de escrita em falha de disco/banco;
consultar o guia para limites operacionais e rollback com backup pré-v11.
`git diff --check`: PASS; sem commit, push ou merge.

## 11/11/2026 — Fonte padrão do chat e dica de configuração

Branch: fix/chat-default-data-source; árvore inicialmente limpa. Alterados
BusinessSourcePicker.tsx (única fonte autorizada selecionada ao carregar, data
local atual; demo com referência fixa), ChatPage.tsx (dica visível mesmo com
configurações recolhidas) e BusinessChatContextService.cs (esclarecimento com
caminho para os controles). Múltiplas fontes mantêm escolha explícita; não
reaplicar padrão depois de escolha manual de Somente documentos. Sem migração.

Validação executada:
- `cd front; npm run typecheck`: PASS.
- `cd front; npm run build`: PASS; aviso de chunks maiores que 500 kB.
- `dotnet run --project tests/BusinessChat/BusinessChat.csproj`: PASS, incluindo
  build do gateway com warnings; SQLite temporário e double de Trino, sem fonte
  real. Autorizações e ausência de indicadores inventados verificadas pela suíte.
- `git diff --check`: PASS.

Não realizados: teste de navegador/temas, consulta real, deploy ou reinício de
containers. Preview: iniciar frontend com `cd front; npm run dev` usando a API
local configurada; conferir seleção da única fonte e data em Configurações da
conversa. Mudança disponível no código local; sem commit, push ou merge.

## 11/11/2026 — Recuperação da consulta Atlas após perda do Memory

Screenshot do usuário já continha fonte/data; falha agora vinha do Trino.
Diagnóstico ao vivo: `SHOW TABLES FROM memory.atlas` como atlas_reader retornou
schema inexistente. Trino Memory é volátil; conexão/probe SELECT 1 não comprova
existência das tabelas. Containers permaneceram em execução, sem recriação.

Executados:
- `python scripts/atlas_trino_load.py`: PASS; schema ausente inicializado com as
  sete tabelas sintéticas e manifest, sem alterar SQLite ou dados do portal.
- `python scripts/atlas_trino_load.py --verify`: PASS, quatro SELECTs de
  conferência no Trino real, incluindo valores exatos por faixa.
- Segunda execução do loader: PASS, sem escrita/duplicação.
- SQL aging extraído de ApprovedTrinoQuery.cs, referência 2026-11-01, executado
  como atlas_reader no Trino real: PASS, cinco faixas iguais ao baseline.

Limites: não enviado chat autenticado nem chamada LLM; usuário deve reenviar a
pergunta. Healthcheck continua unhealthy por erro de parsing de porta no script
nativo da imagem; SQL responde normalmente. Nenhuma alteração/recriação do
container para corrigir healthcheck nesta intervenção. Reiniciar Trino Memory
volta a exigir carga; persistência futura necessita incremento específico.

## 2026-11-11 — Automatic Atlas dataset recovery

Scope: scripts/atlas_trino_load.py, trino/atlas/loader.Dockerfile,
docker-compose.atlas.yml, trino/atlas/compose.yml, tests/test_atlas_loader.py,
and Atlas operations/audit documentation. Prior uncommitted chat changes kept.
No SQLite migration, deletion, volume reset, authentication or API changes.

Implementation: unprivileged read-only Python sidecar, pinned base image,
dedicated demo network, health marker only after dataset verification. Checks
manifest/counts every 30 seconds; reloads only an empty schema; never overwrites
partial/different datasets. Server startup failures retry every 5 seconds.
Gateway depends on loader readiness. Explicit curl healthcheck avoids the Trino
image script parsing a CRLF port value as part of the URL.

Commands/results:
- `docker compose config --quiet`: PASS (no secret configuration printed).
- `docker compose -f trino/atlas/compose.yml config --quiet`: PASS.
- `python -m unittest discover -s tests -p "test_atlas_*.py" -v`: PASS, 8 tests;
  deterministic generation, no overwrite, readiness failure/recovery and fast
  restart verification. No new frontend/.NET changes in this increment.
- `docker compose build atlas-loader`: PASS.
- `docker compose up -d --no-build atlas-trino atlas-loader`: applied locally;
  recreated volatile Trino, automatically loaded seven tables and verified.
- `docker compose up -d --no-build ai-gateway`: PASS, waited for healthy loader;
  Compose also recreated ML/OCR dependencies; existing images, no source rebuild.
- `docker compose up -d --no-deps --no-build --wait --wait-timeout 120 atlas-loader`:
  final image applied and healthy after idempotent validation.
- `docker compose restart atlas-trino`: recovery test without manual loading.

Startup checks made before readiness returned SERVER_STARTING_UP / missing
schema/tables as expected; no false successful validation was recorded.
No browser/chat/paid LLM test in this increment. Memory still does not preserve
manual edits; this mechanism restores only the fixed synthetic baseline. No
commit, push, merge, database reset or changes to WhatsApp containers.

Final runtime result: PASS after restarting the final Trino/loader deployment;
all four real verification SELECTs passed without manually loading any data.
Final loader logs confirm automatic reload plus verification. Trino and loader
both healthy, gateway running. `python scripts/atlas_trino_load.py` after recovery:
PASS, no writes/duplicates. `git diff --check`: PASS (CRLF normalization warnings).

## 2026-11-11 — LiteLLM integration and LangGraph preparation

Branch: feature/litellm-gateway. Architecture summary and implementation plan
presented before source edits, then explicitly approved. Pending chat-default
and Atlas startup-recovery changes preserved, with no commit, push or merge.

Files in this increment:
- ai-gateway/Infrastructure/Qdrant/Services/LLMs/LiteLlmService.cs: additive
  OpenAI-compatible provider, trusted OrganizationScope, request/call correlation,
  actual deployment metadata, streaming and CancellationToken; no C# retries.
- ChatCompletionAdapter.cs: bounded extension hooks, shared protocol preserved.
- ILlmMetrics.cs, AiProviderCatalog.cs, Program.cs: metadata, catalog and DI.
- ChatUsageService.cs and ChatTitleService.cs: correlated logs; SQLite usage
  remains one final invocation plus separate title, not sum of proxy attempts.
- front/src/pages/AiSettingsPage.tsx and UsagePage.tsx: LiteLLM label/filter.
- docker-compose.yml, docker-compose.litellm.yml, .env.example: opt-in proxy,
  environment-only credentials, no public port or dependency for direct adapters.
- infra/litellm/{routes.json,configure.py,audit.py}: configurable capability
  allowlists, billing/privacy authorization, retry/cooldown budgets and separate
  JSONL attempt/fallback ledger in dedicated persistent volume.
- tests/LiteLlm/{LiteLlm.csproj,Program.cs,fake_upstream.py},
  tests/test_litellm_policy.py and tests/litellm_proxy_smoke.py.
- README.md, docs/README.md, docs/features/LITELLM.md,
  docs/architecture/{AS_IS_AUDIT.md,LANGGRAPH_PREPARATION.md}, this log.

Proxy image fixed at docker.litellm.ai/berriai/litellm:v1.90.2 with digest
sha256:19a879f9c4ed466f2438aa08b22e2b472053ab57c6325db73c71d0624d2d4953.
No LangGraph runtime, tool transport/executor in C#, SQL changes, SQLite schema
migration, authentication changes or removal of existing provider implementations.

Validation actually executed:
- `cd front; npm run typecheck`: PASS.
- `cd front; npm run build`: PASS, existing large chunk warning (>500 kB).
- `dotnet run --project tests/Providers`: PASS, all three direct adapters,
  selection, ownership, titles, payment/rate-limit errors and migration regression.
- `dotnet run --project tests/LiteLlm`: PASS, provider settings/selection,
  trusted tenant/correlation, actual model, streaming, single usage + title,
  disabled/unavailable proxy, explicit direct adapter, 429, truncation and
  cancellation. Unknown cost is retained as null; no retry in C#.
- `dotnet run --project tests/Usage`: PASS, accounting, pricing, isolation,
  unavailable metrics, partial/failed calls and migration regression.
- `dotnet run --project tests/BusinessChat`: PASS, analytical/documental policy,
  grants, grounding and no invented metrics; temporary SQLite/query double.
- `python -m unittest discover -s tests -p test_litellm_policy.py -v`: PASS,
  six tests (route substitution, secret references, free account attestation,
  free-model validation, local-only privacy, paid authorization + budget).
- `python tests/litellm_proxy_smoke.py`: PASS with real pinned proxy and
  synthetic upstreams in disposable Docker containers. 429/503/timeout fallback,
  bounded retries/cooldown, Groq wire adapter, plain/SSE tool-call passthrough,
  successful-result tokens, denied authentication/unknown paid model, failed
  attempts, actual Groq provider, fallback events and tenants 7/8 in safe ledger.
  All test containers/networks cleaned; no production volumes or provider keys.
- `docker compose config --quiet`: PASS.
- `docker compose -f docker-compose.yml -f docker-compose.atlas.yml -f docker-compose.litellm.yml --profile litellm config --quiet`: PASS, without printing secrets.
- `git diff --check`: PASS, CRLF normalization warnings only.

Issues found and resolved during validation: local variable collision in shared
SSE code; parallel .NET builds collided on the shared project output, so later
.NET checks ran sequentially. Mock Groq response initially omitted service_tier.
Callback loading requires an absolute module path because generated config is
in /tmp. Deployment cooldown must be explicit to avoid Retry-After: 0 overriding
router-wide cooldown; cooldown tests count router failures separately from SDK
attempts. Initial image exploration used v1.81.3; final real-proxy validation
uses v1.90.2 and its digest above. Final tests passed after those fixes.

Limits: no real Groq/OpenRouter inference because credentials/account eligibility
were not supplied for this work; no paid inference. No browser/theme runtime
suite in this increment. Proxy/dotnet/frontend source not deployed to the running
portal, no live credentials or .env modified. To activate, follow LITELLM.md;
existing direct providers remain configured independently. Provider quota is
external, never guaranteed free. Paid destinations excluded by default; enabling
one requires route allowlist, explicit paid-route authorization and a budget.
The router budget is operational/process-dependent, not a durable hard financial
cap. Use provider-side billing limits for that requirement. Callback ledger is
not transactional billing: unknown failure costs stay unknown and reconciliation
with provider invoices remains necessary. Tool calls are tested at the proxy
only, as requested. Future LangGraph contracts/diagrams are documentation only.

## 2026-11-11 — Activate Atlas demonstration on Groq via LiteLLM

Explicit authorization: same branch, preserve pending chat/Atlas work, use the
GROQ_API_KEY already placed by owner in .env as the demonstration default.
No key values were printed, placed in source, sent to browser or stored in DB.

Changes: optional insert-only Atlas default in AtlasKnowledgeSeed.cs; variables
in docker-compose.atlas.yml/.env.example; seed tests in tests/AtlasKnowledge;
Groq fast deployment reasoning_effort=low through routes.json/configure.py,
with a policy test preventing credential/budget overrides via tuning; generic
cloud quota notice in LoginPage.tsx; operations documentation. LiteLLM memory
limit raised to 1536m after observing about 955 MiB RSS under its former 1 GiB
limit. front/Dockerfile uses 300000 ms network timeout/concurrency 4 for Yarn,
preserving the lockfile and dependency versions.

Private local configuration: kept supplied GROQ_API_KEY; generated strong internal
LiteLLM master key; enabled LiteLLM/free-account route and Atlas litellm/fast
initial default; appended LiteLLM override/profile to existing Atlas Compose
settings. .env stays ignored by Git. No real secret values in this record.

Before application settings writes: created a consistent SQLite backup inside
private portal_data/backups using sqlite3 backup; integrity_check returned OK.
No schema migration, reset, deletion, password change or volume removal.
Updated provider through authorized installation/global API; set organization
Atlas default and current administrator's Atlas preference to litellm/fast.
Preserved other users' explicit preferences and existing conversation selections.

Commands and actual validation:
- Groq /openai/v1/models: key accepted; gpt-oss-20b available. Initial Python
  default User-Agent was rejected (403); explicit validation User-Agent succeeded.
- Real proxy short inference: HTTP 200, groq/openai/gpt-oss-20b, nonempty answer,
  80 prompt + 22 completion = 102 tokens. Operator probe recorded only in gateway
  ledger, not fabricated as a portal usage row.
- `dotnet run --project tests/AtlasKnowledge`: PASS, opt-in/idempotent default,
  administrator-selected default preserved, existing knowledge/membership tests.
- `python -m unittest discover -s tests -p test_litellm_policy.py -v`: PASS, 7 tests.
- `cd front; npm run typecheck`: PASS.
- `docker compose build ai-gateway portal-front`: gateway image built/published;
  initial combined command failed on Yarn icons-material ESOCKETTIMEDOUT.
- After bounded network timeout/concurrency adjustment,
  `docker compose build portal-front`: PASS, frozen dependencies, typecheck/build
  passed, existing peer dependency/chunk-size warnings only.
- `docker compose up -d --no-deps litellm`, targeted recreation for memory limit,
  `docker compose up -d --no-deps --no-build ai-gateway` and portal-front: applied.
  `docker compose restart litellm`: final deployment tuning loaded.
- Provider diagnostic via authenticated API: connected, no chargeable diagnostic.
  Attempt from Atlas context to change installation settings correctly returned
  403; configuration then used authorized global scope, without relaxing RBAC.
- Authenticated new Atlas conversation: default litellm/fast, real financial
  stream completed/persisted through Groq, five exact Trino aging buckets matched.
  First run recorded chat completed + title invalid_response (1680 tokens total);
  local fallback preserved the title. This exposed GPT-OSS reasoning compatibility.
- After low-effort deployment tuning: new authenticated documentary conversation
  completed, automatic title generated; exactly two completed usage rows,
  chat 1296 tokens and title 176 tokens (1472 total). No C# transport change.
- Running Nginx /login and served JS asset: PASS, new LiteLLM settings and generic
  quota notice present, old exclusive Ollama Cloud wording removed.
- Persistent attempt ledger: correlated Groq events with organization/request/
  call IDs and reported tokens verified without printing raw records/content.
- `docker compose config --quiet`, `git check-ignore .env`, `git diff --check`:
  PASS (CRLF normalization warnings only). Trino, loader and LiteLLM healthy;
  gateway and frontend running. No Trino/SQLite data reset or WhatsApp changes.

All operational tests used synthetic Atlas data or short synthetic prompts.
Groq inference is now real and uses the owner's authorized account/free quota;
no paid fallback enabled. External quota/availability still applies. Older
Ollama-selected conversations require an explicit model switch or a new
conversation. No browser screenshot suite in this increment. No commit/push/merge.

## 2026-11-12 — Ubuntu migration in progress, NOT cut over

Owner explicitly authorized preparing the LAN Ubuntu host, moving the container
stack and SSH tunnel, using deployment credentials already in private .env.
Branch/pending changes preserved. SSH first-use host key enrolled locally;
subsequent connections reject changed keys. No credential values printed.

Verified target: Ubuntu 26.04.1 LTS amd64, four CPUs, about 7.3 GiB usable RAM,
4 GiB swap, 86 GiB free initially. This differs from the owner's stated 16 GB;
reported the actual figure. Docker Engine/Compose installed from Docker's signed
Ubuntu repository. Engine active and Compose 5.7.1 verified. Host provisioning
and two systemd unit templates added under infra/local; units installed/verified
on target but production stack/tunnel not enabled yet.

New Ed25519 key generated ON the Ubuntu host; only its public key was authorized
on the existing Oracle user with restricted remote loopback forwards and no
interactive shell. Original Windows private key was NOT copied. Strict known-host
verification retained. Temporary reverse forwards 29080/29089 are exclusively
127.0.0.1 on Oracle and verified. Public 19080/19089 remain on Windows.

Packaging: exact running images exported (3,562,356,175 compressed bytes), source
plus model assets packaged privately. Slow Wi-Fi stalled bulk transfer; stopped
only the identified source-uploader process, split essential code into a smaller
18,320,722-byte archive, and successfully transferred/hash-verified/extracted it
with private runtime .env (0600). Excluded DEPLOY_SERVER credentials from runtime
.env; retained application secrets via SFTP without displaying them. Enabled-state
of the existing bridge is preserved; current gateway has browser bridge disabled,
while bridge container and its session volume are retained. Optional Compose
variable WHATSAPP_BRIDGE_ENABLED added with previous default true unchanged.

Resume-capable encrypted image streams are still uploading in background;
private progress in artifacts/server-migration/preparation-status.json. Background
preparation will verify/load exact images, generate a local image-ID override,
and transfer optional model assets. It DOES NOT stop production, copy the final
live database, start the public target stack or switch the public tunnel.

Three source volumes identified for preservation: portal_data (~2.2 MiB),
litellm_audit and whatsapp_sessions (~160 MiB). They have NOT yet been cut over.
No reset, volume deletion, public DNS change or stop of the working Windows portal.
Wi-Fi had power-save enabled with weak signal (-74 to -77 dBm); runtime power-save
was disabled without changing credentials/address. Owner asked whether all apps
are containerized and explicitly authorized continuing. On later status request,
reported incomplete migration accurately and continued preparation. Asked whether
Ethernet can be connected to improve transfer speed; work does not depend on reply.

Remaining: complete artifact transfer/load; quiesce writers and bridge, consistent
backup/copy/restore and integrity/count verification; start target and validate
real authentication/Groq/history; final tunnel cutover/public HTTPS verification;
then disable old autostart/stop only source IA-Portal containers, preserving data.
No commit, push or merge. See infra/local/UBUNTU_SERVER.md.

## 2026-11-12 — Wi-Fi-only migration continuation

Owner confirmed Ethernet unavailable; continued with Wi-Fi, no further hardware
approval required. Image upload progressed beyond 43%; not yet a completed
migration. Docker/Compose, source/private runtime configuration and staged tunnel
remain verified. Portal still served by Windows while image transfer continues.

Added infra/local/volume_snapshot.py for offline snapshots with SQLite backup
(including committed WAL), file hashes, database integrity/table counts, numeric
ownership preservation for the node-user browser profile, rejection of nonempty
restore destinations and tar traversal protection. Ephemeral browser locks are
omitted; original source files are not deleted. Three synthetic automated tests
passed (`python -m unittest discover -s tests -p test_volume_snapshot.py -v`).
Initial tests exposed unclosed SQLite handles and WAL-sidecar inventory effects;
fixed via explicit connection closing and normalized SQLite backup/ephemeral
sidecar handling. No application schema/authentication changes.

An owner-authorized private migration worker now waits for verified images,
pre-copies the inactive browser profile, and will perform a short final quiesce,
consistent data/audit snapshots, empty-volume restore, exact table-count checks,
server startup, authenticated real Groq/Trino/stream/title validation, staged
loopback validation, and only then final tunnel promotion. Existing Windows
runner/task identity is verified before disabling/stopping. Source containers
are retained, never removed; failures before promotion restart source writers.
After promotion the destination remains authoritative rather than blindly
rolling back to an older DB. Windows source containers are stopped only after
public HTTPS/health verification. Optional model assets continue transferring.

Private progress: artifacts/server-migration/migration-status.json and
preparation-status.json. Remote smoke/count-check Python/shell syntax validated;
this is not a claim that target end-to-end checks have already executed. A
short-lived Windows idle-sleep inhibition request is held only for the migration
and released on completion/error, without changing persistent power settings.
No application outside containers was introduced on Ubuntu. No commit/push/merge.
