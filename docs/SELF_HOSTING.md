# Self-hosting

For the separately authorized 1GB OCI demonstration deployment, see
[the minimal deployment report](../infra/oracle/MICRO_DEPLOYMENT.md). It uses a
standalone two-container profile; do not mix it into the standard Compose stack.

IA-Portal currently supports a single-node local Docker Compose installation
with SQLite. No Oracle resource, PostgreSQL database, paid service or commercial
edition is required. AI inference requires either a configured external provider
(which may charge separately) or an explicitly configured Ollama endpoint/model.

## Start a generic Community workspace

Requirements: Docker with Compose. For native development/tests: .NET 8 SDK,
Node 22.12+, Yarn Classic 1.22.22 and Python 3 for scripts.

```powershell
Copy-Item .env.example .env  # only if no local .env exists
# Edit locally: JWT_KEY, ADMIN_EMAIL, ADMIN_PASSWORD and provider configuration.
docker compose -f docker-compose.yml --env-file .env config --quiet
docker compose -f docker-compose.yml --env-file .env up --build -d
```

Never print or commit `.env`. Set a random JWT secret (at least 32 bytes) and an
initial admin password of at least 12 characters. The bootstrap does not reset
existing passwords. The explicit `-f` selects the generic stack even if an
operator's local `COMPOSE_FILE` enables Atlas. Keep `ATLAS_SEED_KNOWLEDGE=false`
for a generic installation; turning seed off does not delete existing demo data.

For an **existing** installation, preserve its Compose overrides/profiles and
environment. Do not remove an Atlas connector profile while upgrading a saved
Atlas connection. In the current local setup, which already selects its override
through `COMPOSE_FILE`, update just the application images when ready:

```powershell
docker compose build ai-gateway portal-front
docker compose up -d --no-deps ai-gateway portal-front
```

These commands apply pending migrations through 009 at gateway startup with a pre-upgrade backup.
They were not run against the existing installation during this increment.

Open `http://127.0.0.1:9080`. A fresh installation creates the default organization
and its Owner. Choose **Organizações** to create another organization (from the
default organization), then switch using the top selector. In the new workspace:

1. Choose an available server-approved default model if desired.
2. Import the technical-support template; review/edit documents in the existing KB.
3. Create users through the existing user screen and assign a membership policy
   as Owner. New users created through the old group screen initially retain
   custom group-based access until an explicit membership policy is assigned.
4. Test the template's suggested questions in the existing chat. Missing providers
   or evidence produce explicit errors/clarification, not fabricated connections.

Owners can change membership policies/status; the last active Owner cannot be
removed. Admin handles workspace configuration and ordinary user/group management;
Member can chat/edit KB/query explicitly granted sources; Viewer can read KB.
Connector group grants remain additional requirements; a role does not invent or
automatically grant a physical data source. Owners can invite existing accounts
with single-use 24-hour Member/Viewer links and transfer ownership with explicit
confirmation. Any authenticated holder can accept a link; share it privately.
No email is sent. See [invitation operations](features/ORGANIZATION_INVITATIONS.md).
Ownership of organization 1 includes installation-wide AI administration privileges.

The installation's provider endpoints/credentials remain controlled by the
default organization administrator. No tenant secret editor is implemented.
Tenant defaults and personal preferences are isolated, and never reveal keys.

## Existing data and migrations 007–008

Startup runs checksummed migrations. Existing schemas are never recreated.
Migration 005 previously associated legacy users/data with organization 1.
Migration 007 adds membership policies/version and organization model settings.
Existing `admin` memberships become Admin; the lowest-ID active admin of each
organization becomes Owner. Custom group memberships remain null-policy and keep
their exact grants. Organizations without an active administrator are not given
a fabricated owner; repair requires a reviewed operator procedure.

Existing account-level model preferences are copied only to organization 1.
The old table remains as archival data; runtime uses tenant-scoped preferences.
Other memberships inherit their own organization default or installation default.
Passwords, documents, connector grants, conversations and selected session models
are unchanged. No API key is copied to SQLite.

Migration 008 adds hashed invitations without resetting existing data. Existing
membership roles are unchanged until an explicit accepted invitation or transfer.

Upgrades create a consistent pre-change SQLite backup and validate foreign keys.
Version 007 also explicitly consumes every SQLite batch result so a later SQL
error cannot be hidden while recording a successful version. No old migration
checksum was changed.

Operational rollback: stop the gateway, restore the complete pre-upgrade backup
to a **new path**, use the previous application version and verify integrity.
Do not drop tables/columns in the live database. Post-upgrade writes require
reconciliation before rollback. The test suite validates failure rollback and
backup/restore/new upgrade in temporary files. Keep backups protected: they hold
the same private data as the live database.

`docker compose down` preserves volumes. Do not use `down -v` or remove SQLite to
upgrade. Never use previous one-off authorization to erase local data as permission
to erase a different installation.

## Optional services and architecture limits

- IA-Portal Service channels: [local simulator and reception-only Cloud webhook](service/README.md).
  No new service or credentials are needed for simulation. Live sending/onboarding
  remains blocked/pending; no new port or public ingress is enabled by this module.

- Atlas: follow [the demo guide](demo/ATLAS_LOCAL.md); all data is synthetic.
- Trino is optional. Approved organization profiles must use read-only credentials,
  safe network destinations and explicit source grants. The demo Memory catalog
  requires reloading after restart.
- Vector RAG: follow [RAG_LOCAL.md](RAG_LOCAL.md), configure licensed compatible
  ONNX models and reindex explicitly with organization payloads.
- OCR/ML companion services are .NET, not Python; current ML projections are formulas.
- ARM64 base manifests exist, but MKL is x64-only and full native OCR/vector runtime
  testing is still required. See [the audit](architecture/CURRENT_STATE.md).
- `host.docker.internal` is not assumed to work on every Linux host. Review host
  routing and approved origins rather than exposing Ollama/internal services.

## Validate locally

```powershell
dotnet build ai-gateway/AiGateway.csproj
dotnet build ocr-service/OcrService.csproj
dotnet build ml-service/MlService.csproj
dotnet run --project tests/Workspace
dotnet run --project tests/Invitations
dotnet run --project tests/Organizations
dotnet run --project tests/Connectors
python scripts/smoke_organizations.py
python scripts/smoke_local.py --atlas
npm run typecheck --prefix front
npm run build --prefix front
```

API smoke uses an isolated temporary SQLite database and synthetic local HTTP
services. It does not touch the running portal's data, need real LLM credentials
or modify OCI. Browser tests and optional real Qdrant/Trino tests are described in
`tests/`; do not point destructive integration fixtures at a production source.

Public hosting is a separate task. Keep loopback bindings, protect internal
services, configure TLS/reverse proxy/firewall, review CORS/abuse protection,
redact logs and implement retention/recovery before exposing tenant data. No
production deployment is performed by these instructions or by this increment.
