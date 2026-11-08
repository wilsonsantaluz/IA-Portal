# Product roadmap

The target is one open-source/self-hosted core and a future managed offering.
License choice and ownership review remain with the project owner. There is no
commercial fork or payment gateway in this plan's first delivery.

| Increment | State | Verifiable outcome |
| --- | --- | --- |
| Current-state audit | Delivered | `architecture/CURRENT_STATE.md`, evidence separated from plans |
| Organization workspace | First bounded delivery | Existing tenant isolation reused; create organization, Owner/Admin/Member/Viewer, protected final Owner, tenant model defaults/preferences, real HTTP and migration tests |
| Knowledge template foundation | First bounded delivery | One versioned technical-support template, insert-only import into current tenant; no fake connectors/tools |
| Setup journey | Specification + reusable first form | `features/SETUP_WIZARD.md`; organization create form reused by workspace page |
| Community governance | Draft documentation | Contribution/security/conduct guides; Apache 2.0 vs AGPLv3 decision pending |
| Organization invitations | Planned | Explicit consent, expiring one-time invitation, no cross-tenant user enumeration or automatic account takeover |
| Tenant model/credential policy | Planned | Per-tenant approved model allowlists, encrypted/vault-backed secret references and rotation; no browser-held API keys |
| Knowledge lifecycle | Planned | Libraries, version history, index jobs, deletion/retention, per-document ACL |
| Extensible data access | Planned | Adapter contracts exercised by a real PostgreSQL/SQL Server/REST source, read-only grants and SSRF controls |
| Agents | Planned | Configurable/versioned agents with reviewed tools, budgets and audit events; preserve existing conversations |
| Official channel integrations | Planned | WhatsApp Business Platform webhook adapter with identity binding; no browser automation |
| SaaS operations | Planned | Tenant provisioning, entitlements/limits, trustworthy usage ledger, monitoring and recovery; payment provider selected separately |
| CI and supply chain | Planned | Required API/frontend/tests, dependency/secret scanning, reviewed ARM64 image builds; no tracked workflow existed at audit time |

Before public multi-tenant hosting: complete the security/privacy/operational work
in `architecture/CURRENT_STATE.md` and `SECURITY.md`. Local passing tests do not
establish production readiness. Native ARM64 OCR/ONNX/MKL behavior and realistic
resource budgets remain gates. Detailed historical delivery logs remain under
`delivery/`; this roadmap describes the new product direction.
