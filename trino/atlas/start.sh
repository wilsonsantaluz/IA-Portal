#!/bin/bash
# Runs once on EVERY container start, including Docker restart-policy recovery.
set -Eeuo pipefail
ready=/tmp/atlas-initialized
rm -f "$ready"
server_pid=
loader_pid=
cleanup() {
  trap - EXIT TERM INT
  rm -f "$ready"
  [[ -z "$loader_pid" ]] || kill -TERM "$loader_pid" 2>/dev/null || true
  [[ -z "$server_pid" ]] || kill -TERM "$server_pid" 2>/dev/null || true
  [[ -z "$loader_pid" ]] || wait "$loader_pid" 2>/dev/null || true
  [[ -z "$server_pid" ]] || wait "$server_pid" 2>/dev/null || true
}
trap cleanup EXIT
trap 'exit 143' TERM
trap 'exit 130' INT
/usr/lib/trino/bin/launcher run --etc-dir /etc/trino &
server_pid=$!
available=false
ready_pattern='"starting"[[:space:]]*:[[:space:]]*false'
for ((attempt=0; attempt<180; attempt++)); do
  kill -0 "$server_pid" 2>/dev/null || { echo 'Trino exited before initialization' >&2; exit 1; }
  if info=$(curl --fail --silent --max-time 2 http://localhost:8080/v1/info) && [[ "$info" =~ $ready_pattern ]]; then
    available=true
    break
  fi
  sleep 1
done
[[ "$available" == true ]] || { echo 'Trino startup timed out' >&2; exit 1; }
# CLI is provided by the original Trino image. No Python runtime in this image.
trino --server http://localhost:8080 --user atlas_loader --file /opt/atlas/initialize.sql >/dev/null &
loader_pid=$!
wait "$loader_pid"
loader_pid=
touch "$ready"
echo 'Atlas initialized once; no background dataset polling.'
wait "$server_pid"
