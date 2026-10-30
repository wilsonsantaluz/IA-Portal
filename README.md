# IA-Portal — Enterprise AI Knowledge Portal

A portfolio-oriented prototype of a corporate knowledge portal combining a **.NET 8 API**, **React/TypeScript interface**, **SQLite persistence**, document OCR, and an external large language model (LLM). It illustrates architecture patterns for knowledge management, document analysis, and AI-assisted workflows.

> **Status: local development prototype, not production-ready.** This public repository has a reduced, generalized scope. It is not a complete production deployment, and its optional advanced RAG integrations require additional setup.

## What works in local mode

- Automatic creation of a local SQLite database on first run.
- Initial administrator bootstrap through environment variables; no hardcoded default password.
- JWT login, knowledge-base entries, streaming chat, persisted conversations, and response ratings.
- **Text-based context selection**, not semantic/vector search: up to eight excerpts are selected from the 200 most recent active knowledge entries.
- PDF/image upload, extraction through a separate OCR service, and analysis through the configured AI provider.
- A basic metrics dashboard.

Local mode does **not** require Qdrant, Trino, or ONNX models. However, it **does require an external DeepSeek API key** for AI responses. Uploaded document content, selected knowledge context, and conversation history may be transmitted to the configured AI provider. Do not use real confidential data in this prototype.

## Architecture

| Component | Technology and purpose |
| --- | --- |
| `front/` | React, TypeScript, Vite, Material UI; Nginx in Docker |
| `ai-gateway/` | ASP.NET Core / .NET 8 API, EF Core with SQLite, authentication, knowledge, chat, and AI/OCR integration |
| `ocr-service/` | .NET 8 service using Tesseract and Poppler for images and PDFs |
| `ml-service/` | Demonstration service with simulated portfolio projections; **not a trained predictive model** |
| SQLite | Local storage for users, knowledge, conversations, and ratings |
| Optional advanced RAG | Qdrant vector retrieval, ONNX embeddings and reranking, and optional Trino queries |

```text
React / TypeScript UI
        |
        v
  .NET 8 AI Gateway ----> SQLite (users, knowledge, history)
        |       |
        |       +----> DeepSeek API (external LLM)
        |
        +------------> OCR service (Tesseract / Poppler)
        |
        +-- optional --> Qdrant + ONNX models
        +-- optional --> Trino (separate analytical data source)
```

## Quick start with Docker

**Requirements:** Docker Compose, your own DeepSeek API key, and locally generated authentication credentials.

1. Copy `.env.example` to `.env` (for example, `Copy-Item .env.example .env` in PowerShell).
2. Set the following values in `.env`:

| Variable | Description |
| --- | --- |
| `DEEPSEEK_API_KEY` | Your AI provider API key |
| `JWT_KEY` | Random JWT signing secret of at least 32 bytes |
| `ADMIN_EMAIL` | Initial administrator email |
| `ADMIN_PASSWORD` | Initial administrator password of at least 12 characters |
| `USE_ADVANCED_RAG` | Keep `false` for the default local mode |

3. Start the application:

```powershell
docker compose config --quiet
docker compose up --build
```

- UI: http://localhost:9080/login
- API Swagger UI: http://localhost:9089/swagger

The Compose ports bind to `127.0.0.1` by default. The named `portal_data` volume persists SQLite data. `docker compose down` preserves volumes; `docker compose down -v` removes them. The bootstrap administrator is created only when there are no users; changing `ADMIN_PASSWORD` afterward does not reset an existing password.

The document-upload control accepts PDF, PNG, and JPEG files up to 20 MB. OCR and AI analysis require their respective services to be available.

## Run components outside Docker

Requirements: .NET 8 SDK, Node.js 22.12+, and Yarn Classic.

Configure these environment variables for the gateway process:

