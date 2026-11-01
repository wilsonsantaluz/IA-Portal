# Usuários, grupos e permissões

Implementação na branch `feature/user-group-permissions`. As tabelas existentes `Users`, `Roles`, `UserRoles`, `Permissions` e `RolePermissions` foram reaproveitadas; os papéis são apresentados como grupos. Nenhuma tabela, coluna, senha ou documento existente foi substituído.

## Uso

1. Acesse http://127.0.0.1:9080 e entre novamente com a conta administrativa existente.
2. Em **Grupos e permissões**, crie um grupo e marque as permissões por tela.
3. Em **Usuários**, cadastre nome, e-mail, senha inicial e um ou mais grupos.
4. Para manutenção, use o botão de edição: altere nome/e-mail, grupos, status ou informe uma nova senha. Deixe a senha em branco para mantê-la.

O cadastro é administrativo, sem inscrição pública. Contas são desativadas em vez de excluídas fisicamente. Grupos sem membros podem ser excluídos com confirmação. Busca, carregamento, erros e sucesso são apresentados nas telas; tabelas têm rolagem própria em telas estreitas.

| Chave | Tela e capacidade |
| --- | --- |
| `chat.access` | Chat, histórico da própria conta, avaliação e análise de documentos |
| `dashboard.view` | Métricas e avaliações do dashboard |
| `kb.view` | Consulta da base de conhecimento |
| `kb.manage` | Cadastro, edição, ativação e reindexação da base; inclui consulta |
| `access.manage` | Administração de usuários, senhas, grupos e permissões |

As permissões são a união dos grupos. Uma conta sem grupo/permissão consegue autenticar, mas recebe um estado explícito sem telas autorizadas. Grupos não recebem permissões automaticamente. Usuários sem edição da base não veem os comandos de manutenção e recebem HTTP 403 nas chamadas de escrita.

Usuários e grupos compartilham uma única permissão administrativa porque ambas as telas permitem conceder acesso completo ao sistema. Conceda `access.manage` apenas a administradores de confiança. Não há permissões distintas de visualizar/criar/editar/excluir usuários nesta versão, nem restrição por documento, seção ou linha.

## Garantias implementadas

- APIs administrativas exigem `access.manage`; as APIs de conteúdo exigem a permissão da área. A política fallback exige autenticação e administração para endpoints sem uma política específica. Somente login é explicitamente anônimo.
- JWTs são assinados e validados como antes. A cada requisição, o servidor verifica se a conta está ativa e consulta seus grupos/permissões no SQLite, substituindo os claims antigos. Alterar o armazenamento do navegador ou reutilizar claims antigos não concede acesso.
- Alteração de senha invalida os JWTs emitidos com a senha anterior. Um HMAC do hash BCrypt, usando a chave de assinatura do servidor, identifica a versão da credencial sem expor o hash. O hash nunca é devolvido nos DTOs de usuários.
- Tokens emitidos antes desta versão não possuem essa versão da credencial e exigem novo login. A desativação bloqueia login e requisições enquanto a conta estiver inativa; reativação sem troca de senha não é uma revogação permanente de tokens antigos ainda válidos.
- E-mails são normalizados nas novas gravações e comparados sem diferenciar maiúsculas. Grupos e e-mails duplicados, referências inválidas, senhas curtas e senhas com mais de 72 bytes são recusados. Registros legados não são renomeados automaticamente.
- O grupo reservado `admin` mantém todas as permissões e não pode ser alterado/excluído pelas APIs. A inicialização adiciona o catálogo de forma idempotente, preservando grupos/chaves existentes e sem conceder privilégios a outros grupos.
- Transações de escrita SQLite protegem atualizações de vínculos e a verificação de pelo menos um administrador ativo. Não se permite desativar a própria conta ou retirar o próprio grupo `admin`. Grupos com membros não podem ser excluídos.
- O usuário da conversa e das avaliações vem do JWT, ignorando identidades enviadas pelo cliente. Leituras de histórico são filtradas pelo proprietário; tentativas de reutilizar uma sessão alheia não podem gravar mensagens nela.
- O frontend consulta `/api/auth/me` ao carregar, ao recuperar foco e a cada 30 segundos enquanto visível. Alterações podem levar até esse intervalo para aparecer no menu; a API aplica a próxima requisição imediatamente. Rotas diretas têm bloqueio adicional no cliente.

O padrão segue as [políticas de autorização do ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-8.0) e o evento [OnTokenValidated](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.jwtbearer.jwtbearerevents.ontokenvalidated?view=aspnetcore-8.0).

## Compatibilidade

Nenhuma evolução de esquema foi necessária. A inicialização continua sem apagar/recriar banco. Instalações existentes devem possuir um administrador ativo vinculado ao grupo exato `admin`, criado pelo bootstrap original; não há recuperação automática que promova contas sem autorização.

