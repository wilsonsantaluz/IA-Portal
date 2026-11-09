#!/usr/bin/env bash
set -euo pipefail
# Explicit repair for the 1GB Oracle Linux Micro profile, not a general tuning step.
# Disables kernel crash dumps so their reserved memory can be used by the portal.
[[ $(id -u) -eq 0 ]] || { echo "Run through sudo on the Micro VM." >&2; exit 1; }
reserved=$(cat /sys/kernel/kexec_crash_size)
total_kib=$(awk '/^MemTotal:/ {print $2}' /proc/meminfo)
if (( reserved == 0 )); then
  echo "No crash memory is currently reserved; no change made."
  exit 0
fi
if (( total_kib * 1024 + reserved > 1152 * 1024 * 1024 )); then
  echo "Not a <=1GB Micro memory envelope; refusing to disable crash diagnostics." >&2
  exit 1
fi
command -v grubby >/dev/null
systemctl stop --no-block kdump.service
if [[ $(cat /sys/kernel/kexec_crash_loaded) == 1 ]]; then
  kexec -p -u
fi
printf '0\n' > /sys/kernel/kexec_crash_size
systemctl disable kdump.service
grubby --update-kernel=ALL --args=crashkernel=0
echo "Crash reservation released and future reservation disabled. Kernel crash dumps are unavailable."
free -m