- `Jwt__Key`
- `Bootstrap__AdminEmail` and `Bootstrap__AdminPassword`
- `DEEPSEEK_API_KEY`
- Optional: `ConnectionStrings__PortalConnection` (defaults to `Data Source=data/portal.db;Foreign Keys=True`)
- Optional: `OCR_SERVICE_URL` (defaults to `http://localhost:9082`)

The root `.env` is read by Docker Compose, **not automatically by .NET**.

```powershell
cd ai-gateway
dotnet run --urls http://localhost:9089
```

In a separate terminal:

```powershell
cd front
yarn install --frozen-lockfile
yarn dev
```

Vite serves the UI on port 5173 and proxies `/api` to the gateway. See `front/.env.example` for frontend API origin configuration; never put secrets in Vite environment variables.

To run only the OCR service in Docker:

```powershell
docker compose run --rm -p 127.0.0.1:9082:8080 ocr-service
```

**Database limitation:** initialization uses EF Core `EnsureCreatedAsync`, not migrations. It does not upgrade existing SQLite schemas or import data from earlier SQL Server implementations.

## Optional advanced RAG

Advanced retrieval is separate from the default local workflow. It requires compatible external models/tokenizers and additional infrastructure.

Expected model paths include:

```text
models/embeddings/nomic-v15/model.onnx
models/embeddings/nomic-v15/tokenizer.json
models/reranking/model.int8.onnx
models/reranking/tokenizer.json
```

The embedding configuration expects 768-dimensional vectors. Check model compatibility and licensing yourself.

1. Set `USE_ADVANCED_RAG=true`.
2. Run `docker compose --profile rag up --build`.
3. Configure Trino separately if analytical SQL queries are needed; Trino is not included in the Compose stack. `trino/examples/` contains synthetic demonstration data.
4. Reindex knowledge entries using `/api/kb/reindex-all`. Entries created in local mode do not automatically have vector indexes.

The advanced path is an optional integration, **not a claim of turnkey production-ready RAG**. Trino must be configured with restricted, read-only permissions before any real data source is connected.

## Build and smoke tests

```powershell
dotnet build ai-gateway/AiGateway.csproj
dotnet build ocr-service/OcrService.csproj
dotnet build ml-service/MlService.csproj
python scripts/smoke_local.py
cd front
yarn build
```

The local smoke test uses a real API and temporary SQLite database, but substitutes HTTP test doubles for the AI provider and OCR service. It does **not** validate live model quality, Tesseract extraction, ONNX inference, Trino connectivity, or browser-based end-to-end flows. These commands are documented for reproduction; they have not been rerun as part of this README update.

## Security and publication notes

**Do not expose this application to the public internet as-is.** The [publication review](docs/REVISAO_PUBLICACAO.md) records outstanding work, including:

- Rotating any credentials previously exposed in earlier local materials.
- Completing server-side authorization and per-user ownership checks on administrative and history endpoints.
- Hardening AI-generated SQL with restricted read-only database permissions, validation, and limits.
- Reviewing untrusted Markdown/HTML, OCR resource limits, temporary-file cleanup, CORS, HTTPS, and data retention.
- Implementing schema migrations and a production backup/recovery strategy.

The local working directory may contain **private, unredacted originals** under `.private-originals/`. This directory is Git-ignored, but **a manual ZIP of the entire working folder can still include it**. Do not upload or distribute those materials.

For a reviewed source-only export:

```powershell
python scripts/export_public.py
```

Inspect the generated `.publication/` directory and its SHA-256 manifest before sharing. Pattern-based scans cannot prove that all sensitive information has been removed. Confirm repository history and external copies separately.

## Scope, ownership, and license

This public version is a generalized demonstration. It must not be interpreted as containing any organization's private source code, datasets, credentials, or full internal implementation.

**No open-source license has been selected.** Do not assume reuse or redistribution rights. Verify ownership and third-party dependency/model licenses before publishing a license.

For a detailed Portuguese audit and the current list of limitations, see [docs/REVISAO_PUBLICACAO.md](docs/REVISAO_PUBLICACAO.md).
