# Reusable setup journey — UX specification

The same journey serves Community self-hosting and future Cloud onboarding.
Only organization provisioning and a reusable `OrganizationForm` exist now.
There is no eight-step wizard falsely reporting unimplemented integrations.

| Step | UX / validation | Delivery state |
| --- | --- | --- |
| 1. Organization | Name, immutable unique lowercase slug, creator becomes Owner; installation permission checked server-side | Functional form in Organizações |
| 2. Business segment | Generic category, no personal/customer data; skipping is allowed | Spec only |
| 3. Knowledge template | Show version, document structure, examples and permitted capabilities before import | Technical-support v1 import works independently |
| 4. AI provider/model | Show configured server-approved models, external processing/cost notice; never request browser-held API keys | Organization default selection works; tenant secret setup planned |
| 5. Knowledge | Review/edit imported documents or create normal KB rows; show indexing status honestly | Existing KB; dedicated wizard state planned |
| 6. Data source | Optional; only actual approved profiles, authorized read-only test; explicitly label absent adapters planned | Existing Trino administration; other adapters planned |
| 7. Test question | Reuse existing chat, show sources/model/usage and missing context; do not invent success | Existing chat; wizard linking planned |
| 8. Finish | Summarize completed/skipped/incomplete steps with next actions | Spec only |

The expanded wizard must be resumable with organization-scoped progress,
accessible keyboard navigation, server validation, loading/error states and
clear retry behavior. Closing it must not lose imported documents or recreate
organizations. Never persist secrets in wizard state. Changing organizations must
cancel/clear old progress requests. Do not add a second chat or billing gateway.

## Public template contract

Source: `ai-gateway/Application/Templates/technical-support.v1.json`.
`schemaVersion`, stable `id`, immutable content `version`, localized name/description,
category, reference behavior instructions, document keys/content, example questions
and allowed capability names are versioned together. Applying a template inserts
missing tenant documents through the existing knowledge service and never
overwrites human edits or reactivates inactive entries. Reapply is idempotent.
Vector mode uses the existing indexer; relational authorization remains mandatory
for orphaned points if external indexing and SQL cannot commit atomically.

Instructions/capabilities are declarative reference content, **not** executable
agent configuration or authorization grants. Imported text never overrides
security rules. No SQL tool, connector or external channel is enabled by import.

Catalog direction: technical support is complete; sales assistance, real estate,
clinic/service-provider and enterprise-data consultation templates are planned.
Do not create placeholder manifests that claim those templates work. No real
patient, customer or employer data belongs in public templates.
