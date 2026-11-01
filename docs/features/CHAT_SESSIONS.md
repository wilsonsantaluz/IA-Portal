# Conversas, sessões e títulos

## Experiência desejada

- Botão **Nova conversa** cria um contexto separado para o usuário autenticado.
- Conversas recentes agrupadas visualmente: Hoje, Ontem, Últimos 7 dias, Anteriores (fuso do usuário para exibição; timestamps UTC persistidos).
- Ao selecionar uma conversa, carregar mensagens paginadas e permitir continuar o contexto.
- Buscar por título; renomear; excluir com confirmação; manter estado correto após reload.
- Mostrar provedor/modelo da conversa e da resposta quando houver troca explícita.
- Nunca mostrar dados de outro usuário nem por manipulação de UUID.

## Ciclo de vida

1. Criar conversa autenticada com título provisório (`Nova conversa`).
2. Enviar mensagem e registrar a requisição com idempotência quando possível.
3. Persistir resposta, status e timestamps; lidar com erro sem duplicar mensagem.
4. Após a primeira resposta bem-sucedida, gerar título curto (ideal 3–8 palavras) com chamada econômica e contexto mínimo.
5. Se geração de título falhar, usar trecho seguro da primeira pergunta; marcar origem `fallback`.
6. Se usuário renomear, marcar `manual`; nunca sobrescrever automaticamente.
7. Registrar consumo do título como `purpose=title`, separado do chat.

## API — contratos sugeridos (adaptar aos existentes)

| Operação | Método/rota ilustrativa | Regra |
| --- | --- | --- |
| Listar minhas conversas | `GET /api/conversations?cursor=...` | Somente usuário autenticado; paginação |
| Criar | `POST /api/conversations` | Dono obtido do JWT |
| Ler conversa | `GET /api/conversations/{id}` | 404/403 sem revelar dados de terceiros |
| Listar mensagens | `GET /api/conversations/{id}/messages` | Propriedade validada; paginação |
| Enviar | `POST /api/conversations/{id}/messages` | Validação, limites, histórico contextual |
| Renomear | `PATCH /api/conversations/{id}` | Marcar título manual |
| Excluir | `DELETE /api/conversations/{id}` | Autorização + semântica documentada |

**Não criar estes endpoints se já houver equivalentes**; preservar compatibilidade ou documentar migração do frontend.

## Testes mínimos

- Usuários A e B: A não lista, lê, altera, exclui nem envia mensagens na conversa de B.
- Histórico preservado após reiniciar API e frontend.
- Título gerado uma vez; título manual preservado; falha usa fallback.
- Paginação estável; ausência de mensagens duplicadas ao repetir requisição.
- Estado vazio, erro de rede e conversa excluída são tratados no frontend.

## Estado implementado — etapa 1 aprovada (01/11/2026)

A seção de experiência acima é o objetivo completo. Neste incremento foram entregues:

- Sessões UUID criadas no backend e vinculadas ao JWT, com título provisório, datas, versão, provedor/modelo efetivamente usados pelo caminho atual e exclusão lógica.
- Listar/buscar/selecionar/renomear/excluir conversas; recarregar ou entrar por outro navegador recupera a lista do servidor. O armazenamento local guarda apenas a última seleção por conta.
- Histórico de mensagens em páginas de 50, com botão para anteriores; lista de conversas em páginas de 30. Limite máximo de 100 por API. Mensagens ordenadas por horário + ID; conversas por última atividade + GUID. Busca é substring com a semântica de comparação SQLite atual, sem promessa de busca semântica/fuzzy.
- Períodos Hoje/Ontem/Últimos 7 dias/Anteriores no fuso do navegador; horários dos DTOs de sessão/mensagens em UTC. Drawer de conversas em telas menores, sem substituir a navegação principal.
- IDs reais do SQLite no histórico, estados de interação e confirmação de persistência antes de sinalizar sucesso no SSE. Abrir uma sessão não altera sua atividade.
- Pergunta preservada em falha/cancelamento; resposta só é salva como concluída após o provedor terminar e a transação persistir. Stream interrompido não é apresentado como sucesso. Leases vencidas são recuperadas no próximo envio explícito, sem job agendado.
- `requestId` explícito evita chamada duplicada em replay concluído. Payload diferente, requisição pendente ou requisição anterior não concluída com o mesmo ID retornam 409. O cliente não faz retry pago automático. IDs ausentes são gerados para compatibilidade, sem garantia de deduplicar reenvios sem ID.
- Propriedade validada antes de OCR/LLM/SSE e novamente nas escritas; ausência, exclusão e acesso alheio retornam 404. Até administradores precisam ser donos para acessar conteúdo de conversa. As rotas SQL legadas verificam dono quando um sessionId é fornecido; não foram transformadas em outro fluxo de persistência.
- Upload funcional preservado; texto extraído usado/persistido limitado a 50.000 caracteres nesta etapa. Para documentos maiores, a análise é parcial por esse limite. A entrada textual também tem limite de 50.000 caracteres; contexto adicional do chat, 12.000. Arquivos continuam limitados a 20 MB e formatos já aceitos.
- Timeout de geração/OCR de dois minutos, lease de três minutos; sem fallback silencioso de provedor. O contexto RAG mantém seus próprios limites/cancelamento e não passa a executar Trino.

