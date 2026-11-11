# Log de ações

## Uso

A tela **Log de ações**, rota `/audit`, permite consultar registros por período,
nome/ID de usuário, ação, recurso e resultado. A pesquisa livre também localiza
operação, rota, referência da requisição e origem da conexão. Paginação por cursor,
até 100 registros por resposta (interface solicita 50), período máximo de 366 dias
por consulta. Datas persistidas em UTC; interface exibe o horário local.

O acesso exige `audit.read`, verificado no backend e no frontend. Owner/Admin e o
grupo legado `admin` recebem a permissão pelo catálogo existente. Grupos comuns,
inclusive Atlas — Colaboração, não a recebem. Um administrador autorizado pode
delegá-la usando as permissões existentes.

Na organização selecionada, os registros são limitados àquela organização.
Somente a administração da instalação, pela organização 1 com `audit.read`, tem
visão global e vê tentativas anônimas de login/acessos recusados sem identidade.
O filtro não aceita um ID de organização para ampliar o escopo. Selecionar a
organização da instalação no controle existente para acessar a visão global.

## Cobertura e dados

Middleware registra cada requisição concluída sob `/api`, incluindo leituras,
login correto/incorreto, cadastro, alterações, saída e acessos recusados, além de
requisições de webhooks. Não registra assets, health checks fora de `/api`, cliques
locais sem requisição (por exemplo, tema/menu) ou cada tarefa interna de workers.
Uma consulta aos próprios logs também gera registro, visível em nova pesquisa.
As operações anteriores à instalação desta versão não são reconstruídas.

Cada registro contém data/hora de início, usuário/organização validados, nome do
usuário, ação, área, operação, método, template da rota, recurso quando conhecido,
resultado, HTTP status, duração, referência aleatória e IP da conexão. Nome vem da
identidade assinada ou do login validado; ID é a identidade revalidada no servidor.
Login recusado não é atribuído à pessoa cujo e-mail foi digitado. Header/body de
cliente não escolhem ator ou organização. IP representa a conexão observada pelo
gateway; pode ser o proxy, e não o dispositivo final. Não confiar em headers
arbitrários para transformá-lo em IP do visitante.

Não são persistidos request/response bodies, parâmetros de query, senhas, hashes
de senha, tokens, chaves, perguntas, mensagens, nomes/conteúdo de arquivos nem
mensagens de exceção. Rota desconhecida vira `/api/*`, sem caminho bruto. IDs em
rotas são capturados apenas para chaves conhecidas e valores numéricos/UUID;
documentos salvos e sessões criadas têm ID explícito definido pelo servidor.

O resultado considera status HTTP, cancelamento e exceções. O emissor SSE marca
falhas de chat, inclusive quando o stream já iniciou com HTTP 200. O teste de
conectividade de IA também marca diagnóstico negativo como falha, sem gravar a
resposta do provedor. Outras respostas HTTP 2xx de negócio são classificadas por
status, sem inspecionar seu conteúdo. Cadastro aceito significa solicitação
recebida; não é prova de criação de conta ou entrega de e-mail.

O frontend envia `POST /api/auth/logout` autenticado com `keepalive` antes de
limpar a sessão local. A saída local continua disponível offline, mas sem rede
esse evento pode não chegar. O endpoint não acrescenta revogação de JWT; mantém
o contrato anterior de saída/expiração e apenas permite registrar a ação.

## Persistência e operação

Migração **011_ActionAudit.sql** acrescenta somente tabela/índices/triggers, com
backup consistente prévio e checksum no ledger. Inicialização valida colunas,
índices e triggers. Migrações anteriores preservadas. Triggers impedem UPDATE e
DELETE de registros; não existe endpoint de alteração/exclusão de logs.

Escrita usa INSERT parametrizado num contexto independente da ação, para
registrar também operações recusadas, transações revertidas e cancelamentos.
Esse INSERT é uma exceção estreita de infraestrutura ao escopo de tenant; a leitura
normal mantém o filtro. Identidade/organização vêm somente da autenticação
validada. Não há identificação por e-mail digitado em login incorreto.

Falha de gravação não desfaz uma ação já concluída: há timeout de cinco segundos
e diagnóstico sanitizado `ActionAuditAppendFailed`, sem dados do evento. Isso é
registro local de melhor esforço, sem fila durável, réplica externa ou garantia
de entrega quando o banco/disco falha. Proprietário do arquivo SQLite ainda pode
alterar o schema; os triggers não equivalem a prova criptográfica contra adulteração.

Sem exclusão automática ou retenção implícita. Monitorar o crescimento do banco
e dos backups, sobretudo com acesso público e polling de sessão. Mudança de
retenção/exportação externa exige política específica. Os logs contêm metadados
pessoais de acesso: restringir também arquivos, backups e permissões de grupos.

Rollback: parar o novo gateway e executar a imagem anterior com uma cópia completa
do backup pré-v11 em outro caminho, reconciliando gravações posteriores. A imagem
anterior não aceita ledger 11; só trocar o binário não é rollback de schema.
Não apagar o banco/volume nem remover tabelas da instalação viva.

## Validação reproduzível

- `dotnet run --project tests/Audit`: bancos temporários, v10→v11, preservação,
  backup/restauração, rollback de DDL, append-only, escrita independente, identidades,
  falhas HTTP/SSE, dados sensíveis ausentes, filtros, limites e cursor.
- `python scripts/smoke_audit.py`: gateway HTTP real e banco temporário, login,
  saída, API administrativa, negações, escopos e injeção de identidade ignorada.
- `node tests/ui/audit-login.cjs`: Playwright/Chromium com API fixtures; pesquisa,
  paginação, estado vazio, proteção de rota/menu, logout, ambos os temas e mobile.
  Também verifica o login renovado, senha visível/oculta, erros e movimento reduzido.
  Configurar `IA_PLAYWRIGHT_MODULE` e `IA_CHROME_PATH` quando necessário.

Nenhum desses testes precisa de SMTP, serviço externo de IA ou conta real.
Capturas sintéticas ficam em `artifacts/audit-login-ui/`, fora do Git. Consultar
o diário para os resultados efetivamente executados e a atualização local.
