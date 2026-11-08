# Community workspace delivery ? 2026-11-07

Branch: `feature/community-organizations`, based on clean `01d33bc`. No automatic
merge, paid dependency, commercial fork, production deployment or OCI API write.
The previous local OCI retry runner was stopped by verified process identity to
respect the new task boundary. No `.env`, current portal database or private key
was read/copied/modified by this delivery.

## Diagnosis and delivered behavior

The repository already had tenant authentication/memberships, scoped KB/RAG,
user-owned conversations, connector grants and additive migrations 001?006.
The first increment completes workspace provisioning and built-in membership
policies rather than replacing that architecture. The [baseline audit](../architecture/CURRENT_STATE.md)
was written before application edits; its last section records delivered changes.

- `POST /api/organizations`: default-organization authorized administrator creates
  a server-ID workspace, creator membership Owner and local admin group atomically.
  No arbitrary client tenant ID is trusted and no other account is silently joined.
- Current-organization member listing and policy/status updates: Owner/Admin/Member/
  Viewer, owner-only changes, version/CAS checks and protected final active Owner.
  Admin cannot reset an Owner account through the old users endpoint. Existing
  custom group policies remain supported without auto-granting new access.
- Tenant-specific default model and `(organization, account)` personal preferences.
  Installed provider endpoints/keys remain installation-owned infrastructure;
  tenant secret vaults/model allowlists are explicitly deferred. Existing sessions
  keep their selected models.
- Existing selector now preserves its explicit validated tenant header. The new
  Organiza??es page uses the existing shell, themes, permissions and administration.
- One versioned technical-support template, two complete synthetic documents,
  reference behavior/capability metadata and example questions. Import uses existing
  knowledge service, skips edited/inactive matching documents and grants no tools.
  Real HTTP smoke verifies retrieval of imported/edited text in the existing chat.
- English contributor/security/conduct/self-hosting/architecture/product roadmap
  documentation, Mermaid diagrams, reusable first organization form and an honest
  eight-step setup specification. Apache 2.0/AGPLv3 alternatives documented; no
  LICENSE selected. Two legacy employer references removed from public docs.

## Migration 007 and rollback

`007_OrganizationWorkspace.sql` adds nullable PolicyRole and concurrency Version
on memberships, OrganizationAiDefaults and OrganizationAiPreferences. Existing
admin memberships map to Admin; the lowest active existing admin ID per tenant
becomes the single initial Owner. The immutable group relationship is used for
this choice so an UPDATE cannot promote later rows after updating the first.
Non-admin legacy custom groups keep a null policy and their existing grants.
The old account preference is copied only to organization 1; its old table remains
archival. No documents, passwords, sessions, connectors or old SQL checksums change.

Fresh schemas and v6 upgrades are covered. Existing schemas get a consistent
pre-upgrade backup. Operational rollback restores the complete backup into a new
path with the previous app version; no in-place destructive downgrade is offered.
Post-upgrade writes require reconciliation. Tests cover backup integrity/restore,
re-upgrade, repeated initialization and a late DDL failure rolling back columns,
tables, policy updates and version recording.

