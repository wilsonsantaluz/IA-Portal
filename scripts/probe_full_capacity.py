"""Disposable local-only full-RAG memory probe; never targets a live portal."""
import concurrent.futures
import argparse
import json
from pathlib import Path
import subprocess
import time
import urllib.request
import uuid

base='http://127.0.0.1:29089'
container='ia-portal-full-trial-api'
parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--configuration',required=True,help='Private synthetic-only probe env file, outside Git')
configuration=Path(parser.parse_args().configuration)
values=dict(line.split('=',1) for line in configuration.read_text().splitlines() if '=' in line)
assert values['Bootstrap__AdminEmail']=='probe@example.test'
assert values['DEEPSEEK_API_KEY']=='synthetic-probe-only'
opener=urllib.request.build_opener(urllib.request.ProxyHandler({}));token=''
def request(path,data=None,raw=False):
    headers={'Content-Type':'application/json'}
    if token:headers['Authorization']='Bearer '+token
    with opener.open(urllib.request.Request(base+path,data=json.dumps(data).encode() if data is not None else None,headers=headers),timeout=180) as response:
        text=response.read().decode();return text if raw else json.loads(text)
for _ in range(60):
    try:request('/health/ready');break
    except OSError:time.sleep(1)
else:raise RuntimeError('Disposable probe did not start')
token=request('/api/auth/login',{'email':values['Bootstrap__AdminEmail'],'password':values['Bootstrap__AdminPassword']})['token']
def exercise():
    item=request('/api/kb/upsert',{'keyName':'capacity_probe_synthetic','section':'Synthetic','title':'Capacity validation','content':'Synthetic capacity validation code VECTOR-CAPACITY-42.','isActive':True})
    print('Embedding/indexing completed for the isolated synthetic item.',flush=True)
    session=request('/api/chatBot/sessions',{})['id']
    # The fake provider is loopback port1. No real inference or credentials.
    request('/api/ia/ask/stream',{'sessionId':session,'requestId':str(uuid.uuid4()),'pergunta':'Synthetic capacity validation code'},raw=True)
started=time.monotonic()
with concurrent.futures.ThreadPoolExecutor(max_workers=1) as pool:
    result=pool.submit(exercise)
    while not result.done():
        stats=subprocess.run(['docker','stats','--no-stream','--format','{{.Name}} {{.MemUsage}}',container],capture_output=True,text=True)
        print('elapsed=%.1fs %s'%(time.monotonic()-started,stats.stdout.strip()),flush=True)
        if time.monotonic()-started>190:break
        time.sleep(2)
    try:result.result(timeout=5);print('Native RAG request completed (the LLM endpoint is deliberately unavailable).')
    except Exception as error:print('Native RAG probe stopped:',type(error).__name__)
state=subprocess.check_output(['docker','inspect','--format','{{json .State}}',container],text=True)
value=json.loads(state)
print('Final state:',json.dumps({key:value.get(key) for key in ['Status','ExitCode','OOMKilled']}))
print('No live database, collection, provider key or production source was used.')
