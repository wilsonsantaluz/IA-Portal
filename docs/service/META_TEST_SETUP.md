# Meta test-number setup and local update

Updated2026-11-09. The owner chose **Meta's test number** as the first integration
path. No personal/business number is migrated or browser-automated. Local code is
ready for simulated channels and signed text reception; **real outbound sending,
Embedded Signup and Coexistence are not implemented by this increment**.

## What is needed from Meta

Registration and app management are separate entry points. The owner-supplied
Cloud API Get Started guide distinguishes them; the app dashboard is not the
registration URL. The [getting-started guide](https://developers.facebook.com/documentation/business-messaging/whatsapp/get-started)
describes the WhatsApp use case, API Setup, test phone number ID and Messaging
account ID. Follow the actual dashboard labels when it uses Messaging account
instead of WABA. The supplied sample API version is an example, not an automatic
version upgrade. Automated checks returned a login gate/429; authenticated
screens were not independently inspected.

1. Complete [developer registration](https://developers.facebook.com/async/registration/)
   first, if needed. Then open the [Meta App Dashboard](https://developers.facebook.com/apps).
   Create an app with the WhatsApp use case/product and follow its setup wizard.
   Associate the business portfolio when requested. Dashboard labels and eligibility
   requirements can vary; account registration/terms/verification must be performed
   by the account owner, not bypassed by IA-Portal.
2. Open the WhatsApp API setup/testing screen. Use the **test number supplied by
   Meta**. Record App ID, WhatsApp Business Account ID (WABA) and Phone Number ID.
   Phone Number ID is an asset ID, not the telephone number in international format.
3. Add/verify your consenting recipient phone in the test setup when required.
   The dashboard provides a test access token and message-testing controls. Tokens
   can expire/revoke; use the lifetime and permissions shown for your actual app.
   Do not treat a temporary test token as a permanent production credential.
4. Obtain the **App Secret** from the app's private settings for webhook signature
   validation. Choose a separate random **Verify Token** locally; register that
   same verification value in the Meta callback settings.
5. Use the webhook URL shown in **Canais → Conectar WhatsApp oficial** once a real
   public HTTPS origin is configured and routed to the local gateway. Subscribe
   to message events using the actual app's dashboard instructions.

The developer guide returned a login page during this audit. We are not claiming
to have inspected the user's dashboard, verified their business or generated a
token. Accessible primary references confirm the official app/Embedded Signup/
WABA/phone management direction:
[Meta's reference application](https://github.com/fbsamples/business-messaging-sample-tech-provider-app),
[Cloud API entry point](https://developers.facebook.com/documentation/business-messaging/whatsapp/get-started).
SaaS onboarding for other businesses needs separate app review/permissions and
business eligibility checks; a personal test app does not establish that approval.

## Frontend versus backend parameters

| Parameter | Frontend | Backend |
| --- | --- | --- |
| App ID | Read-only public metadata | Operator configuration |
| WABA ID / Phone Number ID | Connection registration and tenant-authorized metadata | Must match an independently operator-approved tenant route |
| Callback URL | Can be displayed/copied | Must be an explicitly configured public HTTPS origin plus the fixed webhook path |
| Access token | Never returned or stored in browser | Private environment only; current sender is unavailable, so its presence is preparation, not a live-send claim |
| App Secret | Only a configured/pending boolean | Private environment, raw-body HMAC validation |
| Verify Token | Only a configured/pending boolean | Private environment, constant-time webhook verification; operator enters it in Meta privately |

No `VITE_*` token, localStorage credential, plaintext credential SQLite row or
global account lookup is added. The setup endpoint requires `channels.manage`,
returns an explicit safe DTO and only exposes numbers approved for the active
validated organization. It makes no network call to Meta. **Configuration filled**
does not mean **provider connection verified**.

## Private local configuration

A blank template was placed outside Git at:

`C:\oci\ia-portal\whatsapp-local.env`

The checked-in blank source is [whatsapp.env.example](whatsapp.env.example).
Fill the private copy locally; never paste real tokens/secrets into chat or a PR.
It uses `WHATSAPP_APP_ID`, `WHATSAPP_APP_SECRET`, `WHATSAPP_VERIFY_TOKEN`, optional
`WHATSAPP_ACCESS_TOKEN`, `WHATSAPP_PUBLIC_BASE_URL`, `WHATSAPP_PHONE_NUMBER_ID`,
`WHATSAPP_WABA_ID` and `WHATSAPP_ORGANIZATION_ID`.

For the **current local Atlas installation**, preserve both existing overrides
and add the optional receive-only overlay only after the private values exist:

```powershell
docker compose --env-file .env --env-file C:\oci\ia-portal\whatsapp-local.env -f docker-compose.yml -f docker-compose.atlas.yml -f docker-compose.whatsapp.yml config --quiet
docker compose --env-file .env --env-file C:\oci\ia-portal\whatsapp-local.env -f docker-compose.yml -f docker-compose.atlas.yml -f docker-compose.whatsapp.yml up -d --no-deps ai-gateway
```

The quiet validation was tested with synthetic values only. The real overlay was
**not applied**: actual Meta credentials/IDs/public local callback are still missing.
The existing `.env` was not edited. Generic installations should retain their own
actual Compose files rather than importing Atlas unnecessarily. Never run plain
`config`, inspect full environment JSON or put secrets in build arguments/logs.

## Public callback is a separate requirement

`http://127.0.0.1:9080` is the local portal, not a callback Meta can reach.
Use a separately configured HTTPS reverse proxy/tunnel with a valid certificate,
forwarding **only** `/api/webhooks/whatsapp` to this local service. Validate routing
before telling Meta to verify it. Do not expose SQLite, Trino, Qdrant or admin APIs
just to receive a webhook. No new tunnel/public endpoint was started by this task.

The current Oracle hostname does not automatically reach the local Windows
gateway. Pointing the Meta callback there without an explicit relay/configuration
would target the cloud installation instead. Preserve the working cloud portal
and local stack while choosing the actual callback path. Reverse-proxy logs must
redact verification query strings and credential/body data.

## Local update actually performed

- Built/recreated only `ai-gateway` and `portal-front`, using the existing Compose
  selection. Trino, OCR and ML containers were not restarted.
- Consistent pre-change SQLite backup under the protected
  `C:\oci\ia-portal\local-channels-backups` directory, plus previous image IDs.
- Actual schema upgraded **7→9** through existing migrations008/009. No new
  migration was introduced by this setup-card increment.
- Before/after fingerprints match for existing users/password hashes, knowledge
  and chat history:1user,10KB rows,1session,2messages. Integrity/FKs passed.
- Real browser login and **Canais** page verified at `http://127.0.0.1:9080/channels`,
  light/dark, with the new official-setup guide and honest missing-configuration
  states. No secret-entry field was introduced.
- Backend/frontend builds/typecheck passed; Channels unit/integration smoke passed
  including metadata credential non-disclosure and tenant routing isolation.
  Existing organization and full local Atlas smokes passed with HTTP doubles.
  Browser fixture tests covered themes/mobile/axe; a separate real local browser
  check covered the deployed version. No Meta/LLM message was sent by these checks.

Next: the owner creates the Meta test app and fills the private configuration;
then validate a public callback and actual signed receipt from the test number.
Live outbound API delivery still needs its own reviewed implementation with
durable send intent, ambiguity reconciliation and provider status handling. A
simulated response or configured access-token boolean is not that integration.
