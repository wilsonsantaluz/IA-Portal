#!/usr/bin/env bash
set -Eeuo pipefail
umask 027

die() { echo "Deploy recusado: $*" >&2; exit 1; }
[[ $# == 1 && -f "$1" ]] || die 'informe o deploy.conf privado'
# Operator-controlled configuration, never obtained from a PR or workflow input.
source "$1"
: "${PORTAL_PROJECT_NAME:?}" "${PORTAL_DATA_VOLUME:?}" "${PORTAL_RUNTIME_ENV:?}"
: "${PORTAL_RELEASE_ROOT:?}" "${PORTAL_FRONT_URL:?}" "${PORTAL_READY_URL:?}"
[[ ${#PORTAL_COMPOSE_FILES[@]} -gt 0 ]] || die 'lista Compose vazia'
[[ "$PORTAL_RELEASE_ROOT" = /* && "$PORTAL_RUNTIME_ENV" = /* ]] || die 'use caminhos absolutos'
[[ -f "$PORTAL_RUNTIME_ENV" ]] || die 'runtime.env ausente'
export PORTAL_DATA_VOLUME COMPOSE_PROFILES
unset COMPOSE_FILE COMPOSE_PROJECT_NAME
command -v flock >/dev/null
command -v curl >/dev/null
docker compose version
mkdir -p "$PORTAL_RELEASE_ROOT"
exec 9>"$PORTAL_RELEASE_ROOT/.deploy.lock"
flock -n 9 || die 'outro deploy em andamento'

# Refuse a wrong project/volume rather than bootstrapping an empty production DB.
container=$(docker ps -aq --filter "label=com.docker.compose.project=$PORTAL_PROJECT_NAME" --filter 'label=com.docker.compose.service=ai-gateway')
[[ -n "$container" && "$container" != *$'\n'* ]] || die 'esperado exatamente um gateway existente'
mounted=$(docker inspect --format '{{range .Mounts}}{{if eq .Destination "/app/data"}}{{.Name}}{{end}}{{end}}' "$container")
[[ "$mounted" == "$PORTAL_DATA_VOLUME" ]] || die 'volume informado difere do gateway existente'
docker volume inspect "$PORTAL_DATA_VOLUME" >/dev/null

cd "$(git rev-parse --show-toplevel)"
revision=$(git rev-parse HEAD)
release=$(mktemp -d "$PORTAL_RELEASE_ROOT/$revision.XXXXXX")
# Persistent release: checkout cleanup must never remove live bind mounts.
git archive HEAD | tar -x -C "$release"
cd "$release"
compose=(docker compose --project-name "$PORTAL_PROJECT_NAME" --project-directory "$release" --env-file "$PORTAL_RUNTIME_ENV")
for file in "${PORTAL_COMPOSE_FILES[@]}"; do
  [[ -f "$file" ]] || die 'arquivo Compose configurado ausente'
  compose+=(-f "$file")
done
compose+=(-f infra/deploy/production.yml)
"${compose[@]}" config --quiet

# Build one service at a time to reduce peak pressure on the 7 GB host.
# No down, prune, volume recreation or automatic database rollback.
export COMPOSE_PARALLEL_LIMIT=1
export COMPOSE_BAKE=false
services=$("${compose[@]}" config --services)
while IFS= read -r service; do
  [[ -n "$service" ]] && "${compose[@]}" build "$service"
done <<< "$services"
"${compose[@]}" up -d --no-build --wait --wait-timeout 180
curl --fail --silent --show-error --retry 20 --retry-delay 3 --retry-connrefused --retry-all-errors --max-time 5 "$PORTAL_READY_URL" >/dev/null
curl --fail --silent --show-error --max-time 10 "$PORTAL_FRONT_URL" >/dev/null
printf '%s\n' "$release" > "$PORTAL_RELEASE_ROOT/current-release"
ln -sfn "$release" "$PORTAL_RELEASE_ROOT/active"
echo "Deploy validado: $revision"
