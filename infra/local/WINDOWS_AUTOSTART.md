# Windows logon startup for the local portal and reverse tunnel

Configured2026-11-10. The public Oracle Caddy already has Docker
`unless-stopped` and an enabled Docker systemd service. Locally the managed task
**IA-Portal-ReverseTunnel** runs on the current user's Windows logon, under that
user's interactive token with limited privileges and no stored password.

The task:

1. Opens the already installed Docker Desktop if it is not running.
2. Waits for the local gateway readiness endpoint while Docker restores containers.
3. Connects the strict-host-verified reverse SSH tunnel and retries on disconnection.

The task is hidden, has no execution time limit, uses IgnoreNew to reject duplicate
instances and retries task failures after one minute. The runner's mutex supplies
an additional duplicate guard. Public script copies live under
`%LOCALAPPDATA%\IA-Portal\reverse-tunnel\installed` so changing Git branches does
not remove the runtime. The private SSH key remains at its original protected
path; no key contents, API tokens or Windows password are stored in the task.

The user's Docker Desktop registry startup entry existed but was disabled by
Windows StartupApproved. The task explicitly launches Docker instead of editing
that registry preference. No automatic Windows login, sleep policy, machine reboot,
or pre-login Docker service workaround was configured.

## Install/update

```powershell
# Inspect definition only.
powershell -NoProfile -ExecutionPolicy Bypass -File infra/local/register-tunnel-autostart.ps1 -RemoteHost YOUR_VM_IP -KeyPath C:\oci\YOUR_KEY
# Register for the current user; discovers the installed Docker Desktop path.
powershell -NoProfile -ExecutionPolicy Bypass -File infra/local/register-tunnel-autostart.ps1 -RemoteHost YOUR_VM_IP -KeyPath C:\oci\YOUR_KEY -Register
```

An explicit `-DockerDesktopPath` is supported. The helper refuses unrelated tasks
with the same name and validates existing task ownership by SID, including Windows
short-name canonicalization. Repeating registration refreshes installed scripts
without interrupting an already matching task. To adopt changed arguments on a
running task, perform a controlled stop/start, not repeated parallel launches.

```powershell
Get-ScheduledTask -TaskName IA-Portal-ReverseTunnel
Get-ScheduledTaskInfo -TaskName IA-Portal-ReverseTunnel
```

A long-running healthy task can report267009 (`SCHED_S_TASK_RUNNING`), not exit0.
An additional start request may be refused while IgnoreNew keeps the existing
instance running; inspect task state, runner PID and public health together.

For deliberate maintenance:

```powershell
Disable-ScheduledTask -TaskName IA-Portal-ReverseTunnel
Stop-ScheduledTask -TaskName IA-Portal-ReverseTunnel
powershell -NoProfile -ExecutionPolicy Bypass -File infra/local/stop-reverse-tunnel.ps1
# Later:
Enable-ScheduledTask -TaskName IA-Portal-ReverseTunnel
Start-ScheduledTask -TaskName IA-Portal-ReverseTunnel
```

The stop helper recognizes both direct and installed launcher invocation, checks
process identity/start time, and handles a parent already terminated by Scheduler.
A recorded orphan SSH child can then be stopped without affecting other sessions.

## Container policy and limitations

Base/Atlas Compose now declare `restart: unless-stopped`; the bridge already did.
Existing running gateway/frontend/OCR/ML/Atlas Trino/bridge containers received the
same policy with `docker update`, without recreation or data changes. Optional RAG
profiles were not activated. Intentionally stopped containers remain stopped.
Startup restores services, not volatile Trino memory-catalog demo tables or other
nonpersistent provider data; no seed/import is added by this change.

**Docker Desktop requires this user's login.** After a workstation reboot, sign
in normally; Docker, containers and tunnel then start without opening a terminal.
Hosting before anyone logs in would need a different service/runtime arrangement.
The computer must stay powered on/awake and have network connectivity. SSH can
retry while Wi-Fi/Internet or the VM is unavailable; that is not a public uptime
guarantee.

## Validation performed

- Real task registration, SID ownership normalization and idempotent re-registration.
- Actual transfer from the manual runner to Scheduler, installed-path runner and
  SSH child visible, task Running/267009, normal public HTTPS readiness200.
- Repeated task start did not duplicate the runner. A stopped parent/orphan during
  handover was identified and cleaned using the recorded SSH PID/start time; stop
  helper was corrected for terminated-parent races.
- All six existing running containers have unless-stopped; combined Compose
  validation passed while preserving private overrides. Remote edge policy and
  Docker systemd enablement were verified read-only.
- Existing reverse-tunnel safety tests passed. No Windows reboot, Docker Desktop
  shutdown, database migration, application build or real message/LLM call occurred.
