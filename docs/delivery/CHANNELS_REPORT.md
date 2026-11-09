# IA-Portal Service MVP delivery — 2026-11-08

Branch: `feature/service-whatsapp`, from fetched `origin/main` (`a9ec52c`).
Initial tree clean. Existing organizations/invitations were present and reused.
No direct main changes, automatic merge, push, deploy, Oracle changes or paid
resource creation. Existing containers, SQLite and `.env` were not modified.

## Implemented

- Tenant-scoped channel administration and real `/channels` route/menu in the
  existing React/MUI shell. Owner/Admin use current backend RBAC; Member/Viewer
  are denied. Multiple local numbers per organization are supported.
- External contacts scoped per connection, persistent conversations/messages,
  unique external IDs, concurrent inbox deduplication, pagination and human pause.
- Opt-in deterministic extractive agent using shared local chat textual retrieval
  restricted to active authorized tenant document IDs. Response references include
  content hashes. No arbitrary SQL/tools or external inference.
- Durable worker/inbox/outbox, simulated delivery events, bounded transient retry,
  restart recovery, per-number and organization caps, 24-hour window checks,
  content redaction retention and deduplication tombstones.
- Official-envelope text webhook, verification GET, raw-body HMAC, operator-bound
  WABA/phone/organization routing, bounded parsing and unsupported-event handling.
  No automatic response to app echoes or history events.
- UI/CLI simulators; costs remain unknown rather than invented.

## Files

New gateway module `ai-gateway/Channels/`: models/contracts, EF mapping,
constrained agent, channel service, worker and controllers. The shared retriever
lives in `Infrastructure/Qdrant/Services/Chats`, so core chat does not depend on
the channel module. New migration
`Infrastructure/EF/Migrations/009_Channels.sql`. Updated `PortalDbContext`,
`ChatSchemaMigrator`, DI `Program`, permission catalog `PortalAccess`, and the
existing `LocalChatContextService` to use shared selection without changing its
document bounds/ranking.

Frontend: new `pages/ChannelsPage.tsx`; updated `App`, `Sidebar`, `HeaderBar`.
Scripts: `simulate_channel.py`, `smoke_channels.py`. Tests: new `tests/Channels`
and `tests/ui/channels.cjs`; historical migration fixture exclusions updated in
Workspace, Invitations, Connectors, PreOrganizationContext, ChatTitles and Usage.
Documentation: `docs/service/{README,ARCHITECTURE,WHATSAPP_RESEARCH}.md`, this report,
audit/current state, roadmap, self-hosting, docs index and implementation log.

## Migration

Version009 adds five tables/indexes with composite tenant/reference integrity.
No old checksums changed. Fresh DB and real v8 upgrade/repeat/backup restore tests
passed, including late-DDL failure rollback. Existing live database was not upgraded.
Rollback uses a complete pre-upgrade backup and prior binary at a new path after
stopping the gateway and reconciling newer writes; no destructive down script.

## Validation

| Command/check | Observed result |
| --- | --- |
| `dotnet build ai-gateway/AiGateway.csproj --no-restore` | PASS, 11 pre-existing warnings |
| `dotnet run --project tests/Channels` | PASS: v8 upgrade/repeat/backup/restore/late-DDL rollback, scoped reads/writes, both organizations' authorized evidence, multi-number contact isolation, concurrent duplicate input, default-off, agent/handoff/window/caps, persisted retry after fresh context, bounded failures, content retention/tombstones, signature checks |
| `dotnet run --project tests/Workspace`, Invitations, Organizations, AtlasKnowledge, Connectors, Providers, ChatSessions, ChatTitles, Usage, BusinessChat and access/SeedSmoke (separate projects) | PASS |
| `python scripts/smoke_channels.py` | PASS: actual HTTP/JWT/SQLite/background worker, opt-in simulation, references, duplicates, handoff, signed webhook/verification/malformed envelopes, ignored echoes, operator routing, tenant denials and Member403; synthetic secrets only |
| `python scripts/smoke_organizations.py` | PASS: workspace, invitation/ownership and tenant isolation regression |
| `python scripts/smoke_local.py --atlas` | PASS: current chat/SSE/titles/usage/providers/KB/connectors/hybrid/restart; local HTTP doubles, no paid calls |
| `npm run typecheck --prefix front` | PASS |
| `npm run build --prefix front` | PASS, existing large-chunk warning |
| `node tests/ui/channels.cjs` | PASS: empty/create/documents/default-off/opt-in/simulated history/handoff, light/dark, 390/320px and axe main; API fixtures |
| `node tests/ui/atlas.cjs`, `node tests/ui/atlas-security.cjs` | PASS: existing chat/connectors/tenant switching, numeric charts, Mermaid sandbox/fallback |
| Architecture diagram parse using the installed Mermaid renderer in Chrome | PASS |
| `git diff --check`, changed/new-file secret-pattern scan and new documentation local-link validation | PASS |

Browser tooling used local Chrome, temporary Playwright/axe modules and a disposable
Vite server at127.0.0.1:15173. Ignored artifacts include channel test/build logs and
light/dark screenshots. API smoke and browser fixtures are distinct validations.
No live Meta, real LLM, native ARM64, OCR or vector/ReRAG run was added here.

## Pending / deliberate boundaries

This is the requested **first local vertical**, not the complete WhatsApp/SaaS
roadmap. Real Cloud sending/access tokens, provider rate errors/ambiguous delivery
reconciliation, inbound delivery/pricing events, templates, Embedded Signup,
Coexistence eligibility/app-event/history sync, general agent definitions and human
manual sending are pending. Nullable billing fields reserve a storage contract;
they do not constitute implemented Meta billing ingestion. LLM usage/cost fields
are null because no model was called; existing web usage ledgers are preserved.

Official research is documented with primary links and explicit access limits:
some developer pages returned429/login. No account eligibility, current allowance
or price was certified. Backend rejects real auto-send rather than simulating a
successful official connection. Retention currently redacts content only; contact
IDs/tombstones/backups require a full erasure policy before handling public traffic.
Worker design is single-node; public ingress/rate protection, multi-app vault,
distributed leases, durable audit governance and service SLOs need later work.

## Local operation / next step

Follow [the module README](../service/README.md) to update application images with
existing overrides and demonstrate `/channels`. It includes optional CLI simulation
and unexecuted Meta test-account setup instructions. No secrets are required for
the local vertical and no new dependency/service is required.

Next increment: after authorized Meta test credentials and confirmed official
contracts/eligibility, implement a safe Cloud outbox sender and delivery/pricing
reconciliation, then Embedded Signup and verified account-specific Coexistence.
