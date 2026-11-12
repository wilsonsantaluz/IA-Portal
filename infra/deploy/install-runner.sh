#!/usr/bin/env bash
# Ubuntu x64. Installs binaries only; registration uses a short-lived GitHub token.
set -Eeuo pipefail
[[ $EUID == 0 && $(uname -m) == x86_64 ]]
id github-runner >/dev/null 2>&1 || useradd --create-home --shell /bin/bash github-runner
# Docker group grants control of Docker/the host; restrict repository writers.
usermod -aG docker github-runner
install -d -o github-runner -g github-runner -m 750 /opt/actions-runner /opt/ia-portal/releases
# Parent must be traversable to read the private runtime and create releases.
chgrp github-runner /opt/ia-portal
chmod g+rx /opt/ia-portal
[[ ! -e /opt/actions-runner/config.sh ]] || { echo 'Runner already installed; not replacing it.'; exit 0; }
curl -fsSL --max-time 60 https://api.github.com/repos/actions/runner/releases/latest -o /opt/actions-runner/release.json
read -r url digest < <(python3 - <<'PY'
import json
r=json.load(open('/opt/actions-runner/release.json'))
a=next(a for a in r['assets'] if a['name'].startswith('actions-runner-linux-x64-'))
assert a['browser_download_url'].startswith('https://github.com/actions/runner/releases/download/')
assert a['digest'].startswith('sha256:')
print(a['browser_download_url'], a['digest'].split(':',1)[1])
PY
)
curl -fL --retry 3 "$url" -o /opt/actions-runner/runner.tar.gz
printf '%s  %s\n' "$digest" /opt/actions-runner/runner.tar.gz | sha256sum -c -
tar -xzf /opt/actions-runner/runner.tar.gz -C /opt/actions-runner
chown -R github-runner:github-runner /opt/actions-runner
/opt/actions-runner/bin/installdependencies.sh
echo 'Binary installed. Register as github-runner, then install/start svc.sh.'
