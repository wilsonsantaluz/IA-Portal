# Organization invitations and ownership

This increment reuses existing accounts, JWT, tenant authorization, workspace UI
and role policies. It adds no registration service, email provider or SaaS edition.

## Operation

1. An active **Owner** opens **Organizações** in the intended organization.
2. Generate a Member or Viewer link. It is shown once; share it privately.
3. The recipient signs in with an existing account, reviews the displayed account,
   organization and role, then explicitly accepts or goes back.
4. Acceptance switches to the server-validated organization. Preview grants nothing.
   Owners can revoke pending links.

An invitation is a **bearer capability**, not an email-address-bound invitation:
any authenticated holder can accept it. The account must already have an active
membership to sign in. Signup, verified recipient-bound invitations, delivery,
password recovery and MFA remain separate work. No email is sent by this feature.

Links expire after 24 hours, are consumed once and allow only Member or Viewer.
An organization can have at most 20 effective pending links. Existing active
members keep their roles; inactive memberships are never reactivated. Acceptance
does not grant custom groups or connector access, or copy documents/conversations.

## Security boundary

- Codes contain 256 random bits; SQLite stores only SHA-256 hashes. Raw codes are
  returned once, held in component memory and put in a URL **fragment**, never an
  API path/query or localStorage. Preview removes the visible fragment/history
  entry. Authenticated POST bodies carry codes. Operators must keep telemetry
  and request/response-body capture from collecting these capabilities.
- Invalid, expired, revoked, consumed or invalidated codes receive a generic
  unavailable response. Anonymous preview is denied.
- Cross-tenant lookup is a deliberate, bounded capability path. The stored code
  determines the target, never a body/header tenant ID. Ordinary entity queries
  and management retain tenant filters.
- Issuer account, organization and Owner membership must remain active with the
  exact membership version recorded at issuance. Demotion/removal/membership
  changes invalidate outstanding codes; later re-promotion does not revive them.
- Consumption and membership creation share a transaction. Conditional updates
  prevent concurrent double acceptance. Revocation uses optimistic versions.
- Audit events contain actor/organization/invitation IDs, no codes/hashes. Durable
  audit retention and broader abuse controls remain future work.

## Ownership transfer

An Owner selects another active member with an active account and confirms the
transfer. Both membership versions must match. The target becomes Owner and the
actor becomes Admin atomically. Stale submissions change neither role. Other
organizations/custom groups are unaffected. The final active Owner is protected;
generic role edits cannot promote a new Owner.

**Organization 1 also controls installation administration.** Its new Owner gains
installation-wide AI configuration privileges. The confirmation dialog warns
about this. Tenant-owned provider keys and model allowlists remain unimplemented.

## API and persistence

| Method/path | Contract |
| --- | --- |
| GET `/api/organizations/current/invitations` | Owner-only metadata, latest 100, no codes/hashes |
| POST `/api/organizations/current/invitations` | Owner creates `{role: "Member"}` or Viewer |
| POST `/api/organizations/current/invitations/{id}/revoke` | Owner, current tenant, `{version}` |
| POST `/api/organization-invitations/preview` | Authenticated `{token}`, no membership change |
| POST `/api/organization-invitations/accept` | Authenticated `{token}`, explicit consent |
| POST `/api/organizations/current/transfer-ownership` | Owner, `{targetUserId, ownerVersion, targetVersion}` |

Migration **008_OrganizationInvitations.sql** adds one table/indexes. Earlier
checksums are unchanged. Startup backs up before upgrade, validates schema/foreign
keys and records version 8 transactionally. Existing rows are preserved. Tests
exercise v7 upgrade, repeat startup, backup restore and late-DDL failure rollback.
Operational rollback uses the previous binary and complete pre-upgrade backup at
a new path. No live table-drop downgrade exists; reconcile subsequent writes.

Run `dotnet run --project tests/Invitations`,
`python scripts/smoke_organizations.py` and `node tests/ui/invitations.cjs` with
Playwright/browser configured. Tests use temporary SQLite, synthetic identities
and browser API fixtures, not real invitations or paid providers.
