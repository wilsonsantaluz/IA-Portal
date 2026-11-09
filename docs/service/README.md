# IA-Portal Service — channel MVP

**Local update2026-11-09:** the channel module is now running in the existing local
installation with preserved data, migrations7→9 and a safe Meta setup guide/status
card. Start with [META_TEST_SETUP.md](META_TEST_SETUP.md) for the chosen test-number
flow, private parameters and the remaining live-connection gates.

This is a module of the existing .NET gateway and React application, not a new
microservice or commercial edition. The first delivery is deliberately local:

**simulated inbound → durable history → authorized textual retrieval → extractive
agent → simulated response**. No Meta or LLM credentials are required for it.
The Cloud API webhook is implemented for authenticated text reception, but real
outbound delivery, Embedded Signup and Coexistence are **not completed**.

Read [the architecture](ARCHITECTURE.md), [official-contract research and access
limits](WHATSAPP_RESEARCH.md) and [delivery/test report](../delivery/CHANNELS_REPORT.md).

## Run the local demonstration

Preserve the current installation's `.env`, database, volumes and Compose overrides.
After reviewing the branch, the operator can update only the application images:

```powershell
docker compose build ai-gateway portal-front
docker compose up -d --no-deps ai-gateway portal-front
```

These commands were executed in the later2026-11-09 local update. Gateway startup
creates a pre-upgrade backup and applies migration009 when it is pending.
No reset, `down -v`, new database service or paid resource is needed.

1. Sign in as Owner/Admin and open **Canais** (`/channels`). Custom groups need
   `channels.manage`; creation also requires `kb.view`. Member/Viewer do not gain
   channel administration automatically.
2. Ensure the current organization has a synthetic KB document, e.g. a warranty
   policy. Register **Simulador local**, a display name and synthetic account/phone
   IDs. Select only the documents this external-facing agent may disclose.
3. Automatic replies start **off**. Enable **Respostas automáticas simuladas**
   explicitly. This is not a WhatsApp connection or an LLM call.
4. Submit a matching synthetic question. The worker polls every two seconds.
   Click **Atualizar histórico**, select the conversation, and inspect the answer,
   sources and delivery state. Missing evidence produces a clarification.
5. Enable human handoff to pause the bot, or disable the agent/channel. Held
   messages are not automatically replayed when enabling it later. This protects
   against an unexpected backlog of automatic replies.
6. Edit/save the response cap and content-retention period. History is paginated
   in 50-row pages. No costs are invented when measurement is absent.

The CLI is optional. Set `IA_PORTAL_TOKEN` privately in your local environment,
then use a **simulator** connection ID (never a real contact):

```powershell
python scripts/simulate_channel.py <connection-uuid> --organization <organization-id> --message-id synthetic-001 --text "Como funciona a garantia?"
# Repeat the same ID to check deduplication. Remove the token from the environment afterward.
Remove-Item Env:IA_PORTAL_TOKEN
```

The CLI permits only loopback HTTP origins and reads the JWT from the environment,
not command arguments. The UI is the simplest complete local demonstration.

## Reproduce validation without changing the portal database

```powershell
dotnet build ai-gateway/AiGateway.csproj
dotnet run --project tests/Channels
python scripts/smoke_channels.py
python scripts/smoke_organizations.py
python scripts/smoke_local.py --atlas
npm run typecheck --prefix front
npm run build --prefix front
```

The channel smoke launches a temporary gateway/SQLite database with synthetic JWT
and webhook secrets, exercises the actual signed endpoint and background worker,
then removes only its own temporary fixture. No real Meta/LLM requests are made.
Browser tests: run Vite on 127.0.0.1:15173, install/use local Playwright tooling,
set `IA_PLAYWRIGHT_MODULE`/`IA_CHROME_PATH` as needed, optionally `IA_AXE_MODULE`,
then `node tests/ui/channels.cjs`. They use mocked APIs; API smoke covers actual
authorization/persistence separately.

## Future Meta test-account setup (not performed here)

1. Use your own Meta developer/business account. Follow the current official
   [Cloud API entry point](https://developers.facebook.com/documentation/business-messaging/whatsapp/overview)
   and the app dashboard to add WhatsApp and obtain its test WABA/phone IDs.
   Add/verify only consenting test recipients. Do not upgrade billing or create a
   paid resource as part of this MVP. Dashboard flows and eligibility can change.
2. Inspect required permissions, app mode, token scopes/expiry and asset ownership.
   A SaaS serving other businesses requires its own reviewed Tech Provider and
   Embedded Signup flow; a development test account does not establish approval.
3. For a separately authorized reception test, provide **server environment**
   configuration, never browser fields or checked-in secrets:

   | Environment key | Meaning |
   | --- | --- |
   | `WhatsApp__AppSecret` | Meta app signing secret, not an access token |
   | `WhatsApp__VerifyToken` | Locally chosen unpredictable verification token |
   | `WhatsApp__Routes__PHONE_ID__OrganizationId` | Operator-verified target organization |
   | `WhatsApp__Routes__PHONE_ID__AccountId` | Corresponding verified WABA ID |
   | `Channels__OrganizationLimits__ORG_ID` | Optional monthly response-reservation cap; default 1000 per organization |

   Replace PHONE_ID/ORG_ID with actual IDs in **environment variable names**. No
   values are persisted in channel rows. Current scope is one Meta app per portal
   installation; a multi-app secret vault requires a later increment. Compose
   operators must explicitly pass these values via a private override/secrets
   mechanism; merely adding them to the host `.env` does not inject them into an
   existing container. This delivery does not alter `.env` or Compose.
4. Register a **WhatsApp Cloud API · somente recebimento** connection matching the
   operator route. Another organization cannot claim a WABA/phone binding. Missing
   operator configuration is rejected, not shown as a successful connection.
5. Meta requires a reachable HTTPS callback for live webhook delivery. Public
   ingress/TLS/proxy configuration and testing need a separate authorized task.
   Use callback `/api/webhooks/whatsapp`, verification token and subscription to
   message events. Do not expose internal services or add an unattended tunnel.
   Redact verification query strings and request bodies at every proxy/log layer.
6. Confirm signature, routing, durable receipt and duplicate handling with the
   consenting test account. Automatic **real** sending stays blocked by backend
   policy. No access-token editor or Cloud sender exists in this delivery.

## Operational limits

- Single-node SQLite worker; do not run multiple gateway replicas as a distributed
  delivery system. Transactions make local simulation durable and deduplicated.
- Retries apply only to the deterministic simulator's explicitly transient errors:
  30/60-second backoff, maximum three attempts. Real network sends cannot safely
  reuse this algorithm without handling ambiguous accept/timeouts and reconciliation.
- 24-hour service window uses the provider message timestamp, not webhook receipt
  time. Expired messages are held. Approved template sending is not implemented.
- Per-number limits count reserved outbound rows (including failed/held results)
  in UTC months, conservatively. Organization-wide reservations have an additional
  operator cap. This is a message cap, not an invoice or an LLM-token budget.
- Retention redacts message text and source metadata after 1–365 days (default30).
  Contact/provider IDs and delivery tombstones remain for deduplication. Full
  personal-data erasure, legal retention and backup deletion need a separate policy;
  this is not a claim of LGPD compliance. Disabling an entire organization pauses
  its worker, including retention; operator lifecycle handling remains necessary.
- Handoff pauses automation; a complete human agent inbox with manual outbound
  messages, queues and staffing assignments is still pending.

No production deployment, Oracle change, paid resource, real credential use,
browser-driven WhatsApp automation or license change was performed.
