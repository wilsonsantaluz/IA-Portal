#!/usr/bin/env bash
set -euo pipefail
# Oracle Linux micro: avoid DNF's large metadata working set. Archive is downloaded
# from Docker's official HTTPS static distribution on the operator workstation.
archive=${1:?Pass the uploaded official Docker archive path}
if command -v docker >/dev/null 2>&1; then
  echo "An existing Docker installation was found; refusing to replace it." >&2
  exit 1
fi
install -d -m 0750 /opt/ia-portal/runtime
tar -xzf "$archive" -C /opt/ia-portal/runtime --strip-components=1
sudo install -m 0755 /opt/ia-portal/runtime/* /usr/local/bin/
sudo tee /etc/systemd/system/ia-portal-docker.service >/dev/null <<'UNIT'
[Unit]
Description=IA-Portal dedicated Docker runtime (host networking only)
After=network-online.target
Wants=network-online.target
StartLimitBurst=3
StartLimitIntervalSec=120
[Service]
Type=notify
ExecStart=/usr/local/bin/dockerd --host=unix:///run/ia-portal-docker.sock --group=root --data-root=/var/lib/ia-portal-docker --exec-root=/run/ia-portal-docker --pidfile=/run/ia-portal-docker.pid --bridge=none --iptables=false --ip6tables=false --ip-forward=false --ip-masq=false --userland-proxy=false
Restart=on-failure
RestartSec=5
Delegate=yes
KillMode=process
MemoryHigh=128M
MemoryMax=256M
MemorySwapMax=256M
LimitNOFILE=1048576
[Install]
WantedBy=multi-user.target
UNIT
sudo systemctl daemon-reload
sudo systemctl enable --now ia-portal-docker.service
sudo /usr/local/bin/docker --host unix:///run/ia-portal-docker.sock version --format '{{.Server.Version}}'
