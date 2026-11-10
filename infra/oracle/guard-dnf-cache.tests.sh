#!/usr/bin/env bash
set -euo pipefail
[[ ${DNF_FIXTURE_CONTAINER:-} == yes ]] && test -f /.dockerenv
mkdir -p /fixture/bin
export PATH=/fixture/bin:$PATH
cat >/fixture/bin/systemctl <<'EOF'
#!/usr/bin/env bash
echo "$*" >>/fixture/calls
if [[ "$*" == *LoadState* ]]; then echo loaded
elif [[ "$*" == *ExecStart* ]]; then echo "${FIXTURE_EXEC:-/usr/bin/dnf makecache --timer}"
elif [[ "$1" == is-enabled ]]; then echo enabled
elif [[ "$1" == cat ]]; then echo original-unit
else echo synthetic-status
fi
EOF
chmod +x /fixture/bin/systemctl
script=/scripts/guard-dnf-cache.sh
bash "$script" --check >/fixture/output
! test -f /etc/systemd/system/dnf-makecache.service.d/90-ia-portal-memory.conf
! grep -q '^disable\|^stop\|^set-property' /fixture/calls
bash "$script" --apply >/fixture/output
grep -q '^disable --now dnf-makecache.timer$' /fixture/calls
grep -q '^set-property --runtime dnf-makecache.service MemoryMax=256M MemorySwapMax=64M CPUQuota=20%$' /fixture/calls
grep -q '^MemoryMax=256M$' /etc/systemd/system/dnf-makecache.service.d/90-ia-portal-memory.conf
test "$(cat /opt/ia-portal/recovery/dnf-cache-guard/before.service)" = original-unit
bash "$script" --apply >/fixture/output
test "$(cat /opt/ia-portal/recovery/dnf-cache-guard/before.service)" = original-unit
echo operator-config >/etc/systemd/system/dnf-makecache.service.d/90-ia-portal-memory.conf
if bash "$script" --apply >/fixture/output 2>&1; then echo 'Expected operator override denial'; exit 1; fi
test "$(cat /etc/systemd/system/dnf-makecache.service.d/90-ia-portal-memory.conf)" = operator-config
export FIXTURE_EXEC='/usr/bin/dnf upgrade'
if bash "$script" --apply >/fixture/output 2>&1; then echo 'Expected package-install unit denial'; exit 1; fi
echo 'PASS: dry-run no mutation, bounded cache unit only, timer/backup/idempotency, operator override and package-install unit preserved. Synthetic container only.'
