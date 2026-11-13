# Login compacto — validação em 12/11/2026

Branch: `feature/compact-login`. Alteração restrita ao login, sem novas dependências
ou mudanças de API, sessão, cadastro e retorno de convites.

Grid 48%/52%, altura mínima do viewport, formulário até 440 px, campos/botão de
46 px e intervalo de 16 px. Toolbar no fluxo normal. Abaixo de 900 px, ilustração
oculta e marca junto ao seletor de tema. Alturas insuficientes permitem rolagem.
Sem overflow hidden no shell, corte de conteúdo ou transform scale.

Imagem original e SVGs preservados. Painel ilustrado usa os tokens escuros
existentes nos dois temas, sem transparência. Object-fit contain mantém a arte
inteira visível. Cartão sobreposto, flutuantes e lista removidos. Formulário usa
tema selecionado, bordas legíveis no claro e botão sólido sem brilho adicional.
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
Formulário medido em 440 px no desktop/tablet e 342 px no mobile; campos/botão
em 46 px. Controles essenciais visíveis em Full HD com 800 px úteis.
Capturas de todos os viewports geradas e inspecionadas. Exemplos para revisão:

- [Desktop escuro](screenshots/login-compact-dark.png)
- [Desktop claro](screenshots/login-compact-light.png)
- [Mobile escuro](screenshots/login-compact-mobile-dark.png)
- [Mobile claro](screenshots/login-compact-mobile-light.png)

O teste `tests/ui/login-layout.cjs` confirma 12 layouts, zero violações axe WCAG
A/AA encontradas, ausência de erros JavaScript, aviso operável com Enter, senha
visível/oculta, payload original de login, loading com botão desabilitado, erro,
tema salvo e link de cadastro condicionado à API. Não equivale a auditoria
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
