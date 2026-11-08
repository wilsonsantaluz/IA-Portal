# Current state — IA-Portal

Audit date: 2026-11-07. Baseline: `01d33bc`; implementation branch:
`feature/community-organizations`. This inventory was written before changing
application code. Later changes and validation are recorded in the delivery log.

## Product and runtime

The repository contains a working self-hosted prototype, not a deployed SaaS.
Community self-hosting and a future managed service will share one core. No
license has been selected; public source availability alone does not grant an
open-source license. There is no commercial edition, billing gateway or agent
configuration system.

| Area | Existing implementation | Gaps / risks |
| --- | --- | --- |
| API | ASP.NET Core/.NET 8, controllers, scoped EF Core SQLite, application interfaces, provider/connector adapters | Boundaries are logical, not separate module assemblies; no need for more services |
| Web | React 18, TypeScript, Vite 7, MUI 5, React Router, Axios, Recharts/Mermaid | pt-BR, dark default/light optional; no organization management screen at baseline |
| OCR | .NET 8, system Tesseract and Poppler, authenticated upload through gateway | Native OCR resource limits/temp cleanup need further hardening |
| ML | .NET 8 service returning deterministic formulas | Not a trained prediction model; do not present as real forecasts |
| Python | Dataset/load/export/smoke scripts and HTTP test doubles | No Python application service or independently deployed ReRAG service |
| Storage | SQLite portal volume, checksummed SQL migrations 001–006, pre-upgrade consistent backup | Single-node local storage; no validated multi-replica/distributed migration coordinator |
| Vectors | Optional Qdrant + ONNX embedding/reranking | External models/licensing; live ARM64 inference and full vector stack not validated |
| Analytics | Optional Trino connector and closed Atlas query plans | No generic PostgreSQL/SQL Server/REST adapter; no arbitrary AI SQL |
| Delivery | Dockerfiles + Compose, opt-in Atlas/RAG, loopback bindings | No tracked CI workflow at baseline; no production deployment in this audit |

## Identity and organization boundaries

`AuthController` issues JWTs after BCrypt verification. JWT validation checks the
active account and password-derived token version against SQLite on **every
request**, then resolves `X-Organization-Id` against an active membership and
active organization. Headers and JWT permission claims are not trusted as an
authorization source: claims are replaced from current database permissions.
Missing header selects the first active organization, with an explicit Atlas
demo preference flag. Invalid or unauthorized headers fail authentication.

`PortalDbContext` has tenant query filters and write guards for KB, groups,
sessions, ratings and connectors; messages/usage inherit session scope.
Conversation repositories additionally validate authenticated ownership. User
identities are installation-wide; memberships/groups are organization-local.
Shared identities cannot have their password/profile changed by another tenant's
administrator. Default organization is created for a fresh install. Migration
005 assigns legacy data/memberships to it without deleting data or changing
passwords; optional Atlas lives in another organization.

Gaps discovered:

- `OrganizationsController` only lists a user's organizations. No create/manage
  endpoints or built-in Owner/Admin/Member/Viewer membership policies exist.
- Custom group RBAC exists; the reserved `admin` group grants the catalog.
  Preserve these existing grants while introducing explicit policies.
- `AiProviderSettings`/system default are intentionally installation-wide, editable
  only from organization 1 with `ai.manage`. **Credentials are environment-backed**,
  not plaintext SQLite records. There is no per-tenant provider/key vault.
- `AiUserPreferences` is keyed only by account. Its model choice can follow a user
  across organizations; tenant defaults/preferences need independent storage.
- The Axios interceptor unconditionally replaces an explicit organization header.
  This defeats the header selector's `/auth/me` validation of a different tenant;
  fix it and cover the real selector interaction in browser tests.
- Broad `IsSystem`/`IgnoreQueryFilters` escape hatches exist for maintenance and
  permission resolution. New HTTP code must not use them to bypass scope.

## Knowledge and RAG/ReRAG

Knowledge is stored as `KnowledgeBaseItem` rows, not separate libraries/folders.
The default local context builder searches at most 200 recent active tenant rows
using bounded textual matching and selects up to eight excerpts. It combines
authorized per-user history and optional uploaded document context. This is not
semantic search. Atlas has ten opt-in, insert-only synthetic documents.

The advanced path chunks documents, generates ONNX embeddings, uses a Qdrant
organization payload filter, then revalidates active tenant item IDs in SQLite
**before reranking**. ONNX reranks retrieved headers and source rows are loaded
again through tenant filters. Old vector points without organization metadata
are excluded; explicit reindexing is required. Local fallback does not pretend
to be vector RAG. Retrieval data must remain untrusted context, never security
instructions. Some advanced retrieval logs include question/title text: remove
or redact before handling personal/confidential data in a managed service.

No document change-history/version UI, public knowledge template catalog,
retention/deletion workflow, or per-document ACL exists beyond organization scope.
LGPD readiness and resistance to all prompt injection are not established.

## Models, data sources and output

DeepSeek, OpenAI-compatible and Ollama adapters already support explicit model
selection, streaming, titles and token reporting. Server origin allowlists and
disabled redirects restrict configured destinations. Secret values never belong
in frontend settings. Usage is persisted with source/status and optional versioned
price estimates; it is not a billing ledger or enforceable quota system.

