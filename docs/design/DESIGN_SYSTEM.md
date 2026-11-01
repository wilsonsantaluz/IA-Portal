# Design system — IA-Portal

## Fonte visual

- Estado atual: **escuro por padrão**, conforme solicitação posterior do proprietário, com preferência clara/escura preservada. Não reverter o comportamento ao trabalhar nas próximas fases.

- Conceito aprovado: **Enterprise Light com modo escuro**, logo geométrica IA-Portal em azul/ciano/violeta.
- Arquivos de referência deste pacote: `ia-portal-reference.png` (conceito) e `ia-portal-design-system-reference.png` (painel de componentes). São imagens de orientação, **não especificações pixel-perfect**.
- SVGs em `brand/`: `ia-portal-logo.svg`, `ia-portal-logo-dark.svg`, `ia-portal-mark.svg`. Verificar o resultado visual antes de substituir a marca já implantada.
- A captura local mais recente do usuário mostra uma interface já refatorada; preservar sua estrutura e melhorar incrementalmente, sem refazer o visual aprovado.

## Tokens iniciais (consistentes com AGENTS.md)

| Token | Claro | Escuro |
| --- | --- | --- |
| Fundo | `#F5F8FC` | `#0B1220` |
| Sidebar | `#FFFFFF` | `#101B2E` |
| Card | `#FFFFFF` | `#17243A` |
| Texto | `#14243D` | `#F1F5FC` |
| Texto secundário | `#64748B` | `#A5B4C8` |
| Borda | `#E4EAF2` | `#293B55` |
| Primária | `#1769E0` | `#4D93FF` |
| Ciano | `#06B6D4` | `#22D3EE` |
| Violeta | `#6847E8` | `#9878FF` |

São tokens de implementação propostos, não amostras medidas da imagem. Se o frontend já usa tokens equivalentes, preferir os existentes e documentar divergências.

## Componentes

- **AppShell**: sidebar recolhível, topbar, título, área de conteúdo responsiva.
- **ThemeToggle**: claro/escuro, persistência e foco acessível.
- **ConversationList**: grupos por período, título, seleção, menu contextual, busca e estados vazios.
- **ChatThread**: bolhas/itens de usuário e assistente, estado de processamento, erro e retry.
- **ChatComposer**: textarea acessível, envio, anexos somente se funcionais, indicador de modelo.
- **ModelSelector**: modelos habilitados e status, sem segredos no frontend.
- **UsageSummary**: dados reais, unidades e distinção entre medido/estimado.
- **AdminSettings**: provedores, status de configuração, teste de conexão e permissão administrativa.
- **Feedback**: loading, skeleton, toast, confirmação destrutiva, erro e empty state.

## Padrões de interface

- Português brasileiro nos rótulos; fonte sans-serif legível; escala de espaçamento consistente.
- Respeitar contraste, navegação por teclado, labels, responsividade e `prefers-reduced-motion`.
- Não inventar conversas recentes, usuários, custos ou dados de dashboard.
- Não esconder ações críticas apenas no frontend: autorização é responsabilidade do backend.
