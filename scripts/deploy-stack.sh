#!/usr/bin/env bash
# systemd boot/stop entrypoint, using the last successfully deployed release.
set -Eeuo pipefail
source /etc/ia-portal/deploy.conf
export PORTAL_DATA_VOLUME COMPOSE_PROFILES
unset COMPOSE_FILE COMPOSE_PROJECT_NAME
if [[ -d "$PORTAL_RELEASE_ROOT/active" ]]; then
  cd "$PORTAL_RELEASE_ROOT/active"
  compose=(docker compose --project-name "$PORTAL_PROJECT_NAME" --env-file "$PORTAL_RUNTIME_ENV")
  for file in "${PORTAL_COMPOSE_FILES[@]}"; do compose+=(-f "$file"); done
  compose+=(-f infra/deploy/production.yml)
else
  # Before the first successful Actions deploy, retain the migrated stack.
  cd /opt/ia-portal
  compose=(docker compose --project-name "$PORTAL_PROJECT_NAME" --env-file "$PORTAL_RUNTIME_ENV")
fi
case "${1:-}" in
  start)
    services=$("${compose[@]}" config --services)
    # Docker may auto-start a legacy orphan on host boot. Retire it only when
    # the selected release uses self-initializing Trino, not the old demo stack.
    if grep -qx atlas-trino <<< "$services" && ! grep -qx atlas-loader <<< "$services"; then
      legacy=$(docker ps -aq --filter "label=com.docker.compose.project=$PORTAL_PROJECT_NAME" --filter 'label=com.docker.compose.service=atlas-loader')
      if [[ -n "$legacy" ]]; then
        while IFS= read -r container_id; do
          docker update --restart=no "$container_id" >/dev/null
          docker stop --time 30 "$container_id" >/dev/null
        done <<< "$legacy"
      fi
    fi
    "${compose[@]}" up -d --no-build --pull never --wait --wait-timeout 240
    ;;
  stop) "${compose[@]}" stop --timeout 30 ;;
  *) exit 2 ;;
esac
