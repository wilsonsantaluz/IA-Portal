# Contributing

IA-Portal is preparing a shared Community/self-hosted and future managed-service
core. Read [the audit](docs/architecture/CURRENT_STATE.md),
[target architecture](docs/architecture/TARGET_ARCHITECTURE.md) and
[roadmap](docs/ROADMAP.md) before proposing a structural change.

**A project license has not been selected.** Do not assume redistribution or
relicensing rights. Ownership and Apache 2.0/AGPLv3 are pending an owner decision;
see [license options](docs/LICENSE_OPTIONS.md). No CLA or copyright transfer is
introduced by this document. Discuss substantial external code contributions
with the maintainer before submitting material whose licensing is uncertain.

- Work on a focused branch and open a pull request; do not commit directly to main.
- Describe the problem, resulting behavior, tests and limits of validation.
- Preserve the pt-BR UI, existing design tokens/light-dark themes and API contracts.
- Reuse the current modules; add a service/framework only with a demonstrated need.
- Every tenant-sensitive query/write needs validated organization scope. User IDs,
  tenant headers, vector payloads and model/tool output are not authority.
- Include additive migration, backup/restore/failure tests and legacy policy with
  persistence changes. Never reset databases to make an upgrade pass.
- Use synthetic data, `example.test` identities and temporary storage. Never submit
  `.env`, private keys, SQLite files, private originals, customer documents or logs
  containing secrets. Review diffs and history, not just `.gitignore`.
- Do not enable paid calls, external messages, OCI writes or production deploy in tests.
- Keep authentication, egress, SQL read-only rules and connector grants enforced server-side.

Run the [self-hosting validation commands](docs/SELF_HOSTING.md#validate-locally)
and the suites relevant to the affected module. Browser tests require Playwright
and Chrome as documented in `tests/ui/`; optional integrations require disposable
services. Report missing infrastructure honestly. There is no mandatory tracked
CI workflow yet; a successful UI build alone is insufficient.

Security findings should use [SECURITY.md](SECURITY.md), not a public exploit PR.
Follow [the community conduct policy](CODE_OF_CONDUCT.md). Documentation for new
architecture and contributors should be in English; product labels remain pt-BR.
