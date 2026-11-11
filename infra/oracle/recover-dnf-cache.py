"""Apply the authorized DNF cache guard after SSH returns; never requests reboot."""
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

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--host',required=True)
parser.add_argument('--domain',required=True)
parser.add_argument('--key',required=True)
parser.add_argument('--minutes',type=int,default=35)
args=parser.parse_args()
if not re.fullmatch('[a-zA-Z0-9.-]+',args.host) or not re.fullmatch('[a-zA-Z0-9.-]+',args.domain):parser.error('Invalid host/domain')
key=Path(args.key).resolve(strict=True)
state=Path(os.environ['LOCALAPPDATA'])/'IA-Portal/reverse-tunnel';state.mkdir(parents=True,exist_ok=True)
lock=(state/'dnf-recovery.lock').open('a+b');lock.write(b'0');lock.flush();lock.seek(0)
try:msvcrt.locking(lock.fileno(),msvcrt.LK_NBLCK,1)
except OSError:raise SystemExit('DNF recovery worker already active')
def status(phase,message):
    payload=dict(phase=phase,message=message,processId=os.getpid(),updatedUtc=datetime.now(timezone.utc).isoformat())
    (state/'dnf-recovery-status.json').write_text(json.dumps(payload),encoding='utf-8')
    print(phase+': '+message,flush=True)
ssh=['ssh','-i',str(key),'-o','BatchMode=yes','-o','StrictHostKeyChecking=yes','-o','ConnectTimeout=8']
remote='opc@'+args.host
def run(command,timeout=20):
    try:return subprocess.run(ssh+[remote,command],capture_output=True,timeout=timeout,creationflags=subprocess.CREATE_NO_WINDOW)
    except subprocess.TimeoutExpired:return None
def fetch(base,path):
    opener=urllib.request.build_opener(urllib.request.ProxyHandler({}))
    with opener.open(base+path,timeout=10) as response:return response.read()
deadline=time.monotonic()+args.minutes*60
applied=False
try:
    while time.monotonic()<deadline:
        status('waiting_for_ssh','Awaiting VM recovery; no reboot requested by this worker.')
        probe=run('true')
        if probe is None or probe.returncode:time.sleep(10);continue
        if not applied:
            status('applying_guard','SSH verified; applying the approved cache-only memory guard.')
            try:
                copy=subprocess.run(['scp','-i',str(key),'-o','BatchMode=yes','-o','StrictHostKeyChecking=yes','-o','ConnectTimeout=8',str(Path(__file__).with_name('guard-dnf-cache.sh')),remote+':'],capture_output=True,timeout=20,creationflags=subprocess.CREATE_NO_WINDOW)
            except subprocess.TimeoutExpired:time.sleep(10);continue
            if copy.returncode:time.sleep(10);continue
            result=run('sudo bash "$HOME/guard-dnf-cache.sh" --apply',timeout=90)
            if result is None or result.returncode:status('guard_pending','Approved guard has not completed; details omitted.');time.sleep(10);continue
            verify=run('systemctl show dnf-makecache.service -p MemoryMax -p MemorySwapMax; systemctl is-enabled dnf-makecache.timer || true')
            if verify is None or verify.returncode or b'MemoryMax=268435456' not in verify.stdout or b'disabled' not in verify.stdout:
                status('guard_pending','Guard properties not yet verified.');time.sleep(10);continue
            applied=True;status('guard_applied','Cache timer disabled; cache service memory limit verified. Waiting for public tunnel.')
        try:
            public='https://'+args.domain
            if json.loads(fetch(public,'/health/ready')).get('status')!='ready':raise ValueError()
            if hashlib.sha256(fetch(public,'/login')).digest()!=hashlib.sha256(fetch('http://127.0.0.1:9080','/login')).digest():raise ValueError()
            try:fetch(public,'/api/channels');raise ValueError()
            except urllib.error.HTTPError as error:
                if error.code!=401:raise
        except Exception:time.sleep(10);continue
        status('complete','Cache guard applied, public TLS/local frontend/readiness and anonymous API401 verified.');break
    else:status('blocked','Recovery timed out; do not assume public availability or submit a duplicate reboot.')
finally:
    lock.seek(0);msvcrt.locking(lock.fileno(),msvcrt.LK_UNLCK,1);lock.close()