Históricos antigos usavam identificadores fornecidos pelo cliente, que não comprovam o proprietário. Permanecem no SQLite, mas não são reassociados automaticamente. Novas conversas usam identidade do JWT e uma chave de sessão local por conta. O conteúdo da base e os vetores permanecem preservados.

As antigas operações avulsas de exclusão física de usuário, atribuição de papel e criação/exclusão de chave de permissão foram retiradas. Vínculos agora são enviados no `PUT /api/users/{id}` e permissões no `PUT /api/roles/{id}`, com validação e transação. Não havia telas ativas usando as operações retiradas. Clientes externos dessas rotas precisam ser adaptados.

## APIs principais

| Método/rota | Função |
| --- | --- |
| `GET /api/auth/me` | Conta e permissões atuais; qualquer conta autenticada |
| `GET /api/users` e `GET /api/users/{id}` | DTOs sem credenciais |
| `POST /api/users` | Nome, e-mail, senha e `groupIds` |
| `PUT /api/users/{id}` | Nome, e-mail, `isActive`, `groupIds` e senha opcional |
| `GET /api/roles` | Grupos, membros e permissões |
| `POST /api/roles` e `PUT /api/roles/{id}` | `name` e lista `permissions` |
| `DELETE /api/roles/{id}` | Excluir grupo vazio não reservado |
| `GET /api/permissions` | Catálogo fixo de permissões conhecidas |

## Validação executada

| Comando | Resultado |
| --- | --- |
| `dotnet build ai-gateway/AiGateway.csproj --no-restore` | Passou; 14 avisos C# preexistentes |
| `node front/node_modules/typescript/bin/tsc --noEmit -p front/tsconfig.json` | Passou |
| `docker build -t ia-portal-access-api C:/source/IA-Portal/ai-gateway` | Passou; publish Release |
| `docker build -t ia-portal-access-front C:/source/IA-Portal/front` | Passou; typecheck e Vite |
| `node tests/access/api.cjs` | 5 grupos passaram; API real, banco descartável |
| `node tests/access/ui.cjs` | 5 grupos passaram; criação/manutenção real, menus/rotas restritos, acessibilidade claro/escuro e mobile 320/390 px |
| `dotnet run --project tests/access/SeedSmoke` | Passou; banco em memória existente preservado e inicialização repetida idempotente |
| `python scripts/smoke_local.py` | Passou; SQLite, login, KB, chat, avaliações, OCR simulado e persistência após reinício |
| `dotnet run --project tests/RagSmoke -- C:/source/IA-Portal` | Passou; ONNX/Qdrant/reranking reais, LLM local simulado e isolamento entre contas |
| `node tests/ui/phase-a.cjs` | 11 grupos de regressão passaram |

As suítes de navegador usam Playwright, Chrome e axe-core externos ao projeto, instalados em pasta temporária, e conta sintética. As capturas ficam em `docs/design/screenshots/access/`. O teste de acesso não usa mocks das APIs administrativas. O teste de RAG usa um Qdrant vazio exclusivo na porta 16333. Nenhum registro de teste foi inserido no banco diário, nem foi feita chamada ao provedor de IA real.

Durante a verificação foram corrigidos um erro de materialização de coleção no EF e contraste insuficiente em cabeçalhos/status no modo claro. O teste local de inicialização passou a verificar Swagger, pois métricas agora exigem autenticação. O teste RAG usa caminho absoluto para evitar a resolução relativa ao diretório do projeto de teste.

Os builds conservam avisos preexistentes de C#, bundle Vite acima de 500 kB e dependências pares. A análise axe cobre as áreas executadas, não é uma auditoria completa. Não foram feitos teste de carga/concorrência, envio de e-mail, recuperação de senha por e-mail, MFA ou inferência do provedor real. Esses recursos não fazem parte desta entrega.

## Aplicação local

Imagens validadas foram etiquetadas como `ia-portal-ai-gateway` e `ia-portal-portal-front`, e aplicadas com `docker compose up -d --no-deps --no-build ai-gateway portal-front`. Volumes preservados. Qdrant, ML e OCR mantiveram IDs e horários de início. Confirmados frontend HTTP 200 e APIs protegidas HTTP 401 sem sessão pelo proxy Nginx. Qdrant continuou pronto.

Alterações concentradas nos controllers de autenticação/administração, políticas em `ai-gateway/Security/PortalAccess.cs`, inicialização e repositório de chat; no frontend, `AccessManagementPage.tsx`, contexto de autenticação, guardas, rotas, navegação e comandos condicionados por permissão. Testes e guia de reprodução em `tests/access/`.

Nenhum commit, merge, push ou publicação externa foi realizado. A aplicação continua sendo um protótipo; esta entrega não substitui as demais revisões descritas em `REVISAO_PUBLICACAO.md`.