During testing, the SQLite 8 batch executor was observed to suppress later SQL
errors when using ExecuteNonQuery. The runner now explicitly consumes each reader
result before commit for all embedded migration batches. This changes execution,
not old migration text/checksums. [Upstream report](https://github.com/dotnet/efcore/issues/32740).
Historical test fixtures were updated to actually remove v7 schema when simulating
old versions, rather than permit partially recognized migrations.

## Validation actually executed

| Command / check | Result and scope |
| --- | --- |
| `dotnet build ai-gateway/AiGateway.csproj --no-restore` | PASS; 11 preexisting warnings, no errors |
| `dotnet build ocr-service/OcrService.csproj --no-restore` | PASS; no warnings/errors |
| `dotnet build ml-service/MlService.csproj --no-restore` | PASS; no warnings/errors |
| `dotnet run --project tests/Workspace --no-restore` | PASS; real v6->v7 fixture with multiple admins, single Owner, preferences, backup/restore/failure rollback, tenant filters, final Owner and adversarial vector payloads |
| `dotnet run --project tests/Organizations --no-restore` | PASS; legacy v4 migration preservation/backup/restore/rollback |
| `dotnet run --project tests/AtlasKnowledge --no-restore` | PASS; opt-in/idempotency/human edits/Atlas organization selection |
| `dotnet run --project tests/Connectors --no-restore` | PASS; migration, SQL/SSRF/limits/group/tenant protections; HTTP doubles, no live Trino in this task |
| `dotnet run --project tests/Providers --no-restore` | PASS; existing adapters, model selection, secrets, usage and migration behaviors |
| `dotnet run --project tests/ChatSessions --no-restore` | PASS; ownership, persistence, replay, scope and migration |
| `dotnet run --project tests/ChatTitles --no-restore` | PASS; titles, fallback, usage and migration |
| `dotnet run --project tests/Usage --no-restore` | PASS; usage aggregation/ownership, accounting and migration |
| `dotnet run --project tests/BusinessChat --no-restore` | PASS; documentary/SQL/hybrid/clarification behavior, no fabricated data |
| `dotnet run --project tests/access/SeedSmoke --no-restore` | PASS; legacy users/passwords/groups/knowledge preservation |
| `python scripts/smoke_organizations.py` | PASS; real gateway/JWT/temporary SQLite, create/switch, forged/cross-tenant IDs, model defaults/preferences, Member/Admin/Viewer denial, stale versions, live JWT revocation, template reapply and retrieval; LLM HTTP double |
| `python scripts/smoke_local.py --atlas` | PASS; existing API/OCR/LLM/Trino doubles, organization isolation, sources, hybrid charts, settings/usage and restart persistence |
| `dotnet run --no-build --project tests/RagSmoke -- <repo>` with RAG_TEST_QDRANT | PASS; new empty/disposable Qdrant container, local real ONNX embedding/reranking, index/search/SSE, fake LLM. Container removed in finally; not ARM64 |
| `npm run typecheck --prefix front` / `npm run build --prefix front` | PASS; existing large-chunk warning |
| `node tests/ui/organizations.cjs` | PASS; real component behavior with synthetic API fixtures, create/switch/reload/default/import, both themes, 390/320px, axe main checks |
| `node tests/ui/atlas.cjs` | PASS; chat/connectors regression, diagrams/numeric charts, themes/mobile, organization change and axe |
| `node tests/ui/atlas-security.cjs` | PASS; graph input/security checks |
| Browser parse with installed Mermaid | PASS; all three architecture diagrams in README/target architecture |
| `python -m unittest discover -s tests -p test_atlas_dataset.py -v` | PASS; four tests |
| `git diff --check` | PASS |

Initial failed checks were corrected and rerun: migration max-version recognition,
old synthetic fixtures, swallowed late batch errors, multiple-admin backfill,
expected Owner label, HTTP status/stream route assumptions, Windows SQLite test
connection cleanup and browser required-label/hover-tooltip timing. No production
reset or weakening of authorization was used to make tests pass.

Evidence: ignored `artifacts/community-tests/`, `artifacts/community-ui/` and
existing `artifacts/atlas-ui/`. Browser fixture screenshots are not a live backend
deployment; separate API smoke uses real authentication and persistence.

## Remaining work and recommended next increment

Implement consenting organization invitations and a complete ownership-management
journey first. Then add tenant model allowlists and encrypted/vault-backed secret
references, document history/index lifecycle and a required CI pipeline. Agents,
official WhatsApp Business Platform, other connector adapters, quotas/billing and
managed operations are planned, not implemented integrations. No WhatsApp Web,
Chromium automation or arbitrary admin SQL tool is added. No license is chosen.

SQLite is single-node; no multi-replica/SaaS readiness is claimed. Current OCR/ML
are .NET services, no Python runtime service or PostgreSQL is required. ARM64 base
images exist, but native MKL lacks ARM64 and full ARM64 runtime/resources still
require validation. Auth abuse protection, CORS/TLS, OCR limits, durable access
audit/privacy/retention and operational recovery remain production gates.

Run locally using [SELF_HOSTING](../SELF_HOSTING.md). The current running Atlas
stack was not rebuilt/migrated by these tests; preserve its Compose override and
run `docker compose build ai-gateway portal-front`, then
`docker compose up -d --no-deps ai-gateway portal-front` to apply the reviewed change
locally. Startup backs up and migrates existing SQLite; never delete volumes.

## File inventory

- `CODE_OF_CONDUCT.md`
- `CONTRIBUTING.md`
- `README.md`
- `SECURITY.md`
- `ai-gateway/AiGateway.csproj`
- `ai-gateway/Application/SeedWork/AtlasKnowledgeSeed.cs`
- `ai-gateway/Application/Templates/technical-support.v1.json`
- `ai-gateway/Controllers/Organizations/KnowledgeTemplatesController.cs`
- `ai-gateway/Controllers/Organizations/OrganizationsController.cs`
- `ai-gateway/Controllers/Users/UsersController.cs`
- `ai-gateway/Infrastructure/EF/AiProviderCatalog.cs`
- `ai-gateway/Infrastructure/EF/ChatSchemaMigrator.cs`
- `ai-gateway/Infrastructure/EF/Context/OrganizationSettings.cs`
- `ai-gateway/Infrastructure/EF/Context/PortalDbContext.cs`
- `ai-gateway/Infrastructure/EF/DatabaseInitializer.cs`
- `ai-gateway/Infrastructure/EF/Migrations/007_OrganizationWorkspace.sql`
- `ai-gateway/Infrastructure/EF/OrganizationWorkspaceService.cs`
- `ai-gateway/Infrastructure/Qdrant/Services/Chats/KnowledgeBaseRagService.cs`
- `ai-gateway/Program.cs`
- `ai-gateway/Security/PortalAccess.cs`
- `docs/LICENSE_OPTIONS.md`
- `docs/README.md`
- `docs/ROADMAP.md`
- `docs/SELF_HOSTING.md`
- `docs/architecture/AS_IS_AUDIT.md`
- `docs/architecture/CURRENT_STATE.md`
- `docs/architecture/TARGET_ARCHITECTURE.md`
- `docs/delivery/CODEX_START_HERE.md`
- `docs/delivery/COMMUNITY_WORKSPACE_REPORT.md`
- `docs/delivery/IMPLEMENTATION_LOG.md`
- `docs/demo/ATLAS_KNOWLEDGE_BASE.md`
- `docs/features/SETUP_WIZARD.md`
- `front/src/App.tsx`
- `front/src/components/OrganizationForm.tsx`
- `front/src/layout/HeaderBar.tsx`
- `front/src/layout/Sidebar.tsx`
- `front/src/pages/OrganizationsPage.tsx`
- `front/src/services/api.ts`
- `scripts/smoke_local.py`
- `scripts/smoke_organizations.py`
- `tests/ChatTitles/Program.cs`
- `tests/Connectors/MigrationChecks.cs`
- `tests/Fixtures/PreOrganizationContext.cs`
- `tests/Usage/Program.cs`
- `tests/Workspace/Program.cs`
- `tests/Workspace/VectorIsolationChecks.cs`
- `tests/Workspace/Workspace.csproj`
- `tests/ui/organizations.cjs`