### Compatibilidade de modelos neste incremento

O fluxo SSE continua usando o adapter DeepSeek e `deepseek-chat`. Os caminhos sem streaming/OCR preservam `DEEPSEEK_DEFAULT_MODEL` já suportado pelo adapter; a resposta registra esse modelo efetivo. Não há seletor de modelos nesta etapa. O modelo exibido no cabeçalho é o último utilizado pela sessão, não uma capacidade nova de configurar ou trocar provedores. Cada resposta guarda o identificador de modelo enviado pelo adapter, para não reescrever a identificação de respostas anteriores. A versão efetiva resolvida por aliases do provedor ainda não é capturada pela interface textual existente; isso será tratado com os resultados tipados nas etapas seguintes.

O trecho acima descreve a etapa 1. A etapa 2, abaixo, acrescenta títulos automáticos e consumo auxiliar. Estimativa de custo, consumo completo de chat, preferências de modelo, providers habilitáveis e tela administrativa de IA continuam pendentes.

### Etapa 2 — títulos automáticos (01/11/2026)

Após a primeira resposta persistida, o gateway gera um título usando a capacidade adicionada ao adapter DeepSeek existente. Aplica-se aos envios SSE, sem streaming e documentos. O modelo solicitado é o registrado na primeira resposta; não há troca silenciosa. O prompt contém somente até 1.000 elementos de texto da primeira pergunta (texto extraído, no caso de OCR), sem histórico, base de conhecimento nem resposta completa. Limite de saída de 64 tokens, título de até 80 elementos de texto e timeout de 10 segundos. O título final chega ao cabeçalho/lista pela recarga de metadados que o frontend já faz após o envio; não há novo contrato SSE obrigatório.

O fallback, um trecho da primeira pergunta, é salvo em transação junto com a reserva durável da tentativa em `LlmUsage`, antes de chamar o provedor. Resposta vazia, múltiplas linhas, falha, cancelamento ou timeout conservam esse fallback sem desfazer o chat. Atualização automática só ocorre enquanto a sessão pertence ao usuário, está visível e a origem permanece `fallback`; `manual` sempre vence, inclusive durante a chamada. Não muda a data de atividade apenas para atualizar o título. O PATCH continua limitado a 120 caracteres.

Uma única tentativa automática por sessão evita custos duplicados em replay, chamadas concorrentes e reinício. Não há retry pago automático nem geração retroativa em lote. Sessões provisórias da etapa 1 podem receber título quando uma nova interação é concluída, usando a primeira interação concluída já armazenada. Sessões `legacy` ou `manual` não são alteradas. Se o processo terminar depois da reserva, o fallback permanece e o consumo pode ficar `started`, com resultado desconhecido e tokens nulos; não é apresentado como sucesso nem custo zero. Se terminar entre salvar a resposta e reservar a tentativa, a próxima interação bem-sucedida pode iniciar essa reserva. Não foi criado worker em background.

`LlmUsage` é separado das mensagens e registra finalidade `title`, dono, sessão, mensagem da primeira resposta, provedor, modelo retornado quando disponível, horário UTC, duração e status. Tokens de entrada/saída/total/cache vêm da resposta da API; ausentes são NULL, com fonte `unavailable`. Fonte `reported` indica contadores recebidos, inclusive quando só parte deles está disponível. Cache é subconjunto da entrada e não é somado ao total. Não guarda prompt, conteúdo da resposta, corpo de erro, chaves ou estimativa inventada. Custos, métricas de chat, consultas/agregações e visualização de consumo serão tratados na etapa 3.

