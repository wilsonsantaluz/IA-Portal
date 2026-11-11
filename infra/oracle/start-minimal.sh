#!/usr/bin/env bash
set -euo pipefail
cd /opt/ia-portal
gateway=${GATEWAY_IMAGE:-ia-portal/gateway:micro-20261108}
frontend=${FRONTEND_IMAGE:-ia-portal/frontend-tls:micro-20261108}
site=${PORTAL_SITE:?Set the verified domain, or http://localhost:9080 for an SSH-only test}
docker=(sudo /usr/local/bin/docker --host unix:///run/ia-portal-docker.sock)
for container in ia-portal-api ia-portal-web; do
  if "${docker[@]}" container inspect "$container" >/dev/null 2>&1; then
    echo "$container already exists. Preserve data/config and follow the documented update procedure." >&2
    exit 1
  fi
done
test -s runtime.env
chmod 600 runtime.env
sudo install -d -m 0700 -o 1654 -g 1654 data
sudo install -d -m 0700 caddy-data caddy-config
"${docker[@]}" image inspect "$gateway" "$frontend" >/dev/null
"${docker[@]}" run -d --name ia-portal-api --label com.ia-portal.deployment=minimal \
  --restart unless-stopped --network host --memory 384m --memory-swap 640m --pids-limit 128 \
  --security-opt no-new-privileges --cap-drop ALL --read-only --tmpfs /tmp:rw,noexec,nosuid,size=16m \
  --log-opt max-size=5m --log-opt max-file=2 \
  --env-file runtime.env --env ASPNETCORE_URLS=http://127.0.0.1:9089 \
  --mount type=bind,src=/opt/ia-portal/data,dst=/app/data "$gateway"
"${docker[@]}" run -d --name ia-portal-web --label com.ia-portal.deployment=minimal \
  --restart unless-stopped --network host --memory 96m --memory-swap 128m --pids-limit 32 \
  --security-opt no-new-privileges --cap-drop ALL --cap-add NET_BIND_SERVICE --read-only \
  --env "PORTAL_SITE=$site" --env API_UPSTREAM=127.0.0.1:9089 --env GOMEMLIMIT=64MiB --env GOMAXPROCS=1 \
  --log-opt max-size=5m --log-opt max-file=2 \
  --mount type=bind,src=/opt/ia-portal/caddy-data,dst=/data \
  --mount type=bind,src=/opt/ia-portal/caddy-config,dst=/config "$frontend"
"${docker[@]}" ps --format 'table {{.Names}}\t{{.Status}}'
