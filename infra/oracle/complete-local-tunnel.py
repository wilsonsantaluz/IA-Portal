"""Finish an authorized edge cutover after SSH recovery. Never reboots the VM."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import msvcrt
import os
from pathlib import Path
import re
import subprocess
import time
import urllib.request
import urllib.error

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--host', required=True)
parser.add_argument('--domain', required=True)
parser.add_argument('--key', required=True)
parser.add_argument('--credentials', required=True)
parser.add_argument('--state', default=str(Path(os.environ['LOCALAPPDATA'])/'IA-Portal/reverse-tunnel'))
parser.add_argument('--minutes', type=int, default=30)
args = parser.parse_args()
if not re.fullmatch(r'[a-zA-Z0-9.-]+', args.host) or not re.fullmatch(r'[a-zA-Z0-9.-]+', args.domain): parser.error('Invalid host/domain')
if not 1 <= args.minutes <= 60: parser.error('minutes must be 1..60')
key = Path(args.key).resolve(strict=True)
credentials = Path(args.credentials).resolve(strict=True)
root = Path(__file__).resolve().parent
state = Path(args.state).resolve(); state.mkdir(parents=True, exist_ok=True)
lock = (state/'activation.lock').open('a+b'); lock.seek(0); lock.write(b'0'); lock.flush(); lock.seek(0)
try: msvcrt.locking(lock.fileno(), msvcrt.LK_NBLCK, 1)
except OSError: raise SystemExit('An activation worker is already running')

def status(phase, message):
    data = dict(phase=phase, message=message, updatedUtc=datetime.now(timezone.utc).isoformat(), processId=os.getpid())
    temp = state/'activation-status.tmp'; temp.write_text(json.dumps(data), encoding='utf-8'); temp.replace(state/'activation-status.json')
    print(phase+': '+message, flush=True)

ssh = ['ssh', '-i', str(key), '-o', 'BatchMode=yes', '-o', 'StrictHostKeyChecking=yes', '-o', 'ConnectTimeout=10']
remote = 'opc@'+args.host

def command(command, timeout=30):
    try: return subprocess.run(ssh+[remote, command], capture_output=True, timeout=timeout, creationflags=subprocess.CREATE_NO_WINDOW)
    except subprocess.TimeoutExpired: return None

def request(base, path, token=None, body=None):
    headers = {'Content-Type':'application/json'}
    if token: headers['Authorization']='Bearer '+token
    req = urllib.request.Request(base+path, data=None if body is None else json.dumps(body).encode(), headers=headers)
    # HTTPS uses the system certificate verification; never bypass trust.
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    with opener.open(req, timeout=15) as response: return response.read()

deadline = time.monotonic()+args.minutes*60
try:
    while time.monotonic() < deadline:
        status('waiting_for_ssh', 'Waiting for the authorized VM recovery and both private forwards.')
        probe = command("curl --noproxy '*' --fail --max-time 5 -sS http://127.0.0.1:19080/login >/dev/null && curl --noproxy '*' --fail --max-time 5 -sS http://127.0.0.1:19089/health/ready >/dev/null")
        if probe is None or probe.returncode: time.sleep(15); continue
        status('activating', 'Private forwards respond; uploading only public proxy scripts.')
        try:
            copy = subprocess.run(['scp', '-i', str(key), '-o', 'BatchMode=yes', '-o', 'StrictHostKeyChecking=yes', '-o', 'ConnectTimeout=10',
                                   str(root/'Caddyfile.local-tunnel'), str(root/'activate-local-edge.sh'), remote+':'], capture_output=True, timeout=30, creationflags=subprocess.CREATE_NO_WINDOW)
        except (OSError, subprocess.TimeoutExpired):
            status('retrying', 'Proxy script upload unavailable; no credential details logged.'); time.sleep(15); continue
        if copy.returncode: status('retrying', 'Proxy script upload failed; sensitive details omitted.'); time.sleep(15); continue
        result = command(f'bash "$HOME/activate-local-edge.sh" {args.domain} "$HOME/Caddyfile.local-tunnel"', timeout=150)
        if result is None or result.returncode: status('retrying', 'Edge activation failed or timed out; helper preserves/restores original edge when possible.'); time.sleep(15); continue
        try:
            local = 'http://127.0.0.1:9080'; public = 'https://'+args.domain
            if hashlib.sha256(request(local, '/login')).digest() != hashlib.sha256(request(public, '/login')).digest(): raise ValueError('Frontend mismatch')
            values = dict(line.split('=',1) for line in credentials.read_text(encoding='utf-8-sig').splitlines() if '=' in line and not line.startswith('#'))
            token = json.loads(request(local, '/api/auth/login', body={'email':values['Bootstrap__AdminEmail'],'password':values['Bootstrap__AdminPassword']}))['token']
            # A local JWT must be accepted by the public endpoint and return the same organizations.
            if json.loads(request(local, '/api/organizations', token)) != json.loads(request(public, '/api/organizations', token)): raise ValueError('Backend mismatch')
            try: request(public, '/api/channels'); raise ValueError('Anonymous API accepted')
            except urllib.error.HTTPError as error:
                if error.code != 401: raise
        except Exception:
            status('verification_pending', 'Normal TLS, matching local frontend/backend or authorization not yet verified; old API remains preserved.')
            time.sleep(15); continue
        stopped = command('sudo /usr/local/bin/docker --host unix:///run/ia-portal-docker.sock stop -t 30 ia-portal-api >/dev/null', timeout=45)
        status('complete', 'Public HTTPS verified against local HTML and local JWT/organizations; old cloud web/database preserved. '+('Old cloud API stopped.' if stopped and stopped.returncode == 0 else 'Old cloud API stop not confirmed.'))
        break
    else: status('blocked', 'VM/tunnel recovery or external verification did not complete within the configured window. No reboot was performed by this worker.')
finally:
    lock.seek(0); msvcrt.locking(lock.fileno(), msvcrt.LK_UNLCK, 1); lock.close()
