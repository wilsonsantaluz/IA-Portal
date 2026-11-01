# Testes de sessões — etapa 1

Execute na raiz. Os testes usam dados sintéticos e provedores simulados; não consomem créditos externos.

```powershell
dotnet build ai-gateway/AiGateway.csproj
dotnet run --project tests/ChatSessions
python scripts/smoke_local.py
dotnet run --project tests/access/SeedSmoke
```

`ChatSessions` cria diretório temporário e fixtures antigas para testar migração, backup e restauração, rollback de DDL, checksum, dono do JWT, paginação, idempotência, falhas/leases, renomeação e exclusão lógica. O DROP de tabelas existe somente para construir uma fixture antiga dentro desse banco temporário; nunca aponta para o banco da aplicação. Um handler HTTP em memória verifica cancelamento sem rede. O smoke Python inicia um gateway real com SQLite temporário e doubles HTTP de LLM/OCR, inclusive testes A/B por HTTP e persistência após reinício.

## Navegador

Requer Docker, Playwright, axe-core e Chrome. Ferramentas podem ser instaladas em `$env:TEMP/ia-portal-ui-tools`, conforme o roteiro em `tests/access/README.md`.

```powershell
docker build -t ia-portal-sessions-api ./ai-gateway
docker build -t ia-portal-sessions-front ./front
$sessionMock = Start-Process -FilePath (Get-Command python).Source -ArgumentList @('-u', 'tests/ChatSessions/mock_provider.py') -WorkingDirectory (Get-Location).Path -WindowStyle Hidden -PassThru
$sessionPassword = [guid]::NewGuid().ToString('N') + '!Aa9'
$sessionJwt = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
$sessionCredentials = Join-Path $env:TEMP 'ia-sessions-credentials.json'
[IO.File]::WriteAllText($sessionCredentials, (@{email='sessions@example.test'; password=$sessionPassword} | ConvertTo-Json))
docker network create ia-portal-sessions-test
docker run -d --name ia-portal-sessions-api --network ia-portal-sessions-test --network-alias ai-gateway --tmpfs /app/data -p 127.0.0.1:19089:8080 -e ASPNETCORE_URLS=http://+:8080 -e Chat__UseAdvancedRag=false -e 'ConnectionStrings__PortalConnection=Data Source=/app/data/portal.db;Foreign Keys=True' -e "Jwt__Key=$sessionJwt" -e Bootstrap__AdminEmail=sessions@example.test -e "Bootstrap__AdminPassword=$sessionPassword" -e DEEPSEEK_BASE_URL=http://host.docker.internal:19091 -e DEEPSEEK_API_KEY=synthetic-test-only -e OCR_SERVICE_URL=http://host.docker.internal:19091 ia-portal-sessions-api
docker run -d --name ia-portal-sessions-front --network ia-portal-sessions-test -p 127.0.0.1:19080:80 ia-portal-sessions-front
$env:IA_SESSION_CREDENTIALS = $sessionCredentials
$env:IA_PLAYWRIGHT_MODULE = Join-Path $env:TEMP 'ia-portal-ui-tools/node_modules/playwright'
$env:IA_AXE_MODULE = Join-Path $env:TEMP 'ia-portal-ui-tools/node_modules/@axe-core/playwright'
$env:IA_CHROME_PATH = Join-Path $env:ProgramFiles 'Google/Chrome/Application/chrome.exe'
node tests/ChatSessions/ui.cjs
```

O mock atende só fixtures de LLM/OCR na porta 19091 e não encaminha requests para serviços externos. Deve ser usado somente durante teste. O script de navegador requer conta `@example.test` e remove as conversas dessa conta de teste antes de iniciar; nunca usar uma instalação diária. `IA_UI_BASE_URL` altera a URL descartável (padrão `http://127.0.0.1:19080`).

Capturas: `docs/design/screenshots/sessions/`; resultado: `checks.json`. Limpeza, mesmo após falha:

```powershell
docker rm -f ia-portal-sessions-front ia-portal-sessions-api
docker network rm ia-portal-sessions-test
Stop-Process -Id $sessionMock.Id
Remove-Item -LiteralPath $sessionCredentials
```

Para regressão de RAG real, seguir `tests/RagSmoke/README.md`, sempre com Qdrant vazio/descartável. O teste agora cria sessões pelo servidor e espera 404 para histórico alheio, conforme o contrato atualizado.
