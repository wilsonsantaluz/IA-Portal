#!/usr/bin/env bash
# Run only in an ephemeral bash container, never on the real VM.
set -euo pipefail
test "${EDGE_FIXTURE_CONTAINER:-}" = yes
test -f /.dockerenv
mkdir -p /fixture/bin /usr/local/bin
export PATH=/fixture/bin:$PATH
cat >/fixture/bin/sudo <<'EOF'
#!/usr/bin/env bash
exec "$@"
EOF
cat >/fixture/bin/ss <<'EOF'
#!/usr/bin/env bash
port=${*: -1}; port=${port##*:}
echo "LISTEN 0 128 ${BAD_BIND:-127.0.0.1}:$port 0.0.0.0:*"
EOF
cat >/fixture/bin/curl <<'EOF'
#!/usr/bin/env bash
if [[ "$*" == *https:* && "${PUBLIC_FAIL:-}" == yes ]]; then exit 1; fi
if [[ "$*" == *19080* && "${TUNNEL_FAIL:-}" == yes ]]; then exit 1; fi
echo ready
EOF
cat >/fixture/bin/sleep <<'EOF'
#!/usr/bin/env bash
exit 0
EOF
cat >/usr/local/bin/docker <<'EOF'
#!/usr/bin/env bash
echo "$*" >>/fixture/calls
if [[ "$1" == --host ]]; then shift 2; fi
if [[ "$1" == inspect && "$*" == *com.ia-portal.edge* ]]; then echo "${EDGE_ACTIVE:-}"; exit 0; fi
if [[ "$1" == inspect ]]; then echo synthetic-caddy-image; exit 0; fi
if [[ "$1" == run && "$*" == *' -d '* && "${RUN_FAIL:-}" == yes ]]; then exit 1; fi
echo synthetic-ok
EOF
chmod +x /fixture/bin/* /usr/local/bin/docker
echo synthetic-config >/fixture/Caddyfile
script=/scripts/activate-local-edge.sh
expect_failure() { if bash "$script" portal.example.test /fixture/Caddyfile >/fixture/output 2>&1; then echo 'Expected failure' >&2; exit 1; fi; }
export BAD_BIND=0.0.0.0
expect_failure
! grep -q '^--host .* stop ' /fixture/calls
unset BAD_BIND
export TUNNEL_FAIL=yes
expect_failure
! grep -q '^--host .* stop ' /fixture/calls
unset TUNNEL_FAIL
export RUN_FAIL=yes
expect_failure
grep -q ' rename ia-portal-web-vm-.* ia-portal-web' /fixture/calls
grep -q ' start ia-portal-web' /fixture/calls
test "$(grep -c ' rename ia-portal-web-vm-.* ia-portal-web' /fixture/calls)" = 1
unset RUN_FAIL
: >/fixture/calls
export PUBLIC_FAIL=yes
expect_failure
grep -q ' rename ia-portal-web-vm-.* ia-portal-web' /fixture/calls
unset PUBLIC_FAIL
: >/fixture/calls
bash "$script" portal.example.test /fixture/Caddyfile >/fixture/output
grep -q 'Local edge active' /fixture/output
export EDGE_ACTIVE=local-tunnel
: >/fixture/calls
bash "$script" portal.example.test /fixture/Caddyfile >/fixture/output
! grep -q ' stop ' /fixture/calls
echo 'PASS: non-loopback/missing tunnel denied before stop, launch/HTTPS failure restores previous web, successful activation and idempotent repeat. Synthetic container only.'
