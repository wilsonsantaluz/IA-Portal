# Reformulação visual — 31/10/2026

Branch: `feature/ui-redesign`. Referência: `docs/design/reference.png` e prancha enviada no chat. Não há SVG revisado da marca em `docs/design/`; monograma e favicon continuam provisórios. Nenhuma identidade final foi inventada.

Escopo autorizado nesta revisão: acabamento geral do shell e telas existentes, com foco no dashboard. Sem novas rotas, operações administrativas, dependências de produção ou alterações nos contratos de API.

## Alterações

- `front/src/theme/portalTheme.ts` e `index.css`: tokens compartilhados de sombra, cores e superfícies para ambos os temas, acabamento dos botões/campos, entrada escalonada e respeito a movimento reduzido.
- `layout/DashboardLayout.tsx`, `HeaderBar.tsx`, `Sidebar.tsx`: cabeçalho translúcido, identificação do usuário, navegação com gradiente discreto, transição da sidebar e fundo suave.
- `components/DashboardCard.tsx`, `pages/AdminDashboardPage.tsx`: cards responsivos sem ícones flutuantes ou largura mínima rígida; métricas reais, carregamento, erro com nova tentativa e estado vazio. Gráficos de área e barras usam cores do tema e escala de 0 a 10. Nenhum percentual de crescimento ou tendência foi inventado.
- `components/ChatWelcome.tsx`, `ChatPage/ChatInput.tsx`, `ChatPage/MessageList.tsx`, `ChatPage/MessageItem.tsx`, `pages/ChatPage.tsx` e `markdown.css`: sugestões genéricas editáveis, campo com foco destacado, labels acessíveis, coluna de leitura e acabamento das mensagens. Fluxo de envio, upload, histórico e avaliação preservado.
- `pages/LoginPage.tsx`: composição responsiva com apresentação do produto e formulário existente.
- `pages/KnowledgeBasePage.tsx`: título, espaçamento e superfície alinhados ao sistema visual; editor, tabela e operações existentes preservados.
- `tests/ui/phase-a.cjs`: diretório de capturas configurável e seletores compatíveis com campos obrigatórios. `tests/ui/phase-b.cjs`: validação específica da revisão.

## Validação

| Comando/verificação | Resultado |
| --- | --- |
| `node front/node_modules/typescript/bin/tsc --noEmit -p front/tsconfig.json` | Passou |
| `docker build -t ia-portal-portal-front C:/source/IA-Portal/front` | Passou, incluindo `yarn typecheck && yarn build` |
| `node tests/ui/phase-a.cjs` com variáveis descritas em `tests/ui/README.md` | 11 grupos passaram; nenhum erro JavaScript |
| `node tests/ui/phase-b.cjs` nas mesmas condições | 7 grupos passaram; nenhum erro JavaScript |
| Axe-core no shell e no conteúdo principal de chat/dashboard | Nenhuma violação nas verificações WCAG A/AA executadas |
| Dashboard/chat/base em 320 e 390 px, ambos os temas | Sem rolagem horizontal da página ou do conteúdo principal |
| Falha HTTP 503 e nova tentativa no dashboard | Erro explícito; recuperação pela API real |
| Gráficos com fixtures exclusivas do navegador de teste | Ambos renderizaram nos dois temas |
| Movimento reduzido | Animação de entrada suprimida |
| `git diff --check` | Passou |

Testes usaram gateway real com conta sintética e SQLite temporário em tmpfs, isolados do ambiente diário. Capturas em `screenshots/redesign/` mostram banco vazio real desse ambiente. Fixtures de gráficos existem apenas no teste de navegador, sem inserir registros ou alterar a aplicação. Segredos temporários não foram versionados.

Capturas principais: `dashboard-light.png`, `dashboard-dark.png`, `chat-light.png`, `chat-dark.png`, `login-light.png` e versões mobile. Resultados detalhados: `checks.json` e `phase-b-checks.json` no mesmo diretório.

## Aplicação local e limites

Executado `docker compose up -d --no-deps --no-build portal-front`. Disponível em **http://127.0.0.1:9080**; usar Ctrl+F5 para recarregar assets. HTTP 200 confirmado. Gateway, Qdrant, ML e OCR preservaram IDs e horários de inicialização. Qdrant respondeu `all shards are ready` após a atualização.

O build conserva o aviso preexistente de chunks maiores que 500 kB, incluindo Mermaid. Não foram executados builds/testes .NET nesta revisão exclusivamente de frontend. Não foram feitas chamadas ao provedor externo de IA, testes de OCR, avaliação da qualidade do RAG, reindexação ou alterações de banco. Fluxos de streaming, upload e edição de conhecimento não foram exercitados de ponta a ponta nesta revisão. Os testes automatizados de acessibilidade não constituem auditoria completa.

A inspeção confirmou atributos de autorização comentados no controller de métricas existente. Nenhuma tela privilegiada nem endpoint adicional foi exposto; a revisão de autorização de backend permanece pendente e deve preceder a fase administrativa.

Sem commit, merge, push ou publicação externa. Revisão visual pelo usuário permanece necessária, especialmente da marca quando o SVG exato estiver disponível.

Durante a revisão final surgiu a pasta não rastreada `ia-portal-brand/`, ausente no estado inicial do Git. Foi preservada sem modificações ou incorporação. Seu README descreve uma interpretação vetorial, e a geometria dos SVGs difere da marca da prancha. A aplicação continua com o monograma provisório; é necessário confirmar o SVG exato antes de tratá-lo como marca final.

## Integração dos SVGs fornecidos — 31/10/2026

Após o usuário identificar explicitamente `ia-portal-brand/` como fonte dos arquivos, os três SVGs foram copiados sem modificação para `front/public/brand/`. SHA-256 de cada cópia foi comparado com o original e coincide. O componente `Brand.tsx` agora usa as versões clara e escura conforme o tema, e o símbolo na navegação recolhida. Login e navegação compartilham esse componente. `front/index.html` aponta o favicon para o símbolo fornecido. A pasta original permanece preservada. Isso substitui a pendência anterior de integrar a marca; não representa uma reprodução fiel da prancha PNG, como informa o README do próprio pacote.

Validação da integração: TypeScript e build Docker (`yarn typecheck && yarn build`) passaram, mantendo apenas o aviso preexistente de bundle grande. Atualizado somente `portal-front` com `docker compose up -d --no-deps --no-build portal-front`. Navegador confirmou logo clara/escura carregada, exibição em 390 px sem overflow após estabilizar o breakpoint, favicon e os três SVGs com HTTP 200 e tipo `image/svg+xml`, sem erros JavaScript. Capturas: `screenshots/redesign/brand-login-light.png` e `brand-login-dark.png`. Não foi repetida a suíte completa da revisão anterior para esta troca de assets. Sem alterações de backend ou testes .NET nesta integração.

## Tema escuro por padrão — 31/10/2026

Por solicitação posterior do usuário, o padrão passou a ser escuro, substituindo a orientação inicial de tema claro. `PortalThemeProvider.tsx` e `front/index.html` usam escuro quando não há preferência válida ou o armazenamento está indisponível. Uma preferência explícita `light` continua respeitada. O HTML já aplica o fundo e color-scheme antes do React. O teste da Fase A foi ajustado para esperar escuro inicialmente e então selecionar claro para as verificações existentes. Alterações anteriores preservadas; nenhuma alteração de backend.
