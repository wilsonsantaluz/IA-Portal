#!/usr/bin/env bash
# Bounded automatic repository metadata refresh; not a package upgrade policy.
set -euo pipefail
mode=${1:---check}
[[ "$mode" == --check || "$mode" == --apply ]] || { echo 'Use --check or --apply' >&2; exit 2; }
unit=dnf-makecache.service
timer=dnf-makecache.timer
dropin=/etc/systemd/system/dnf-makecache.service.d/90-ia-portal-memory.conf
backup=/opt/ia-portal/recovery/dnf-cache-guard
[[ $(systemctl show "$unit" -p LoadState --value) == loaded ]] || { echo 'Expected DNF cache unit missing; no changes.' >&2; exit 1; }
command=$(systemctl show "$unit" -p ExecStart --value)
[[ "$command" == *dnf*makecache* ]] || { echo 'Unexpected unit command; refusing to modify it.' >&2; exit 1; }
echo 'Target: automatic DNF metadata cache only; package/security update units are not disabled.'
echo 'Proposed guard: disable background cache timer; cap this cache service at256MiB RAM/64MiB swap/20% CPU/120s.'
systemctl show "$unit" -p ActiveState -p MainPID -p MemoryCurrent
if [[ "$mode" == --check ]]; then echo 'Check only; no changes.'; exit 0; fi
[[ $(id -u) == 0 ]] || { echo 'Apply requires root.' >&2; exit 1; }
if [[ -f "$dropin" ]] && ! grep -q '^# Managed by IA-Portal DNF cache guard$' "$dropin"; then
    echo 'Existing operator override found; refusing to overwrite.' >&2; exit 1
fi
install -d -m 0700 "$backup"
if [[ ! -f "$backup/before.service" ]]; then
    systemctl cat "$unit" >"$backup/before.service"
    systemctl is-enabled "$timer" >"$backup/before.timer-enabled" || true
    systemctl show "$unit" -p MemoryMax -p MemorySwapMax -p CPUQuotaPerSecUSec -p RuntimeMaxUSec >"$backup/before.properties"
fi
install -d -m 0755 "$(dirname "$dropin")"
cat >"$dropin" <<'EOF'
# Managed by IA-Portal DNF cache guard
[Service]
MemoryAccounting=yes
MemoryMax=256M
MemorySwapMax=64M
CPUQuota=20%
RuntimeMaxSec=120s
TimeoutStopSec=15s
EOF
systemctl daemon-reload
systemctl disable --now "$timer"
# Apply a cgroup limit also to an already running cache job, without killing
# arbitrary dnf/rpm processes or interrupting a package installation.
systemctl set-property --runtime "$unit" MemoryMax=256M MemorySwapMax=64M CPUQuota=20%
systemctl stop --no-block "$unit"
echo 'Guard applied. Original unit/timer state saved privately; no package update/install performed.'
systemctl show "$unit" -p ActiveState -p MemoryMax -p MemorySwapMax
