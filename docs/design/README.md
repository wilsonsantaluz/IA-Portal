# IA-Portal — Fase A

Esta entrega limita-se à identidade tipográfica provisória, tokens de tema, layout principal, cabeçalho, navegação responsiva e alternância claro/escuro. O redesenho detalhado das páginas e a administração pertencem às fases seguintes.

## Referência e marca

`reference.png` é uma cópia sem alterações da prancha fornecida na raiz do repositório. Os tokens iniciais vêm da tabela do `AGENTS.md`; não foram apresentados como cores extraídas da imagem.

Ainda falta o SVG revisado da marca. O monograma tipográfico “IA” no layout e no favicon é provisório, não uma reprodução da logo aprovada. O cabeçalho e a sidebar estão preparados para receber o SVG quando ele for disponibilizado.

## Implementação

- `front/src/theme/portalTheme.ts`: tokens semânticos compartilhados, superfícies, texto, bordas, ações e foco de teclado.
- `front/src/theme/PortalThemeProvider.tsx`: claro por padrão, preferência persistida em `theme-mode`, sincronização entre abas e compatibilidade com o atributo `body[data-theme]` existente.
- `front/index.html`: preferência aplicada antes de carregar a aplicação, reduzindo o flash de tema incorreto.
- `front/src/layout/`: sidebar de 248 px, recolhida para 80 px; drawer em telas menores que 900 px; cabeçalho, conteúdo rolável e link de salto para teclado.
- `front/src/components/Brand.tsx` e `ThemeToggle.tsx`: identidade provisória e controle acessível de tema.
- O nome exibido é IA-Portal. As rotas existentes de chat, dashboard e conhecimento continuam em uso. Nenhum link de administração novo é exposto nesta fase.

Não foram adicionadas estatísticas fictícias, fontes externas ou outra biblioteca de UI. As fontes usam Inter quando disponível, com fallback para fontes do sistema. As preferências já gravadas do usuário são respeitadas; somente uma sessão sem preferência começa no tema claro.

## Limites de escopo

Os rascunhos de fases posteriores foram preservados em backup local fora do repositório e retirados desta entrega. O backend e os contratos de autenticação, chat e dados continuam no estado anterior ao redesign, incluindo as correções de RAG já autorizadas.

Os ajustes no DataGrid v5, na assinatura do renderer de código do React Markdown e na propriedade não utilizada de `MessageItem` são correções de compatibilidade necessárias à checagem TypeScript. Não constituem um redesenho dessas telas.

## Revisão visual

As capturas em `screenshots/` são produzidas pelo teste de navegador com conta sintética, gateway real e SQLite temporário. Não contêm dados da instalação de uso diário. O conteúdo das páginas ainda é o legado existente; a Fase A cobre o layout ao redor delas.

Veja [o roteiro do teste](../../tests/ui/README.md) e [o relatório de validação desta fase](PHASE_A_VALIDATION.md) para resultados e limitações.
