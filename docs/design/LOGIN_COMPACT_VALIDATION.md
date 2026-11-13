# Login compacto — validação em 12/11/2026

Revisão: `feature/login-glass-motion`, após o PR #17. Alteração restrita ao login, sem novas dependências
ou mudanças de API, sessão, cadastro e retorno de convites.

Grid 48%/52%, altura mínima do viewport, cartão de até 480 px com formulário de
422 px úteis no desktop, campos/botão de
46 px e intervalo de 16 px. Toolbar no fluxo normal. Abaixo de 900 px, ilustração
oculta e marca junto ao seletor de tema. Alturas insuficientes permitem rolagem.
Sem overflow hidden no shell, corte de conteúdo ou transform scale.

Imagem original e SVGs preservados. Painel ilustrado usa os tokens escuros
existentes nos dois temas, sem reduzir a opacidade da imagem. Object-fit cover
preenche o painel mantendo o elemento IA-Portal visível; extremidades decorativas
podem ser recortadas conforme a proporção. Mensagem curta em vidro fosco de
aproximadamente 140–160 px de altura, sem lista de funcionalidades. Elementos
decorativos flutuam por transform CSS em ciclos de 7 s. Formulário em cartão
translúcido com blur, sombra, borda discreta e entrada de 0,5 s; fundo com gradientes
estáticos. Claro usa superfícies neutras; escuro recupera profundidade e luz suave.
Tokens existentes reutilizados. Sem cópia de marca, imagem ou código da referência.
Somente elementos decorativos usam posicionamento absoluto; conteúdo essencial
permanece em Grid/Flex. Movimento desativado em prefers-reduced-motion.
Aviso completo em details/summary nativos, expansíveis por teclado.

## Resultado no navegador

Chrome headless instalado no Windows, Playwright e axe já disponíveis fora do
projeto. Build final servida pelo Vite preview; respostas de signup status e
login simuladas. Sem contas ou dados de produção.

| Viewport CSS | Claro | Escuro | Overflow horizontal | Rolagem vertical inicial |
| --- | --- | --- | --- | --- |
| 1920×1080 | PASS | PASS | Não | Não |
| 1920×800 (área útil) | PASS | PASS | Não | Não |
| 1366×768 | PASS | PASS | Não | Não |
| 1366×640 (área útil adicional) | PASS | PASS | Não | Não |
| 768×1024 | PASS | PASS | Não | Não |
| 390×844 | PASS | PASS | Não | Não |

Em 390×360 com erro, ambos os temas permitem rolar até o último controle.
Formulário medido em 422 px no desktop/tablet e 316 px no mobile; campos/botão
em 46 px. Controles essenciais visíveis em Full HD com 800 px úteis.
Capturas de todos os viewports geradas e inspecionadas. Exemplos para revisão:

- [Desktop escuro](screenshots/login-compact-dark.png)
- [Desktop claro](screenshots/login-compact-light.png)
- [Mobile escuro](screenshots/login-compact-mobile-dark.png)
- [Mobile claro](screenshots/login-compact-mobile-light.png)

O teste `tests/ui/login-layout.cjs` confirma 12 layouts, zero violações axe WCAG
A/AA encontradas, ausência de erros JavaScript, aviso operável com Enter, senha
visível/oculta, payload original de login, loading com botão desabilitado, erro,
tema salvo e link de cadastro condicionado à API. A animação também foi verificada
com movimento normal (transform varia) e preferência reduzida (animation none).
Não equivale a auditoria
completa de acessibilidade.

## Comandos executados

- `cd front; yarn.cmd typecheck`: PASS.
- `cd front; yarn.cmd build`: PASS. Aviso de chunks >500 kB; divisão do bundle
  não faz parte desta alteração.
- `node tests/ui/login-layout.cjs`: PASS no Vite de desenvolvimento.
- `$env:IA_LOGIN_URL='http://127.0.0.1:19121'` e
  `node tests/ui/login-layout.cjs`: PASS no Vite preview da build final.
- `git diff --check`: PASS.

Para reproduzir: iniciar `yarn dev --host 127.0.0.1 --port 19120` em front e rodar
o script de UI da raiz. Ele aceita IA_LOGIN_URL, IA_PLAYWRIGHT_MODULE,
IA_AXE_MODULE e IA_CHROME_PATH. Módulos de teste não adicionados ao package.json.
Métricas/capturas completas em artifacts/login-layout, ignorado pelo Git.

## Limites

Sem login real, LLM, testes .NET ou containers: backend não foi alterado.
Sem deploy. Mobile/tablet validados por viewport do Chrome desktop, sem aparelho
físico/teclado virtual, Safari ou Firefox. Referências e ativos existentes
reutilizados; nenhuma identidade nova foi criada.
