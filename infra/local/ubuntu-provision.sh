#!/usr/bin/env bash
# Run as root on a fresh, owner-authorized Ubuntu host. Does not reset data/firewall.
set -euo pipefail
. /etc/os-release
[[ "$ID" == ubuntu ]] || { echo 'Ubuntu required' >&2; exit 1; }
if ! command -v docker >/dev/null; then
  if dpkg-query -W -f='${Status}' docker.io containerd runc 2>/dev/null | grep -q 'install ok installed'; then
    echo 'Existing conflicting container packages; inspect before replacing' >&2
    exit 1
  fi
  export DEBIAN_FRONTEND=noninteractive
  apt-get update -qq
  apt-get install -y -qq ca-certificates curl openssh-client
  install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
  chmod 0644 /etc/apt/keyrings/docker.asc
  cat > /etc/apt/sources.list.d/docker.sources <<EOF
Types: deb
URIs: https://download.docker.com/linux/ubuntu
Suites: ${UBUNTU_CODENAME:-$VERSION_CODENAME}
Components: stable
Architectures: $(dpkg --print-architecture)
Signed-By: /etc/apt/keyrings/docker.asc
EOF
  apt-get update -qq
  apt-get install -y -qq docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
fi
systemctl enable --now docker
docker version --format '{{.Server.Version}}'
docker compose version --short
install -d -m 0755 /opt/ia-portal
install -d -m 0700 /etc/ia-portal/tunnel
if [[ ! -f /etc/ia-portal/tunnel/id_ed25519 ]]; then
  ssh-keygen -q -t ed25519 -N '' -C ia-portal-ubuntu-tunnel -f /etc/ia-portal/tunnel/id_ed25519
fi
echo 'PASS: Docker Engine/Compose available; dedicated tunnel key generated locally.'
