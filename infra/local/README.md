# Local Windows HTTPS edge

Native Windows Caddy terminates TLS and proxies to the existing loopback-only
frontend at `127.0.0.1:9080`. That frontend keeps its existing authenticated API
proxy. Docker services, database volumes and private WhatsApp configuration are
not changed by these scripts. Windows-native listening avoids assuming Docker
Desktop port publishing supports your inbound IPv6 path.

## DNS and ingress prerequisites

Use the machine's **non-temporary global IPv6**, not `fe80::`, `::1`, a ULA or a
temporary/privacy address. Inspect with `netsh interface ipv6 show addresses`.
Even a non-temporary address can change when the ISP changes the delegated prefix;
the DDNS updater must update the AAAA record accordingly. No No-IP credential is
stored by these scripts and no DNS change is performed automatically.

- Point AAAA to that IPv6. For IPv6-only operation remove an A record pointing to
  the old cloud server; otherwise different clients may reach different databases.
- CGNAT IPv4 cannot be made inbound-reachable merely by changing an A record.
  An AAAA-only hostname serves IPv6-capable clients, not IPv4-only networks. No-IP
  DNS alone is not an IPv4-to-IPv6 reverse proxy. External Meta webhook reachability
  must be verified in its actual dashboard; it is not inferred from local TLS.
- Allow inbound **TCP80/443 only** to this host in the modem's IPv6 firewall.
  Do not enable DMZ or expose Trino, databases, the gateway port or the browser
  bridge. Direct IPv6 normally needs firewall permission rather than IPv4 NAT.
- Windows firewall uses an optional program-scoped Caddy rule for TCP80/443.
  A listener alone does not establish external reachability or certificate issuance.
- Let's Encrypt initially prefers IPv6 when A and AAAA both exist. Both DNS
  families must route to the intended TLS edge when used together.

## Install and validate

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File infra/local/install-caddy.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File infra/local/start-https.ps1 -Domain YOUR_HOSTNAME
```

The installer pins Caddy2.11.9 from the official release and verifies its published
SHA512 checksum before extracting only `caddy.exe`. The private default runtime
directory is `%LOCALAPPDATA%\IA-Portal\https`; permissions are restricted to the
current user, SYSTEM and administrators. Certificate keys stay outside Git.

By default `start-https.ps1` only validates the Caddyfile, checks gateway readiness,
queries public DNS, identifies local non-temporary IPv6 and reports blockers. It
does not start a listener, change firewall rules or request a public certificate.
Missing/mismatched AAAA, stale/unverified A records and occupied TCP ports prevent
startup. An explicit `-ExpectedIPv4` supports a genuinely public forwarded IPv4;
private/CGNAT IPv4 addresses are rejected. Multiple possible global IPv6 interfaces
require an explicit `-ExpectedIPv6` matching a local preferred non-temporary address.

## Start and stop

After updating DNS and the modem firewall, from elevated PowerShell:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File infra/local/start-https.ps1 -Domain YOUR_HOSTNAME -Start -OpenFirewall
```

This starts one hidden Caddy process, with process identity recorded in private
`runner.json`. Repeating the command reuses that owned process; mismatched PID,
domain or issuer is rejected. Runtime logs omit request URI and headers; access
logging is disabled to avoid webhook verification token leakage. Caddy's admin
endpoint and config persistence are disabled. Certificates and renewal state
persist in private `certs`. A running process does not prove successful issuance:
check external HTTPS certificate trust/hostname, then Meta callback verification.

`-Staging` uses Let's Encrypt staging in a separate directory for diagnostic
issuance; staging certificates are not publicly trusted and cannot complete Meta
setup. Stop staging before starting production. No machine trust store is modified.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File infra/local/stop-https.ps1
```

Stopping targets only a recorded process whose executable and start time match.
It preserves all containers, DNS, keys and renewal state. Removing the optional
Windows rule later uses `Remove-NetFirewallRule -Name IA-Portal-Caddy-HTTPS`.
No scheduled task/service is installed; after reboot/sign-in start the Docker
application and rerun the guarded start command. Do not assume an interactive
Windows/Docker Desktop workstation has production uptime guarantees.

## WhatsApp callback

After external HTTPS works, the callback is:

```text
https://YOUR_HOSTNAME/api/webhooks/whatsapp
```

Set `WHATSAPP_PUBLIC_BASE_URL=https://YOUR_HOSTNAME` in the private runtime env file
and preserve all Compose overrides when recreating **only** the gateway. Configure
the fresh App Secret and verification token privately. Do not put verification
tokens in browser URLs or request logs. Existing Cloud reception is signature-
validated and organization-routed; Cloud sending remains unimplemented. Domain
reachability alone does not subscribe the Meta app or connect an account.

## Tests and actual state, 2026-11-09

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File infra/local/start-https.tests.ps1
python scripts/smoke_local_https.py
```

Executed: official Windows binary checksum/install, actual Caddy configuration
validation, mocked DNS stale-A/missing-or-foreign-AAAA/CGNAT guards and no-listener
dry-run, loopback TLS with a local test CA trusted only by the test's SSL context,
actual portal HTML and security header, anonymous API401, and redaction of a
synthetic verification query on upstream502. Test processes are stopped afterward;
no Windows root trust installation, real token use or public CA issuance occurs.

At inspection the owner-selected hostname still resolved to the Oracle IPv4,
without AAAA. The guarded public start has not run; no public certificate has been
issued locally. No modem, No-IP or Oracle resources were modified. Application
builds are not rerun for this edge-only change; the running backend/frontend were
checked through the TLS smoke instead. No database migration is required.

Primary references: [Caddy HTTPS quick start](https://caddyserver.com/docs/quick-starts/https),
[automatic HTTPS](https://caddyserver.com/docs/automatic-https),
[Let's Encrypt IPv6 validation](https://letsencrypt.org/docs/ipv6-support/).