Data Connectors implements create/update/enable, test/discovery, table selection,
organization-specific approved server profiles, group grants and status. Trino
credentials are environment references; endpoints are IP-pinned, restricted and
TLS-required outside the isolated demo. Public profiles require provenance and
license metadata. Four financial SQL plans are reviewed/closed, read-only and
bounded; the obsolete arbitrary SQL entry point fails closed. The demo Trino
Memory catalog is volatile and must be reloaded after restart.

Chat routes documentary, analytical and hybrid requests, or requests clarification.
Sources, reference date, verifiable table, numeric chart and sandboxed Mermaid
appear in the existing chat. This is not a general tool-executing agent.
WhatsApp Business Platform and other channel adapters do not exist. No browser
automation-based WhatsApp integration should be introduced.

## Tests, packaging and ARM64

Existing .NET executable test projects cover sessions, titles, usage, providers,
organizations/migrations, Atlas seed, connectors, business chat and access seed.
RagSmoke covers vector components with doubles. Python smoke runs a real API and
temporary SQLite, with LLM/OCR doubles; real Trino is optional. Browser scripts
use Playwright, synthetic APIs and optional axe. These do not certify production
security or native OCR/vector inference. There was no tracked pipeline at audit
time. Builds/tests to run for this increment are recorded separately, not assumed.

Previous registry manifest inspection confirmed linux/arm64 for .NET 8 bases,
Node 22 Alpine, Nginx, Qdrant and the pinned Trino digest. Local NuGet ONNX and
tokenizer packages contain ARM64 runtimes. Intel MKL Redist is x64-only; current
ML formulas do not use a trained MKL workload. OCR uses system executables, not
the x86/x64 NuGet Tesseract engine. Full builds/runtime and resource budgets on
ARM64 remain unvalidated. Compose has no PostgreSQL dependency and only Trino
has an explicit memory limit. See `infra/oracle/README.md` for detailed evidence.

## Confidentiality and operational findings

The audit scanned tracked text files only; no private backups, `.env`, database
or private key contents were read/copied. Pattern scan found no private-key blocks
or provider-token-shaped values. Two historical documentation sentences contain
legacy employer identifiers; generalize those references without reproducing
them here. Private IPv4 hits belong to the synthetic Atlas Docker network, not
evidence of a third-party production endpoint. This is a limited content scan,
not certification of Git history, images, ownership or all possible secrets.

Ignored `.private-originals`, databases, logs and local OCI artifacts must never
enter contributions or exports. The source-export allowlist predates infra/tests
and can reject the intentional demo network; review its output rather than claim
it guarantees safe distribution. Review historical credentials/ownership before
licensing. No license will be added automatically.

The previous local OCI retry process was stopped by verified PID/start identity
at the start of this task, to respect the instruction not to modify Oracle
resources. No OCI API was called and no remote resource was changed in this audit.

## First bounded increment

Reuse the current tenant middleware/query filters and add organization management,
explicit membership policies with a documented legacy policy, organization model
defaults and organization/account preferences, plus isolation tests. Preserve
legacy database content and installation-owned secret configuration. Document
future modules rather than build SaaS, payment or external-channel scaffolding
that does not yet have a working implementation.

## State after the bounded increment

Implemented on top of the audited baseline: organization provisioning and member
policy administration, protected final Owner, organization model defaults and
personal model preferences, fixed explicit-header selection, a pt-BR workspace
page and a public versioned technical-support template import. Migration 007 is
additive; old custom groups keep their grants and legacy account model preferences
are assigned only to organization 1. Two legacy identifiers were generalized in
historical documentation. Question/title logging in the audited vector retrieval
path was removed; a wider telemetry review remains necessary.

Tests now include real JWT/HTTP/temporary-SQLite workspace isolation and revocation,
v6 migration/rollback/restore, foreign vector payload rejection before reranking,
and browser interaction with the actual organization selector. A disposable local
Qdrant plus existing ONNX models also passed the advanced retrieval smoke on the
Windows host; this is **not** ARM64 validation. The migration runner now consumes
all SQLite batch results before committing, avoiding the later-statement error
suppression reproduced during failure injection (see
[upstream issue](https://github.com/dotnet/efcore/issues/32740)).

Still missing after that increment: consenting invitations, independent per-tenant provider credentials
and model allowlists, document version history/libraries, agent definitions,
external channel adapters, enforceable quotas/billing, durable audit retention,
mandatory CI, production hardening and an owner-approved license. The existing
running installation was not upgraded or reset as part of these tests.

## Follow-up: invitations and ownership (2026-11-08)

Existing-account, Owner-issued bearer invitations now support explicit consent,
single-use 24-hour expiry, revocation, issuer-version invalidation and Member/Viewer
roles. Atomic ownership transfer has confirmation/version checks; organization 1
also grants installation administration. Migration 008 is additive and tested
against real v7 fixtures with rollback/backup restoration. No global account lookup
or email delivery exists. Links are not recipient-address-bound. See
[security/operation contract](../features/ORGANIZATION_INVITATIONS.md) and
[test/file report](../delivery/INVITATIONS_REPORT.md). The running installation was
not migrated during these tests. Independent tenant credentials/model allowlists,
verified onboarding, durable audit/quotas and public-hosting hardening remain open.
