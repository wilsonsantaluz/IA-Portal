# Validação da Fase A — 31/10/2026

Branch: `feature/ui-redesign`. Escopo: identidade provisória, temas e layout principal, seguindo `AGENTS.md` e a prancha em `reference.png`.

## Resultado

O frontend foi construído e aplicado localmente em `http://127.0.0.1:9080`. Apenas o container `portal-front` foi recriado. Gateway, OCR, ML e Qdrant mantiveram seus IDs e horários de inicialização, verificados antes e depois da atualização. O RAG e as configurações do backend não foram alterados nesta fase.

O tema claro é o padrão para uma sessão sem preferência. A preferência existente em `theme-mode` é respeitada, persiste após recarregar e sincroniza entre abas. O HTML aplica a preferência antes do JavaScript da aplicação.

## Arquivos da entrega

Layout e identidade:

- `front/src/layout/DashboardLayout.tsx`, `HeaderBar.tsx`, `Sidebar.tsx`, `PrivateLayout.tsx`.
- `front/src/components/Brand.tsx`, `ThemeToggle.tsx`.
- `front/src/theme/portalTheme.ts`, `PortalThemeProvider.tsx`, `lightTheme.ts`, `darkTheme.ts`, `theme.ts`.
- `front/src/index.css`, `main.tsx`, `App.tsx`, `front/index.html`, `front/public/portal.svg`.
- `front/src/pages/LoginPage.tsx`: somente o nome exibido, IA-Portal.

Build e compatibilidade:

- `front/Dockerfile`: Node 22 e checagem TypeScript antes do bundle.
- `front/package.json`, `front/tsconfig.json`: comando `typecheck` e configuração estrita.
- `MessageList.tsx`: removida uma prop não utilizada e não declarada no componente destinatário.
- `MarkdownRenderer.tsx`: compatibilidade com os tipos do React Markdown instalado, mantendo blocos e código inline.
- `KnowledgeBasePage.tsx`: tipos das colunas, API de paginação/toolbar compatível com DataGrid v5 e tratamento de campos opcionais no filtro. O editor e os endpoints existentes foram mantidos.

Evidências e reprodução: `tests/ui/phase-a.cjs`, `tests/ui/README.md`, esta documentação, `reference.png` e `screenshots/`.

## Comandos e resultados

| Verificação | Resultado |
| --- | --- |
| `node.exe front/node_modules/typescript/bin/tsc --noEmit -p front/tsconfig.json` | Passou sem erros |
| Build Docker de `front/` | Passou; executou `yarn typecheck && yarn build`, com Vite 7.3.0 |
| Build da imagem do gateway no estado anterior ao redesign | Passou; usado para o ambiente de teste isolado, sem alteração de API |
| `node tests/ui/phase-a.cjs` | Passou: 11 grupos de verificações; nenhum erro JavaScript capturado |
| Axe-core, no cabeçalho, navegação e link de salto cobertos pelo teste | Nenhuma violação reportada nas verificações executadas |
| Instalação local após atualizar o frontend | HTML e assets HTTP 200; proxy respondeu HTTP 405 esperado para GET na rota POST de login |
| Comparação da referência visual | Cópia binariamente idêntica ao PNG fornecido na raiz |

Os builds foram executados via `docker.exe build` com os caminhos Windows absolutos de cada contexto, a partir do WSL. O teste de navegador usou Playwright e o Chrome instalado no Windows, com perfil isolado. Dependências de teste foram instaladas na pasta temporária do sistema, sem alterar dependências de produção.

O build conserva o aviso de chunks maiores que 500 kB, proveniente do bundle existente, incluindo Mermaid. A otimização do bundle está fora da Fase A. Os avisos C# preexistentes não foram tratados nesta entrega.

## O que o navegador verificou

1. Redirecionamento de uma rota protegida para login; senha sintética incorreta recebe 401 e a correta permite entrar.
2. Claro por padrão e navegação desktop com indicação da rota ativa.
3. Escuro, persistência após recarregar e sincronização entre duas abas.
4. Sidebar recolhida de 248 para 80 px, mantendo links acessíveis.
5. Navegação nas telas reais de chat, dashboard e conhecimento.
6. Menu mobile funcional, fechamento após navegar e ambos os temas em 390 px.
7. Ausência de rolagem horizontal da página em 390 e 320 px.
8. Link de salto para o conteúdo e foco visível de teclado no cabeçalho.
9. Logout e remoção da autenticação do navegador.
10. Tema escuro aplicado no HTML mesmo com o JavaScript da aplicação bloqueado intencionalmente.

O resumo executado está em `screenshots/checks.json`. As capturas mostram uma conta sintética e um banco SQLite temporário, com o gateway real e RAG desativado somente nessa instalação descartável. Não foram usados dados de uso diário nem realizadas chamadas ao DeepSeek. Os containers, rede e arquivo de credenciais de teste foram removidos ao final.

## Problemas encontrados durante a validação

- O TypeScript revelou incompatibilidades de props e APIs preexistentes em MessageList, React Markdown e DataGrid v5; foram corrigidas de forma pontual.
- A primeira navegação nova usava links diretamente dentro de um `ul`. A estrutura foi corrigida e a análise automatizada repetida.
- A revisão manual detectou herança inadequada de cor nos links do modo escuro. Os links agora recebem cores explícitas do tema, com contraste conferido nas capturas finais.
- O teste de recolhimento da sidebar precisava aguardar a atualização do DOM pelo React; a asserção foi ajustada para esperar a largura efetiva, sem introduzir atrasos arbitrários nessa verificação.
- No WSL, variáveis de ambiente adicionais para o Node Windows precisaram ser propagadas por `WSLENV`; o roteiro PowerShell evita essa particularidade.

## Pendências para revisão e próximas fases

- Receber o SVG revisado da logo. O monograma “IA” e o favicon desta fase são provisórios.
- Aprovar visualmente o layout e os temas antes do redesenho detalhado das páginas na Fase B.
- A aparência interna de login, chat, dashboard, conhecimento, Markdown e diagramas ainda é predominantemente a existente. Não foi substituída por demonstrações.
- O interceptor HTTP preexistente recarrega a tela no 401 do login e pode ocultar a mensagem de erro. Essa experiência deve ser tratada na Fase B, preservando a autenticação.
- Administração de usuários/papéis/permissões e manutenção do prompt interno ficam para a Fase C. A exposição dessas telas depende da autorização efetiva no servidor; menus novos não foram entregues nesta fase.
- A análise automatizada não representa uma auditoria completa de acessibilidade de todas as páginas. Não foi validada a qualidade de respostas da IA nesta etapa.

Os rascunhos anteriores de fases B/C foram preservados em backup local separado, sem serem aplicados aos containers. Alterações preexistentes de build, RAG e finais de linha também foram preservadas. Nenhum merge ou push automático foi realizado.

## Estado do Git

A entrega da Fase A foi preparada no índice de `feature/ui-redesign`, separada das alterações preexistentes de build/RAG. A tentativa de commit foi recusada pelo Git porque `user.name` e `user.email` não estão configurados neste ambiente. Nenhuma identidade de autor foi inventada ou configurada automaticamente. As alterações permanecem preparadas para revisão; nenhum commit, merge ou push desta fase foi criado.
