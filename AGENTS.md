# IA-Portal — Instructions for coding agents

This file governs automated coding work in the IA-Portal repository. Read it before inspecting, editing, building, or testing the project. If a user request conflicts with these guidelines, ask for clarification before making destructive or security-sensitive changes.

## Product and boundaries

IA-Portal is a standalone AI knowledge and document portal, adapted from an earlier internal corporate application. It must remain independent of its former employer and customers.

- **Never** reintroduce former company names, logos, datasets, endpoints, credentials, business rules, or proprietary code.
- Do not copy material from private folders, local backups, or unrelated workspaces into this public repository.
- Do not publish secrets, `.env` files, real customer information, database files, tokens, or generated personal data.
- Prefer real, working integrations and honest empty states over fake dashboard metrics or mock business records.

## Primary current goal: visual redesign

The user approved a **clean enterprise-light design with optional dark mode** for the IA-Portal. The new identity should be consistent across the existing application, not a disconnected demo or a full rewrite.

### Brand

- Product name: **IA-Portal**. Replace the legacy header label "Portal de Conhecimento IA" in the UI where appropriate.
- Visual direction: contemporary enterprise SaaS, restrained and professional, with an AI/knowledge-management character.
- Brand mark: a simple geometric, abstract **IA / portal** symbol using blue, cyan and a small violet accent. Keep it recognizable at favicon and collapsed-sidebar sizes.
- **Do not claim to have reproduced the approved logo from text alone.** The approved reference image and a separately reviewed SVG are the source of truth when added to `docs/design/`. If these assets are missing, implement the layout and tokens first, then request the exact artwork instead of inventing a supposedly final logo.
- Do not use images or text from the earlier employer's branding.

### Initial design tokens (provisional until visual review)

Use semantic CSS variables or the existing UI framework's theme system; avoid scattering hardcoded colors throughout components.

| Token | Light | Dark |
| --- | --- | --- |
| App background | `#F5F8FC` | `#0B1220` |
| Sidebar | `#FFFFFF` | `#101B2E` |
| Surface/card | `#FFFFFF` | `#17243A` |
| Primary text | `#14243D` | `#F1F5FC` |
| Secondary text | `#64748B` | `#A5B4C8` |
| Border | `#E4EAF2` | `#293B55` |
| Primary action | `#1769E0` | `#4D93FF` |
| Cyan accent | `#06B6D4` | `#22D3EE` |
| Violet accent | `#6847E8` | `#9878FF` |

These are **implementation starting points**, not colors sampled from the concept image. Compare with the approved image when it is available. Maintain readable contrast and visible keyboard focus states.

### Layout and behavior

- **Light mode is the default**; offer a clear light/dark toggle in the top bar.
- Persist the user's theme preference locally; apply it without an obvious flash of the wrong theme.
- Use a consistent responsive application shell: collapsible left navigation, compact top bar, clear page title, sensible content width and spacing.
- Retain working routes and features: chat, dashboard, knowledge base, authentication and any existing administration screens.
- Navigation entries should reflect **real routes and authorized capabilities**. Do not add decorative links that lead nowhere.
- Chat should have a readable conversation column, clear message hierarchy, usable input area and responsive behavior; avoid the previous large empty dark canvas.
- Dashboard must show real available information or explicit empty states; never fabricate usage metrics.
- Administration should expose users, roles and permissions only after confirming backend endpoints, authentication and authorization. A visible menu item is not an access-control mechanism.
- Favor reusable components and existing dependencies over introducing an entirely new UI stack without a reason.
- Ensure labels and primary product-facing text are in Brazilian Portuguese (`pt-BR`) unless the user asks otherwise.
- Support keyboard navigation, accessible labels, sufficient contrast, loading states, errors and narrow screens.

## Implementation sequence

1. **Inspect first:** identify the frontend framework, router, styling solution, existing routes, state management and authentication flow. Report findings before significant changes.
2. **Create a dedicated branch** such as `feature/ui-redesign`; do not commit directly to `main` and do not merge automatically.
3. **Phase A — shell and identity:** implement theme tokens, global layout, header, sidebar, theme switch and brand placement. Preserve existing business behavior. Build and show screenshots of both themes for approval.
4. **Phase B — existing screens:** update chat, dashboard, knowledge base and login using the approved shell. Preserve API contracts and user workflows.
5. **Phase C — administration:** inspect actual users/roles/permissions endpoints and restore or build only legitimate, authorized screens. Treat missing server-side authorization as a blocker for exposing privileged operations.
6. **Validate:** run the relevant build and tests, verify navigation and both themes, and document what could not be tested.

Do not redesign every page in one unreviewed change. Prefer small, reviewable commits and a draft pull request.

## Architecture and local development

Repository areas include `front/`, `ai-gateway/`, `ocr-service/`, `ml-service/`, `scripts/`, `trino/` and `docker-compose.yml`. Inspect actual manifests and configuration before assuming commands or SDK versions.

- Local development is expected to run with Docker Compose and SQLite. Advanced RAG, Qdrant, Trino and ONNX integrations should remain optional unless explicitly requested.
- Use `.env.example` as a template; never read out or print real `.env` secret values in reports, logs or pull requests.
- Prefer targeted changes. Do not silently replace storage engines, authentication flows or service contracts.
- A successful frontend build does **not** establish that the backend, authentication or containers work. Report each separately.
- Run available frontend build/type checks and relevant .NET builds/tests. Report exact commands and outcomes; never claim tests ran when they did not.
- If Docker or another tool is unavailable, state that limitation rather than guessing that the application works.

## Security requirements

- Treat existing authentication and authorization as requiring review; do not assume that JWT issuance protects endpoints by itself.
- Never expose password hashes, API keys or internal configuration through frontend screens or API responses.
- Do not disable authorization, relax CORS, or open privileged routes merely to make a UI screen work.
- Use server-side permission checks for privileged actions. Client-side route guards are supplemental only.
- Never commit secrets, generated databases, uploaded documents, build outputs or confidential artifacts.
- Avoid destructive commands, database resets and force pushes without explicit approval.

## Agent working agreement

- Before edits: explain the intended scope and note any existing uncommitted changes. Do not overwrite the user's work.
- During edits: stay within the approved phase, keep changes focused and avoid unrelated refactors.
- After edits: summarize changed files, commands executed, pass/fail results, remaining risks and instructions to preview locally.
- If a design reference image or logo SVG is unavailable, say so and request it. **Do not substitute a different final identity without approval.**
- If something is ambiguous, favor a minimal reversible implementation and ask before proceeding.
