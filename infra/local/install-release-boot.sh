#!/usr/bin/env bash
# Install boot integration without restarting the live stack or reading secrets.
set -Eeuo pipefail
[[ $EUID == 0 ]] || { echo 'Run as root' >&2; exit 1; }
repo=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
[[ -f /etc/ia-portal/deploy.conf && -d /opt/ia-portal/releases/active ]]
[[ ! -L /etc/ia-portal ]]
bash -n "$repo/scripts/deploy-stack.sh"
# The tunnel's 0700 leaf and 0600 key remain private. Its service user needs
# traversal of this shared parent, not membership of the runner's secret group.
chmod o+x /etc/ia-portal
install -m 0755 -o root -g root "$repo/scripts/deploy-stack.sh" /usr/local/libexec/ia-portal-deploy-stack
install -d -m 0755 /etc/systemd/system/ia-portal-stack.service.d
cat > /etc/systemd/system/ia-portal-stack.service.d/10-releases.conf <<'UNIT'
[Service]
WorkingDirectory=/opt/ia-portal
ExecStart=
ExecStart=/bin/bash /usr/local/libexec/ia-portal-deploy-stack start
ExecStop=
ExecStop=/bin/bash /usr/local/libexec/ia-portal-deploy-stack stop
UNIT
systemctl daemon-reload
systemd-analyze verify ia-portal-stack.service
echo 'Boot integration installed; live stack not restarted.'