A migração `002_TitleUsage.sql` cria apenas essa tabela e índices, com checksum e backup `pre-chat-v2-*` para instalações v1. Instalações legadas fazem backup v1 antes da sequência; bancos vazios são criados com o modelo atual e recebem registros v1/v2. Scripts anteriores não foram reescritos. Backup/DDL/FKs e integridade seguem o mecanismo existente; sem recriação ou downgrade automático. O deploy continua exigindo um único gateway sem outros escritores durante a migração.

Testes adicionais: `dotnet run --project tests/ChatTitles` (migração v1→v2, rollback/checksum, geração, fallback, edição/exclusão durante chamada, isolamento e parsing de usage do adapter) e `python scripts/smoke_local.py` (título real via API com double HTTP, consumo separado, falha sem erro de chat e reinício). Provedores externos são simulados; não comprova disponibilidade nem qualidade de títulos do serviço remoto real.

### SQLite v1 e preservação

**Evolução posterior:** a etapa 3 contabiliza as chamadas de chat e análise de documentos, além do título, usando o mesmo `LlmUsage`. Consumo próprio/por conversa e agregados administrativos estão disponíveis por APIs protegidas, sem nova tela neste incremento. Preços são opcionais, explícitos e versionados; sem preço/tokens suficientes o custo permanece indisponível. A migração v3 é aditiva e preserva o histórico. Contratos, cobertura e limites em [USAGE_METERING.md](USAGE_METERING.md).

`ChatSchemaMigrator` substitui a chamada direta a EnsureCreated no inicializador. Para banco novo usa o esquema do modelo e registra v1. Para banco legado reconhecido, cria backup por `SqliteConnection.BackupDatabase`, verifica integridade e aplica `001_ChatSessions.sql` em transação com registro de checksum em `SchemaVersions`. SQL é normalizado quanto a quebras de linha antes do checksum. Esquema parcial/desconhecido, versão futura ou checksum divergente impedem startup, sem recriar banco.

O backup fica em `SQLITE_BACKUP_DIRECTORY`; padrão no Compose: `/app/data/backups`, no volume persistido. Fora do Docker, o padrão é a subpasta `backups` ao lado do banco. O destino do backup não usa pooling de conexão, para encerrar o arquivo físico ao terminar a cópia. Arquivos SQLite e backups continuam ignorados pelo Git.

Sessões antigas continuam com `OwnerUserId=NULL`. Não há promoção automática por coincidência com `UserId` numérico ou GUID. Conteúdo, IDs, usuários, senhas, grupos e base continuam no banco/backup. A associação de legados requer mapeamento revisado, que não foi implementado neste incremento.

A implantação/migração pressupõe uma única instância do gateway e ausência de outro escritor durante startup; não é um coordenador de migração para várias réplicas. Não há ferramenta de downgrade automático. Para rollback: parar gravações, guardar cópia consistente também do banco pós-migração, restaurar o backup por ferramenta SQLite validada em diretório privado, verificar integridade e iniciar a imagem anterior. Restaurar backup anterior elimina as gravações posteriores; não fazer isso como solução automática para falhas.

Exclusão é lógica: a conversa sai do histórico e das APIs, mas mensagens continuam retidas. Nenhum conteúdo é apagado por cascata. Política de retenção/purga física é uma etapa futura explícita.

### Testes e evidências

- `dotnet run --project tests/ChatSessions`: migração de fixture legada, restauração, idempotência, rollback de DDL, checksum, isolamento do repositório, paginação, versão de título, cancelamento/lease e exclusão lógica; stream bloqueado com handler de teste sem rede.
- `python scripts/smoke_local.py`: API real, mocks de LLM/OCR, isolamento A/B inclusive OCR/SQL antes de chamadas externas, replay sem nova chamada, falha sanitizada e persistência após reinício.
- `node tests/ChatSessions/ui.cjs`: APIs reais em instalação descartável; chat com provider HTTP simulado, duas conversas, busca/renomeação, recarga, navegador novo, paginação de 52 mensagens, exclusão com confirmação, axe-core e telas de 320/390 px nos dois temas.
- Regressão ONNX/Qdrant em `tests/RagSmoke`, com LLM simulado e Qdrant descartável.

Capturas com contas sintéticas: `docs/design/screenshots/sessions/`. Comandos exatos e resultados no diário; não houve chamada ao provedor real nem uso de registros diários nos testes. O roteiro de reprodução está em `tests/ChatSessions/README.md`.
