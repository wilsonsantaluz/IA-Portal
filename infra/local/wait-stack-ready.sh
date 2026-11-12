#!/usr/bin/env bash
set -euo pipefail
deadline=$((SECONDS + 90))
while (( SECONDS < deadline )); do
  if curl --fail --silent --max-time 3 http://127.0.0.1:9089/health/ready >/dev/null &&
     curl --fail --silent --max-time 3 http://127.0.0.1:9080/login >/dev/null; then
    echo 'IA-Portal HTTP readiness confirmed.'
    exit 0
  fi
  sleep 2
done
echo 'IA-Portal HTTP readiness timeout.' >&2
exit 1
