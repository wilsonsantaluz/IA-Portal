# Invitations and ownership delivery — 2026-11-08

Branch: `feature/organization-invitations`, based on the delivered Community
workspace branch (`cef803c`). At the initial fetch, `origin/main` still pointed to
`90b5b86`, despite the merge notification. Previous work was preserved; no main
merge, force push or deployment was performed. The initial working tree was clean.

## Delivered and files

- Gateway: new `OrganizationInvitation` entity, `OrganizationInvitationService`,
  `OrganizationInvitationsController` and migration
  `008_OrganizationInvitations.sql`; existing `PortalDbContext`,
  `ChatSchemaMigrator`, `Program`, `OrganizationWorkspaceService` and
  `OrganizationsController` updated for mapping, registration and atomic transfer.
- Web: new `OrganizationInvitations`, `AcceptInvitationPage` and
  `auth/switchOrganization.ts`; existing `OrganizationsPage`, `LoginPage`,
  `HeaderBar`, `App` and API interceptor updated for consent, login return,
  explicit ownership confirmation and validated organization switching.
- Tests: new `tests/Invitations` and `tests/ui/invitations.cjs`; extended
  `scripts/smoke_organizations.py` and organization browser fixture. Historical
  fixtures in Workspace, Connectors, ChatTitles, Usage and
  `Fixtures/PreOrganizationContext.cs` account for the new table.
- Documentation: feature contract, this report, current/target architecture,
  self-hosting, roadmap, documentation index, AS_IS audit and implementation log.

See [the feature contract](../features/ORGANIZATION_INVITATIONS.md) for exact API,
security boundaries and operator instructions. Links are existing-account bearer
capabilities, not recipient-email-bound invitations. Preview is not consent.

## Migration and preservation

Version 008 adds a table/indexes only, with unchanged older checksums. Automatic
pre-upgrade backup, idempotency, foreign keys, checksum/schema validation and late
DDL failure rollback are covered using temporary real v7 databases. Backup
restoration followed by upgrade is tested. Operational downgrade means restoring
a complete pre-upgrade backup with the previous application version at a new
path, reconciling newer writes; no destructive in-place down script is supplied.

The existing installation/database, `.env`, Compose profiles and remote OCI
resources were not modified. The independent VM capacity-retry process was not
stopped. Application tests do not establish current OCI capacity or VM success.

## Commands and observed results

| Check | Result |
| --- | --- |
| `dotnet build ai-gateway/AiGateway.csproj` | PASS, existing compiler warnings |
| `dotnet run --project tests/Invitations` | PASS: v7 migration/repeat/backup/restore/late-DDL rollback; expiry, revocation, hash-only storage, issuer version, Owner checks, tenant-write rejection, no reactivation/downgrade/group grants, concurrent single consumption, atomic transfer |
| `dotnet run --project tests/{Workspace,Organizations,AtlasKnowledge,Connectors,Providers,ChatSessions,ChatTitles,Usage,BusinessChat}` (each separately) | PASS |
| `dotnet run --project tests/access/SeedSmoke` | PASS |
| `python scripts/smoke_organizations.py` | PASS: actual JWT/HTTP/SQLite consent, tenant isolation, metadata-only preview, revocation, role overposting denial, stale/atomic transfer, issuer invalidation, no code logging |
| `python scripts/smoke_local.py --atlas` | PASS: sessions/titles/usage/providers/admin/organizations/connectors/hybrid chat/restart; local HTTP LLM/OCR/Trino doubles, no paid calls |
| `npm run typecheck --prefix front` | PASS |
| `npm run build --prefix front` | PASS, existing large-chunk warning |
| `node tests/ui/invitations.cjs` | PASS: create/revoke, one-time display, confirmation/cancel, failed-login retry/return, fragment removal, no localStorage/API-URL code, explicit acceptance/switch, light/dark, 390/320px, axe main |
| `node tests/ui/organizations.cjs` | PASS: workspace/selector/defaults and themes/mobile/axe |
| `node tests/ui/atlas.cjs` and `node tests/ui/atlas-security.cjs` | PASS: existing chat/connectors, numeric charts, Mermaid sandbox/fallback and cross-tab tenant reset |
| `git diff --check` | PASS |

Browser tests ran against a disposable Vite server on 127.0.0.1:15173 with API
fixtures, Chrome, Playwright and axe from local temporary tooling. Browser fixtures
are complementary to real API authorization tests. Ignored evidence is under
`artifacts/invitations-tests` and `artifacts/invitations-ui`. No new ARM64/native
OCR/ONNX, real email or real remote Trino validation was performed in this increment.

## Run locally and next increment

For an existing installation, retain its `.env` and selected Compose overrides:

```powershell
docker compose build ai-gateway portal-front
docker compose up -d --no-deps ai-gateway portal-front
```

These operator commands were **not executed against the running installation**.
Gateway startup backs up/applies pending migrations through v8. Open the existing
portal, choose **Organizações**, and use an Owner to generate a link or transfer
ownership. Use a second existing synthetic account to verify consent; never share
a real capability publicly. Organization 1 transfers include installation-wide
AI administration privileges and display a specific warning.

Next: tenant model allowlists and an encrypted/vault-backed credential policy.
Verified-address onboarding/delivery, MFA/recovery, durable audit/abuse controls,
document lifecycle, agents/channels, quotas/billing, CI and public-hosting hardening
remain pending. No separate commercial edition, payment gateway or production
deployment was introduced.
