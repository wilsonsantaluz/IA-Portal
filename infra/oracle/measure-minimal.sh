#!/usr/bin/env bash
set -euo pipefail
samples=${1:-12}
[[ "$samples" =~ ^[0-9]+$ ]] && ((samples>=1 && samples<=120)) || exit 2
docker=(sudo /usr/local/bin/docker --host unix:///run/ia-portal-docker.sock)
for ((sample=1;sample<=samples;sample++)); do
  date -u +%FT%TZ
  free -m | head -3
  "${docker[@]}" stats --no-stream --format '{{.Name}} {{.MemUsage}} {{.CPUPerc}}'
  "${docker[@]}" inspect --format '{{.Name}} restarts={{.RestartCount}} oom={{.State.OOMKilled}}' ia-portal-api ia-portal-web
  curl -fsS http://127.0.0.1:9089/health/ready
  printf '\n'
  sleep 10
done
