#!/usr/bin/env bash
set -euo pipefail
site=${1:?Provide the verified public hostname}
candidate=${2:?Provide the uploaded Caddyfile path}
[[ "$site" =~ ^[A-Za-z0-9.-]+$ ]] || { echo 'Invalid hostname' >&2; exit 1; }
docker=(sudo /usr/local/bin/docker --host unix:///run/ia-portal-docker.sock)
if [[ $("${docker[@]}" inspect --format '{{index .Config.Labels "com.ia-portal.edge"}}' ia-portal-web) == local-tunnel ]]; then
    echo 'Local edge already active; no container recreated.'; exit 0
fi
# Never switch the public edge unless both reverse forwards actually work.
for port in 19080 19089; do
    bindings=$(ss -H -ltn "sport = :$port" | awk '{print $4}')
    [[ "$bindings" == "127.0.0.1:$port" ]] || { echo "Remote TCP $port is absent or not exclusively loopback; no edge change." >&2; exit 1; }
done
curl --noproxy '*' --fail --max-time 10 -sS http://127.0.0.1:19080/login >/dev/null
curl --noproxy '*' --fail --max-time 10 -sS http://127.0.0.1:19089/health/ready >/dev/null
image=$("${docker[@]}" inspect --format '{{.Config.Image}}' ia-portal-web)
stamp=$(date -u +%Y%m%dT%H%M%SZ)
root=/opt/ia-portal/edge
sudo install -d -m 0700 "$root"
config="$root/$stamp.Caddyfile"
sudo install -m 0600 "$candidate" "$config"
"${docker[@]}" run --rm --network host --env "PORTAL_SITE=$site" --mount "type=bind,src=$config,dst=/etc/caddy/Caddyfile,readonly" "$image" caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile
backup="ia-portal-web-vm-$stamp"
"${docker[@]}" stop ia-portal-web >/dev/null
if ! "${docker[@]}" rename ia-portal-web "$backup"; then
    "${docker[@]}" start ia-portal-web >/dev/null
    echo 'Backup rename failed; original web restarted.' >&2; exit 1
fi
rolled_back=0
committed=0
rollback() {
    [[ "$rolled_back" == 0 ]] || return 0
    rolled_back=1
    "${docker[@]}" rm -f ia-portal-web >/dev/null 2>&1 || true
    "${docker[@]}" rename "$backup" ia-portal-web
    "${docker[@]}" start ia-portal-web >/dev/null
}
trap 'if [[ "$committed" == 0 ]]; then rollback; fi' EXIT
trap 'exit 1' HUP INT TERM
if ! "${docker[@]}" run -d --name ia-portal-web --label com.ia-portal.edge=local-tunnel \
    --restart unless-stopped --network host --memory 96m --memory-swap 128m --pids-limit 32 \
    --security-opt no-new-privileges --cap-drop ALL --cap-add NET_BIND_SERVICE --read-only \
    --env "PORTAL_SITE=$site" --env GOMEMLIMIT=64MiB --env GOMAXPROCS=1 \
    --log-opt max-size=5m --log-opt max-file=2 \
    --mount type=bind,src=/opt/ia-portal/caddy-data,dst=/data \
    --mount type=bind,src=/opt/ia-portal/caddy-config,dst=/config \
    --mount "type=bind,src=$config,dst=/etc/caddy/Caddyfile,readonly" "$image" >/dev/null; then
    rollback; echo 'New edge failed; original web restored.' >&2; exit 1
fi
for attempt in {1..20}; do
    if curl --noproxy '*' --fail --max-time 3 -sS --resolve "$site:443:127.0.0.1" "https://$site/health/ready" >/dev/null; then
        printf '%s\n' "$backup" | sudo tee "$root/previous-web-container" >/dev/null
        committed=1
        trap - EXIT HUP INT TERM
        echo "Local edge active. Previous web preserved as $backup; old API/database not modified."
        exit 0
    fi
    sleep 1
done
rollback; echo 'HTTPS validation failed; original web restored.' >&2; exit 1
