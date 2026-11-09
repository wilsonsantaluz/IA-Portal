# Official WhatsApp contracts — research and unresolved gates

Research date: 2026-11-08. No Meta account login, app registration, credentials,
billing change, live messaging or deployment was performed. This is a bounded
research record, not confirmation of this installation's eligibility.

## Accessible primary evidence

Meta maintains an official Cloud API examples repository with webhook and
signature examples. The corresponding protocol uses a verification GET and
signed webhook POST. We implemented the protocol independently in .NET, with
bounded raw-body parsing, constant-time signature comparison and separate app
secret/verification token settings. No sample code or its license was imported.
[Meta Cloud API examples](https://github.com/fbsamples/whatsapp-api-examples),
[signature sample](https://github.com/fbsamples/whatsapp-api-examples/blob/main/signature-validation-with-webhooks-payloads/app.py).

Meta's Tech Provider reference application demonstrates Embedded Signup,
WABA/phone management and messaging; its prerequisites include a developer account
and verified business. It is evidence of the official onboarding direction, not
proof that IA-Portal has received any permission or approval. Its Next.js/Auth0/
Ably/Postgres/Vercel dependencies were not adopted here.
[Meta Tech Provider reference](https://github.com/fbsamples/business-messaging-sample-tech-provider-app).

The accessible WhatsApp Business pricing page describes delivery-based pricing
varying by market and category, names marketing/utility/authentication/service,
and describes a customer-triggered 24-hour service window. It also displays
service/utility and 72-hour entry-point concessions. **We do not treat that page
as a verified current account allowance or a promise of zero cost**: the detailed
dated rate-card page was inaccessible during this audit. No prices or free-tier
counts are encoded in this implementation.
[WhatsApp pricing](https://whatsappbusiness.com/products/platform-pricing/).

## Documentation access limits

The developer overview, webhook, Embedded Signup and Coexistence endpoints below
returned HTTP429/access errors; the detailed pricing endpoint returned a login
page. Search results/third-party descriptions are not sufficient to certify the
current account-specific contract. An authorized operator must verify these in
the Meta dashboard before live integration. The implementation and UI therefore
fail closed for real sending and mark onboarding as unverified.

| Topic | Official reference to verify | MVP decision / unresolved gate |
| --- | --- | --- |
| Cloud API | [Overview](https://developers.facebook.com/documentation/business-messaging/whatsapp/overview) | Text webhook protocol tested with signed synthetic envelopes. No real sender, token validation or provider connectivity claim |
| Webhook contracts | [Webhooks](https://developers.facebook.com/documentation/business-messaging/whatsapp/webhooks/overview), [Graph verification](https://developers.facebook.com/docs/graph-api/webhooks/getting-started) | Verify GET, raw-body HMAC POST; reject unknown routes; ignore unsupported fields rather than guessing their schema |
| Embedded Signup | [Overview](https://developers.facebook.com/documentation/business-messaging/whatsapp/embedded-signup/overview) | UI states pending. Must bind authorized returned WABA/phone assets to authenticated tenant, handle server token exchange and revocation; no fake connect button |
| Business App Coexistence | [Business app onboarding](https://developers.facebook.com/documentation/business-messaging/whatsapp/embedded-signup/onboarding-business-app-users) | Eligibility is not assumed for any account/country/number/app version. Verify permitted flow, limitations, consent and approval before enabling |
| Multiple numbers | [Cloud API reference](https://developers.facebook.com/documentation/business-messaging/whatsapp/overview) | Local architecture supports many connections/numbers per organization. Verify actual account limits/ownership; one app-secret configuration per installation in this MVP |
| App-origin messages | [Coexistence onboarding](https://developers.facebook.com/documentation/business-messaging/whatsapp/embedded-signup/onboarding-business-app-users) | `smb_message_echoes` is recognized only as an ignored field. No app-event persistence or reply triggering is claimed |
| History/contact sync | Same Coexistence reference | Exact eligibility, scope, consent, initiation window and schema not verified. No full-history fetch/import promise. `history` and state-sync fields are ignored |
| Window/templates | [Pricing and policies](https://whatsappbusiness.com/products/platform-pricing/) | Conservative 24-hour bound for free-form simulation; expired work held. Approved template initiation/out-of-window sending pending |
| Rates/allowances | [Detailed pricing](https://developers.facebook.com/documentation/business-messaging/whatsapp/pricing) | Login required; confirm effective date, market/currency/category, tiers, exemptions and any service allowance before budgeting. Amounts remain unknown |
| SaaS/Tech Provider | [Permissions](https://developers.facebook.com/documentation/business-messaging/whatsapp/permissions), [Meta reference app](https://github.com/fbsamples/business-messaging-sample-tech-provider-app) | Verify business/app review, Advanced Access and `whatsapp_business_messaging`/`whatsapp_business_management` applicability, live-mode limits, business asset grants, partner terms, privacy/deletion callbacks and consent. Not completed here |

## Live integration acceptance gates

1. Authorized operator verifies the official current contract, account eligibility,
   asset ownership, permissions, allowed use cases and dated rates/allowances.
2. Supply credentials through an approved secret manager/environment reference;
   no plaintext token in SQLite, API output or browser. Design rotation/revocation
   and multiple-app scoping before managed SaaS onboarding.
3. Review HTTPS ingress, webhook abuse limits, request/query-log redaction, data
   retention/erasure and audit retention. This task created no public endpoint.
4. Add reviewed Cloud sender, send-intent/outbox lease, bounded Retry-After/backoff,
   ambiguity reconciliation and status/pricing ingestion. Keep app echoes/history
   separate from inbound customer messages and suppress automation loops.
5. Confirm the service window, recipient consent, approved template rules and
   message caps before sending. Separate Meta delivery charges, model usage and
   infrastructure; unknown is not zero. Reconcile provider charges, never infer
   permanent prices from examples or a temporary trial balance.
6. Run an explicitly authorized test-account end-to-end verification, including
   failure/replay/status/revocation/Coexistence events. Only then enable live sending.
