# Deploy pelo GitHub Actions no servidor local

O workflow `deploy-local.yml` faz build **no servidor**, sem exportar imagens da
estação e sem registry. Executa em push na `main` (inclui merge de PR) ou execução
manual na `main`. Proteja a branch para exigir PR/revisão; o evento push também
inclui pushes diretos permitidos pelo GitHub. Não há gatilho de pull request.

## Instalação identificada em 12/11/2026

Ubuntu x64, `/opt/ia-portal`, projeto `ia-portal`. Stack com Compose base,
Atlas, LiteLLM e WhatsApp browser; perfil `litellm`. O antigo override
`docker-compose.server-images.yml` aponta para imagens importadas e **não deve
ser incluído no novo pipeline**, que constrói as imagens no próprio servidor.
Volumes e configuração privada existentes devem permanecer.

## Preparar e registrar

1. Instale os binários com `sudo bash infra/deploy/install-runner.sh`.
2. Em GitHub → repositório → Settings → Actions → Runners → New self-hosted
   runner → Linux x64, obtenha o token temporário. No servidor, configure com
   o usuário dedicado (não envie o token ao chat ou commite-o):

   ```bash
   sudo -iu github-runner
   cd /opt/actions-runner
   read -rsp 'Token temporário: ' RUNNER_TOKEN; echo
   ./config.sh --unattended --url https://github.com/wilsonsantaluz/IA-Portal \
     --token "$RUNNER_TOKEN" --name ia-portal-62 --labels ia-portal-62 --work _work
   unset RUNNER_TOKEN
   exit
   sudo /opt/actions-runner/svc.sh install github-runner
   sudo /opt/actions-runner/svc.sh start
   ```

O runner faz conexões de saída ao GitHub; não precisa publicar SSH na Internet.
O grupo Docker permite controle do host: limite quem altera workflows/main e
não use esse runner para executar código de PRs de forks. Confira as
[orientações oficiais](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners).

## Configuração privada e boot

Instale `/etc/ia-portal/deploy.conf` a partir de
`infra/deploy/deploy.conf.example`, proprietário root, grupo github-runner, 0640.
Para a instalação migrada, configure:

```bash
PORTAL_PROJECT_NAME=ia-portal
PORTAL_DATA_VOLUME=ia-portal_portal_data
PORTAL_RUNTIME_ENV=/opt/ia-portal/.env
PORTAL_RELEASE_ROOT=/opt/ia-portal/releases
PORTAL_COMPOSE_FILES=(docker-compose.yml docker-compose.atlas.yml docker-compose.litellm.yml docker-compose.whatsapp-browser.yml /etc/ia-portal/local.yml)
COMPOSE_PROFILES=litellm
PORTAL_FRONT_URL=http://127.0.0.1:9080
PORTAL_READY_URL=http://127.0.0.1:9089/health/ready
```

O usuário do runner precisa ler o runtime.env (root:github-runner, 0640).
Preserve os modelos fora dos releases com `/etc/ia-portal/local.yml`:

```yaml
services:
  ai-gateway:
    volumes:
      - /opt/ia-portal/models:/app/models:ro
```

Não remova outros overrides privados, portas ou mounts da instalação. O script
confere se `/app/data` do gateway existente usa o volume declarado. O volume é
externo: um nome errado não cria um banco vazio. Outros volumes mantêm o mesmo
nome de projeto. Releases persistentes conservam os bind mounts mesmo quando o
runner limpa o checkout. Não há exclusão automática de releases/imagens.

Habilite um drop-in de `ia-portal-stack.service` para usar a release validada no
boot. Até o primeiro sucesso, o wrapper preserva a stack migrada em `/opt/ia-portal`:

```ini
[Service]
WorkingDirectory=/opt/ia-portal
ExecStart=
ExecStart=/bin/bash /usr/local/libexec/ia-portal-deploy-stack start
ExecStop=
ExecStop=/bin/bash /usr/local/libexec/ia-portal-deploy-stack stop
```

Instale `scripts/deploy-stack.sh` em `/usr/local/libexec/ia-portal-deploy-stack`
como root:root 0755 e rode `sudo systemctl daemon-reload`. Não reinicie o serviço
para instalar o drop-in. O túnel permanece com a unidade existente.

## Comportamento e falhas

Build sequencial reduz concorrência no host de 7 GB; ainda haverá pico de CPU/RAM
durante compilação. O Dockerfile frontend já executa typecheck/build; isso não
substitui testes de backend. Compose aguarda healthchecks e o script verifica
readiness SQLite e HTTP frontend. Login/LLM/WhatsApp não são comprovados por isso.

Não executa down/prune/reset, não restaura SQLite automaticamente. Migrações
continuam com backup consistente implementado no gateway. Falha após recriação
pode deixar parte da stack atualizada: examine o job e o banco antes de tentar
rollback. O apontador active só muda após validação. Não reverta imagens com
esquema incompatível nem restaure backup descartando gravações sem revisão.

## Trino ocioso

Medição remota: Trino 1,47 GiB/2 GiB e 3,93% CPU; loader 11,01% CPU em uma
amostra; host 4,3 GB disponíveis, swap zero. Uma amostra não caracteriza picos.
Linhas Java no htop podem ser threads compartilhando a mesma memória; não some
o RES de cada thread. A JVM mantém heap/threads e trabalho interno sem usuários.
[Memória do Trino](https://trino.io/docs/current/installation/deployment.html).

O loader de demonstração gera e verifica fingerprint/contagens periodicamente.
O intervalo saudável passa de 30 para 300 segundos; falhas tentam novamente a
cada 5 segundos. O healthcheck acompanha a nova janela (660 segundos). Após um
reinício do Trino, a recuperação do catálogo Memory pode levar cinco minutos.
Não reduzimos limites de memória às cegas nem removemos Atlas: consultas e
conexões existentes dependem dele. Para desativá-lo, planeje também a remoção das
dependências do gateway; simplesmente parar Trino não é uma solução permanente.

Diagnóstico sem imprimir segredos:

```bash
docker stats --no-stream
docker top ia-portal-atlas-trino-1
docker logs --tail 30 ia-portal-atlas-loader-1
```
