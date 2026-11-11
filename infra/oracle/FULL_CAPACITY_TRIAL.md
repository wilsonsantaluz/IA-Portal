# Full-stack capacity attempt — 2026-11-09

Branch: `infra/oci-full-capacity-trial`, from the clean minimal-deployment branch.
The owner requested an attempt to run connectors, Qdrant and the complete stack
on the existing1GB Micro. **The complete stack was not deployed**: bounded tests
showed that the current native RAG and Trino exceed the machine's capacity.

The existing minimal portal is again available at **https://santaluz.ddns.net**.
No application image, account/password, database schema, model or connector policy
was replaced. No VM/volume/paid resource was created; no real LLM call was made.

## Initial condition and recovery

Before any trial component was added remotely, both public readiness and SSH
already timed out. OCI listed only the existing1GB Micro; the previously authorized
A1 capacity retry runner remained active and was left alone.

Requested one controlled OCI `SOFTRESET` to recover access, then inspected the
guest. The OS exposed only **510640KiB (498MiB)** total memory. Its boot argument
was `crashkernel=1G-64G:448M,64G-:512M`; `/sys/kernel/kexec_crash_size` reported
**469762048bytes (448MiB)**, and kdump was activating. This was a confirmed memory
reservation, not an assumption based on container usage.

Stopped/disabled `kdump.service`, released the crash reservation through sysfs,
and set `crashkernel=0` for installed kernels using `grubby`. Usable memory
immediately returned to **969392KiB (946MiB)** without another reboot. The default
boot entry was checked and contains `crashkernel=0`. Kernel crash dumps are now
unavailable; the tradeoff preserves RAM for this1GB demonstration host. No security
service/firewall/access control was disabled.

The checked-in `reclaim-micro-crash-memory.sh` reproduces this explicit repair
and refuses larger memory envelopes. Its current no-reservation/idempotent path
was verified on the VM. The release commands were applied directly when recovering
the existing reservation. A future boot with the new argument has not yet been
tested; no need to interrupt the recovered portal again for this capacity attempt.
To restore crash diagnostics later, review the appropriate reservation for the
new hardware, restore the boot argument and re-enable kdump as an operator task.

The original soft reboot also verified that the dedicated Docker service and both
application containers start automatically. A persistent previous-boot kernel
journal was unavailable, so the exact cause of the earlier loss of responsiveness
cannot be proven. The448MiB reservation was a concrete additional constraint.

## Bounded local experiments

Trials ran in **new disposable local containers**, with the same Linux gateway,
project model files mounted read-only, synthetic accounts/documents, temporary
SQLite/storage and a fake provider at loopback port1. They did not use the local
portal's database/index, real credentials or an external inference service. This
kept model/native OOM experiments away from the public VM and its accounts.

| Experiment | Observed result |
| --- | --- |
| Qdrant1.20.0,128MiB cap | Approximately18.9–22.3MiB in the small isolated fixture; real collection/upsert/tenant-filtered query passed |
| Existing full native RAG,768MiB cap, no container swap | Process reached a sampled638MiB, then kernel OOM; exit137, `OOMKilled=true`, before indexing completed |
| Existing full native RAG,1536MiB cap, no container swap | Embedding/indexing completed; sampled893MiB and1.321GiB during retrieval/reranking; then kernel OOM, exit137, `OOMKilled=true` |
| Existing local Atlas Trino484 | Approximately1.371GiB already in use, with the configured2GiB limit |
| Trino484,512MiB cap with original query settings | Configuration rejected; this first result alone is not an OOM measurement |
| Trino484,512MiB cap with query limits reduced to64MB and heap headroom64MB | Advanced through plugin loading, then kernel OOM, exit137, `OOMKilled=true` |

Qdrant's small idle footprint is **not** the footprint of the application RAG
pipeline. Existing model files are547310275bytes for embedding and571785841bytes
for reranking, plus tokenizers. File sizes are not presented as RAM requirements;
the actual container OOM results above establish the tested capacity failure.

No smaller/different model or vector dimension was silently substituted. Even
the1.5GiB native gateway failed before combining it with Trino, Qdrant, OCR/ML,
frontend and OS. Increasing swap does not make this a validated full deployment.
Trino's own deployment documentation says to leave memory for native/OS/other
processes and does not support relying on swap for Trino.
[Trino deployment guidance](https://trino.io/docs/current/installation/deployment.html).

All trial containers and their temporary network were removed after recording
results. The existing local full Compose stack was not stopped or recreated.
No unused Qdrant service was left on the VM merely to claim completed RAG support.

## Data preservation and validation

Executed `backup-state.py` through sudo on the recovered VM. It uses SQLite's
online backup API, not a live-file copy that could omit WAL data. It creates a
private root-only timestamped directory, verifies integrity/foreign keys and
preserves the private runtime configuration separately inside that directory.
It prints counts only, not emails, password hashes, keys or document contents.

Snapshot created at:

`/opt/ia-portal/recovery-backups/20261109T031820610209Z`

Preserved: one user, one organization, one membership, five KB rows, one chat
session and two messages. Backup integrity and foreign keys passed. The active
account and completed assistant answer remain present. No password was reset.

Public HTTPS login page, its versioned assets and `/health/ready` all returned200.
Both minimal containers are running, with zero automatic restarts/OOM flags in
the post-recovery inspection. A later snapshot showed:

- RAM946MiB total,636MiB available; swap192MiB used of944MiB.
- API129.5MiB; frontend/HTTPS44.44MiB.
- Current crash reservation0; kdump disabled; default boot configuration verified.

The authenticated smoke using the **initial** bootstrap password received401.
The account is active and the bootstrap login address still exists, but the
initial credential no longer authenticates. No attempt was made to reset it,
mint a bypass token or overwrite user changes. Therefore authenticated recovery
smoke is **not claimed as passed**. Data preservation was verified independently
via consistent backup, integrity and metadata; the user should use their current
password. No actual password is published or requested in chat.

## Reproduction and remaining blocker

`scripts/probe_full_capacity.py --configuration <private-synthetic-probe.env>`
targets only the fixed disposable loopback port29089 and container name
`ia-portal-full-trial-api`. The configuration must use `probe@example.test`, a
random synthetic bootstrap password/JWT and key `synthetic-probe-only`; the script
asserts the fake identity/key before making requests. It reports resource samples
and container exit/OOM status, not a fake successful RAG result.

Prepare disposable Qdrant/network and the existing gateway image with
`Hosting__Minimal=false`, `Chat__UseAdvancedRag=true`, fake DeepSeek origin
`http://127.0.0.1:1/` explicitly allowed, project embedding/reranker paths mounted
under `/models`, and temporary UID1654-owned `/app/data`. Bound the API memory with
`--memory 768m --memory-swap 768m`, then1536m for the second trial. Publish only
loopback29089; never reuse a live collection/database for these OOM experiments.
Ignored evidence is in `artifacts/full-capacity-rag-probe.log` and
`artifacts/full-capacity-rag-1536-probe.log`.

Python syntax checks and actual on-VM backup/idempotent repair execution passed.
No backend/frontend code was changed, so no new app build or migration was needed.
The full deployment remains blocked by memory, not by a missing menu or Docker
flag. The previously planned A1 with12GB is the appropriate next environment to
validate this existing full stack; capacity is not yet available/confirmed.
Alternative smaller models, removal of reranking or external components require
a separate explicit design choice and quality/security validation. They were not
introduced as a disguised complete deployment on this Micro.
