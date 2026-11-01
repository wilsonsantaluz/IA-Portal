# Testes de usuários, grupos e permissões

Use somente uma instalação descartável. Os scripts não devem apontar para a base diária. Nenhum teste lê `.env` ou chama o provedor de IA externo.

## Banco existente e inicialização

```powershell
dotnet run --project tests/access/SeedSmoke
```

O teste cria SQLite em memória com usuários, grupos e conteúdo sintéticos preexistentes. Confirma preservação de senhas/conteúdo, nenhum privilégio automático para não administradores e ausência de duplicação após inicializar duas vezes.

## API e navegador

Construa as imagens na raiz:

```powershell
docker build -t ia-portal-access-api ./ai-gateway
docker build -t ia-portal-access-front ./front
```

Inicie os serviços isolados, usando credenciais novas e temporárias:

```powershell
$accessPassword = [guid]::NewGuid().ToString('N') + '!Aa9'
$accessJwt = [guid]::NewGuid().ToString('N') + [guid]::NewGuid().ToString('N')
$accessCredentials = Join-Path $env:TEMP 'ia-portal-access-credentials.json'
[IO.File]::WriteAllText($accessCredentials, (@{ email='access-admin@example.test'; password=$accessPassword } | ConvertTo-Json))
docker network create ia-portal-access-test
docker run -d --name ia-portal-access-api --network ia-portal-access-test --network-alias ai-gateway --tmpfs /app/data -p 127.0.0.1:19089:8080 -e ASPNETCORE_URLS=http://+:8080 -e Chat__UseAdvancedRag=false -e 'ConnectionStrings__PortalConnection=Data Source=/app/data/portal.db;Foreign Keys=True' -e "Jwt__Key=$accessJwt" -e Bootstrap__AdminEmail=access-admin@example.test -e "Bootstrap__AdminPassword=$accessPassword" ia-portal-access-api
docker run -d --name ia-portal-access-front --network ia-portal-access-test -p 127.0.0.1:19080:80 ia-portal-access-front
$env:IA_ACCESS_CREDENTIALS = $accessCredentials
# Aguarde o gateway responder HTTP 200 em /swagger/v1/swagger.json.
node tests/access/api.cjs
```

API: confirma HTTP 401/403, validação de cadastro, associação a vários grupos, revogação imediata com JWT já emitido, desativação, nova senha, grupo admin protegido, ausência de hash nos retornos e exclusão de grupo sem membros.

Para o navegador, instale ferramentas fora do projeto:

```powershell
$accessTools = Join-Path $env:TEMP 'ia-portal-ui-tools'
npm.cmd install --prefix $accessTools playwright @axe-core/playwright
$env:IA_PLAYWRIGHT_MODULE = Join-Path $accessTools 'node_modules/playwright'
$env:IA_AXE_MODULE = Join-Path $accessTools 'node_modules/@axe-core/playwright'
$env:IA_CHROME_PATH = Join-Path $env:ProgramFiles 'Google/Chrome/Application/chrome.exe'
node tests/access/ui.cjs
```

O teste usa APIs reais, cria/edita grupos e usuários, desativa/reativa, exclui grupo vazio com confirmação e faz login com conta restrita. Confere os menus, rotas diretas, consulta sem edição da base, revogação após atualizar a sessão, os dois temas, axe-core e larguras de 320/390 px. Os registros são exclusivos do SQLite descartável. As capturas ficam em `docs/design/screenshots/access/`.

`IA_ACCESS_URL` pode alterar a URL da API (padrão `http://127.0.0.1:19089`) e `IA_UI_BASE_URL` a do frontend (padrão `http://127.0.0.1:19080`).

Faça a limpeza mesmo em caso de falha:

```powershell
docker rm -f ia-portal-access-front ia-portal-access-api
docker network rm ia-portal-access-test
Remove-Item -LiteralPath $accessCredentials
```

Regressão com modelos reais e isolamento do histórico: [RagSmoke](../RagSmoke/README.md). Regressão local de chat, OCR simulado e persistência: `python scripts/smoke_local.py` após compilar o gateway. Ambos usam dados temporários e serviços de IA simulados.
