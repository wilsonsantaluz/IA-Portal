# Security policy

IA-Portal is a development-stage self-hosted platform, **not production-certified**.
No released-version security support window or response SLA has been established.
Use the current reviewed source and do not expose confidential tenant workloads
without an independent deployment/security assessment.

## Reporting

If this repository enables GitHub private vulnerability reporting, use **Security
→ Advisories → Report a vulnerability**. Do not post credentials, real documents,
cross-tenant content or exploit details in a public issue. If private reporting
is unavailable, open an issue asking for a private reporting channel without
sensitive details; no dedicated security email is claimed here.

Include affected commit/version, component, synthetic reproduction, expected vs
actual authorization, impact and redacted logs. Do not test other organizations,
customers or public instances without authorization. Never send private keys.

## Boundaries

- JWT validation refreshes active account, membership and permission state from DB.
- Tenant headers/bodies and retrieved documents cannot grant access or tools.
- SQLite filters, write guards and conversation ownership protect distinct scopes.
- Vector payload filtering is followed by relational authorization before reranking.
- External SQL is closed/read-only/bounded; destinations and group grants are explicit.
- API keys remain server environment secrets. Tenant-encrypted key storage is planned;
  there is no plaintext key editor or secret-returning API.
- Imported templates are untrusted evidence/reference, not executable agent policy.

Known work remains: authentication rate limits/MFA, production CORS/TLS/ingress,
OCR CPU/memory/temp-file limits, prompt and retrieval-log redaction, durable access
audit/retention, per-document ACL, tenant secret vaults, enforceable usage budgets,
data export/deletion and recovery drills. LGPD compliance is not implied by tenant
filters. Usage estimates are not billing guarantees. Verify provider processing,
consent/legal basis and retention before sending any personal data externally.

Ignore rules are not a secret manager or a history scrubber. Treat historical
credentials as requiring revocation/rotation, not merely removal from a file.
Review model/dependency ownership/licenses independently. No Oracle change or
production deployment is authorized by running local tests.
