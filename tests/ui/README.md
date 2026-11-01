# Validação da Fase A no navegador

`phase-a.cjs` usa Playwright, Chrome/Chromium e axe-core. Execute apenas em uma instalação descartável com credenciais sintéticas. Não use a conta administrativa ou o banco de uso diário para produzir as capturas.

O teste cobre login e rejeição de credenciais inválidas, navegação protegida, sidebar recolhível, rotas existentes, temas, persistência, sincronização entre abas, menu mobile, larguras de 390 e 320 px, foco de teclado, logout e preferência aplicada antes do JavaScript. A análise automatizada de acessibilidade fica restrita ao cabeçalho, navegação e link de salto, correspondentes à Fase A.

## Exemplo em PowerShell, na raiz

As imagens devem ter sido construídas previamente com `docker compose build portal-front ai-gateway`. O gateway de teste usa RAG desativado e SQLite em tmpfs; nenhuma chamada ao provedor de IA é feita.

```powershell
$phaseAPassword = [guid]::NewGuid().ToString("N")
$phaseAJwtKey = [guid]::NewGuid().ToString("N") + [guid]::NewGuid().ToString("N")
$phaseACredentials = Join-Path $env:TEMP "ia-portal-phase-a-credentials.json"
$phaseATools = Join-Path $env:TEMP "ia-portal-ui-tools"
[IO.File]::WriteAllText($phaseACredentials, (@{ email = "phase-a@example.test"; password = $phaseAPassword } | ConvertTo-Json))

docker network create ia-portal-phase-a-test
docker run -d --name ia-portal-phase-a-api --network ia-portal-phase-a-test --network-alias ai-gateway --tmpfs /app/data -p 127.0.0.1:19089:8080 -e ASPNETCORE_URLS=http://+:8080 -e Chat__UseAdvancedRag=false -e "ConnectionStrings__PortalConnection=Data Source=/app/data/portal.db;Foreign Keys=True" -e "Jwt__Key=$phaseAJwtKey" -e Bootstrap__AdminEmail=phase-a@example.test -e "Bootstrap__AdminPassword=$phaseAPassword" ia-portal-ai-gateway
docker run -d --name ia-portal-phase-a-front --network ia-portal-phase-a-test -p 127.0.0.1:19080:80 ia-portal-portal-front

npm install --prefix $phaseATools playwright @axe-core/playwright
$env:IA_PLAYWRIGHT_MODULE = Join-Path $phaseATools "node_modules/playwright"
$env:IA_AXE_MODULE = Join-Path $phaseATools "node_modules/@axe-core/playwright"
$env:IA_CHROME_PATH = Join-Path $env:ProgramFiles "Google/Chrome/Application/chrome.exe"
$env:IA_UI_CREDENTIALS = $phaseACredentials
node tests/ui/phase-a.cjs

# Execute a limpeza mesmo se o teste falhar.
docker rm -f ia-portal-phase-a-front ia-portal-phase-a-api
docker network rm ia-portal-phase-a-test
Remove-Item $phaseACredentials
```

Se usar outro navegador Chromium, ajuste `IA_CHROME_PATH`. `IA_UI_BASE_URL` permite alterar a URL do frontend de teste. As capturas e o resumo das verificações são gravados em `docs/design/screenshots/`. O teste não imprime senhas nem tokens.

O comportamento preexistente do interceptor HTTP em uma resposta 401 no login é recarregar a página. O teste verifica a rejeição das credenciais sem presumir que a mensagem de erro permaneça visível. A melhoria desse retorno pertence à Fase B.

## Revisão visual das telas existentes

`phase-b.cjs` usa as mesmas variáveis de Playwright, Chrome e credenciais sintéticas acima e a mesma instalação descartável. Execute `node tests/ui/phase-b.cjs` após construir e iniciar o frontend de teste. Cobre sugestões editáveis do chat, dashboard vazio com API real, erro e nova tentativa, gráficos com fixtures restritas ao navegador, contraste automatizado do conteúdo principal de chat/dashboard e larguras de 320/390 px em ambos os temas. As capturas ficam em `docs/design/screenshots/redesign/`.

Para guardar as capturas do teste anterior nessa mesma pasta sem sobrescrever evidências antigas, configure `IA_UI_SCREENSHOTS` com seu caminho absoluto antes de executar `phase-a.cjs`. Os gráficos sintéticos não são capturados como dados reais.
