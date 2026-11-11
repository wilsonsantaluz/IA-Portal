# Micro VM memory pressure from automatic DNF metadata cache

**Recovered and guard applied on2026-11-09 (America/Sao_Paulo).** Public HTTPS
again serves the local workstation through the existing SSH tunnel.

Incident:2026-11-09 (America/Sao_Paulo). The public domain continued resolving to
the expected Oracle IPv4 and the workstation gateway returned ready200, while
the VM's SSH banner and HTTPS timed out. OCI lifecycle RUNNING did not establish
guest health. Monitoring reported memory54% ->95% and CPU0.5% ->63%, followed by
missing samples. This was not a DNS resolution failure or evidence of a general
Oracle outage.

Authenticated serial-console history showed repeated **global OOM** events with
the killed process `dnf` in `/system.slice/dnf-makecache.service`, using roughly
696–736MiB resident anonymous RAM. The Micro has about945MiB usable RAM. Those
historical events establish a recurring cache-job problem; a queued read-only
agent command had not responded while the current guest was stalled.

## Approved correction

The owner explicitly authorized this cache-specific protection and another soft
restart if needed. `guard-dnf-cache.sh` validates the service is the DNF makecache
job, saves its original unit/properties/timer-enabled state privately, disables
only `dnf-makecache.timer`, and caps that service at256MiB RAM,64MiB swap,20% CPU
and120seconds. It stops only this metadata-cache unit, not arbitrary `dnf`/`rpm`
processes. Other package/security update units are not modified. No package
installation/removal/upgrade, database migration, cloud resize or application
data change is performed.

```bash
sudo bash guard-dnf-cache.sh --check
sudo bash guard-dnf-cache.sh --apply
```

Default is check-only. A foreign operator override at the dedicated drop-in path
causes refusal rather than overwrite. Original state is retained under
`/opt/ia-portal/recovery/dnf-cache-guard`, and the owned persistent drop-in is
`/etc/systemd/system/dnf-makecache.service.d/90-ia-portal-memory.conf`. Systemd
runtime limits also constrain a cache job already running during application.

DNF metadata is refreshed on demand during normal manual package maintenance.
This small VM still needs a deliberate maintenance window/resources for full
package upgrades. Do not interpret this guard as automatic patch management or
as a reason to disable other security services. To reverse, inspect the saved
properties/timer state and remove only the identified IA-Portal drop-in/runtime
properties; do not use blanket `systemctl revert` or delete other overrides.

## Recovery automation and validation

`recover-dnf-cache.py` is a Windows completion worker. It never reboots or creates
a VM. It waits for strict verified SSH, copies only the public guard script,
applies the approved change through sudo, verifies the memory limit and disabled
timer, then checks normal public TLS/readiness, equality with local frontend HTML
and anonymous API401. It uses a singleton lock and publishes safe progress to
`%LOCALAPPDATA%\IA-Portal\reverse-tunnel\dnf-recovery-status.json`. The existing
reverse-tunnel runner handles reconnection independently.

Tests use an ephemeral Bash container, never the actual VM:

```powershell
docker run --rm -e DNF_FIXTURE_CONTAINER=yes --mount type=bind,source=C:\source\IA-Portal\infra\oracle,target=/scripts,readonly bash:5.2 bash /scripts/guard-dnf-cache.tests.sh
```

PASS: no changes in check mode, exact cache-only target, caps/timer/backup,
idempotent application, operator override preservation and refusal for a package
upgrade unit. Python recovery worker syntax passed. Runtime correction and public
recovery must be confirmed from the real worker/VM; successful mocks alone do not
establish restored availability.

## Actual recovery result

One incident-specific SOFTRESET was submitted after owner approval. SSH recovered,
the worker applied the guard and exited with phase `complete`. Actual properties:
`ActiveState=inactive`, timer `disabled`, `MemoryMax=268435456`,
`MemorySwapMax=67108864`, `CPUQuotaPerSecUSec=200ms`, `RuntimeMaxUSec=2min`.
The original unit/timer state was saved before applying the dedicated override.

Normal public TLS, readiness200, exact public/local frontend HTML hash and
anonymous API401 passed. Three short post-recovery samples reported312–320MiB
available guest memory and Caddy14–30MiB within its96MiB limit. The cache service
stayed inactive during these samples. This is short validation, not a long-running
availability guarantee.

Only the HTTPS edge runs on the VM; the old API remains stopped and preserved.
The tunnel runner and separate provisioning runner were left active. No changes
to DNS, modem, local database, user credentials, app schema or frontend/backend
code were required for this incident. No .NET/frontend build is claimed; only
the Bash/Python guard tests and actual infrastructure/HTTP checks were executed.
