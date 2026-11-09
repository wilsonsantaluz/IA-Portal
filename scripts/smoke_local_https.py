"""Loopback-only Caddy TLS/proxy smoke. No public CA, trust-store changes or login."""
import argparse
import os
from pathlib import Path
import socket
import ssl
import subprocess
import time
import urllib.request
import urllib.error

ROOT = Path(__file__).resolve().parents[1]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--state', default=str(Path(os.environ['LOCALAPPDATA'])/'IA-Portal/https'))
args = parser.parse_args()
state = Path(args.state).resolve()
binary = state/'caddy.exe'
if not binary.is_file(): raise SystemExit('Install verified Caddy first')
test_root = state/'loopback-test'
test_root.mkdir(exist_ok=True)
with socket.socket() as probe:
    probe.bind(('127.0.0.1', 0)); port = probe.getsockname()[1]
env = dict(os.environ, PORTAL_SITE=f'localhost:{port}', PORTAL_TLS_STORAGE=str(test_root/'certs'))
env.pop('PORTAL_ACME_CA', None)
source_config = (ROOT/'infra/local/Caddyfile').read_text(encoding='utf-8')
with socket.socket() as probe:
    probe.bind(('127.0.0.1', 0)); http_port = probe.getsockname()[1]
config = test_root/'loopback.Caddyfile'
config.write_text(source_config.replace('{', '{\n default_bind 127.0.0.1\n http_port '+str(http_port), 1), encoding='utf-8')
log_path = test_root/'smoke.log'
context = ssl.create_default_context()

def run(configuration, failing=False):
    with log_path.open('a', encoding='utf-8') as log:
        process = subprocess.Popen([str(binary), 'run', '--config', str(configuration), '--adapter', 'caddyfile'], env=env, cwd=test_root,
                                   stdout=log, stderr=log, creationflags=subprocess.CREATE_NO_WINDOW)
        try:
            root_cert = test_root/'certs/pki/authorities/local/root.crt'
            for _ in range(80):
                if process.poll() is not None: raise AssertionError('Local Caddy exited; inspect private smoke log')
                if root_cert.is_file():
                    context.load_verify_locations(cafile=str(root_cert))
                    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}), urllib.request.HTTPSHandler(context=context))
                    try:
                        path = '/?hub.verify_token=SYNTHETIC_TLS_REDACTION_CHECK' if failing else '/login'
                        with opener.open(f'https://localhost:{port}'+path, timeout=2) as response:
                            assert not failing and response.status == 200
                            assert 'text/html' in response.headers['Content-Type']
                            assert response.headers['X-Content-Type-Options'] == 'nosniff'
                        break
                    except urllib.error.HTTPError as error:
                        if failing and error.code == 502: break
                        raise
                    except (OSError, urllib.error.URLError): pass
                time.sleep(.2)
            else: raise AssertionError('Local TLS/proxy did not become ready')
            if not failing:
                try:
                    opener.open(f'https://localhost:{port}/api/channels', timeout=5)
                    raise AssertionError('Anonymous channels access succeeded')
                except urllib.error.HTTPError as error: assert error.code == 401
        finally:
            process.terminate()
            try: process.wait(timeout=10)
            except subprocess.TimeoutExpired: process.kill(); process.wait(timeout=5)

run(config)
bad_config = test_root/'upstream-error.Caddyfile'
bad_config.write_text(config.read_text(encoding='utf-8').replace('127.0.0.1:9080', '127.0.0.1:1'), encoding='utf-8')
run(bad_config, failing=True)
assert 'SYNTHETIC_TLS_REDACTION_CHECK' not in log_path.read_text(encoding='utf-8')
print('PASS: trusted loopback TLS, actual portal HTML, security header, anonymous API401 and error-log query redaction. Local CA only; no public certificate issued.')
