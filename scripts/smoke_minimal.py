"""Validate an already running minimal portal. Real inference is explicitly opt-in."""
import argparse
import concurrent.futures
import json
from pathlib import Path
import statistics
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('--url',required=True)
parser.add_argument('--credentials-file',required=True,help='Private runtime.env outside Git')
parser.add_argument('--live-llm',action='store_true',help='One chat plus automatic title may consume provider balance')
parser.add_argument('--replay-turn',nargs=2,metavar=('SESSION','REQUEST'),help='Validate an existing identical turn without a new LLM call')
args=parser.parse_args()
origin=urllib.parse.urlsplit(args.url)
if origin.username or origin.password or (origin.scheme!='https' and not(origin.scheme=='http' and origin.hostname in ('127.0.0.1','localhost'))):
    parser.error('Use verified HTTPS, or loopback HTTP only.')
values=dict(line.split('=',1) for line in Path(args.credentials_file).read_text(encoding='utf-8').splitlines() if '=' in line)
base=args.url.rstrip('/')
opener=urllib.request.build_opener(urllib.request.ProxyHandler({}))
token=''; organization=1
def request(path,data=None,method=None,expected=200,anonymous=False,raw=False):
    headers={'Content-Type':'application/json'}
    if token and not anonymous:headers.update({'Authorization':'Bearer '+token,'X-Organization-Id':str(organization)})
    req=urllib.request.Request(base+path,data=json.dumps(data).encode() if data is not None else None,headers=headers,method=method)
    try:
        with opener.open(req,timeout=150) as response:
            body=response.read().decode();assert response.status==expected,(path,response.status)
            return body if raw else json.loads(body) if body else None
    except urllib.error.HTTPError as error:
        assert error.code==expected,(path,error.code)
        return None

for attempt in range(60):
    try:
        if request('/health/ready')['status']=='ready':break
    except (AssertionError,OSError):pass
    time.sleep(1)
else:raise RuntimeError('Portal did not become ready within 60 seconds')
assert request('/health/live')['status']=='ok'
assert request('/health/ready')['status']=='ready'
request('/api/kb/all',expected=401,anonymous=True)
request('/api/data-connectors',expected=401,anonymous=True)
auth=request('/api/auth/login',{'email':values['Bootstrap__AdminEmail'],'password':values['Bootstrap__AdminPassword']})
token=auth['token'];organization=auth['organizationId']
assert request('/api/data/sources')==[]
request('/api/data-connectors',expected=503)
request('/api/ocr/analisar',{},expected=503)
request('/api/kb/reindex-all',{},expected=503)
request('/api/sql/ask',{},expected=503)
request('/api/knowledge-templates/technical-support/apply',{})
item=request('/api/kb/upsert',{'keyName':'micro-validation-public','section':'Demonstração sintética','title':'Validação da instalação mínima','content':'Documento sintético para validação. O código MICRO de validação do portal é MICRO-ORBITA-42.','isActive':True})
assert any(row['id']==item['id'] for row in request('/api/kb/all'))
print('PASS: health/readiness/login, authorization, textual KB/template, minimal feature gates. Synthetic validation document retained.')
def read_work(_):
    started=time.perf_counter();assert isinstance(request('/api/kb/all'),list)
    return time.perf_counter()-started
with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:elapsed=list(pool.map(read_work,range(24)))
print('Authenticated read smoke: 24 requests, concurrency 3, failures 0; median %.3fs, max %.3fs'%(statistics.median(elapsed),max(elapsed)))
if args.live_llm or args.replay_turn:
    if args.replay_turn:session,operation=args.replay_turn
    else:session=request('/api/chatBot/sessions',{},expected=201)['id'];operation=str(uuid.uuid4())
    started=time.perf_counter()
    output=request('/api/ia/ask/stream',{'sessionId':session,'requestId':operation,'pergunta':'Informe o código MICRO de validação do portal usando somente a base documental.'},raw=True)
    events=[line.partition(':')[2].strip() for line in output.splitlines() if line.startswith('event:')]
    assert 'error' not in events and 'done' in events,'Stream failed'
    history=request('/api/chatBot/history/'+session)
    assert any(m['role']=='assistant' and 'MICRO-ORBITA-42' in m['content'] for m in history)
    print('PASS: external LLM answer/SSE, grounded synthetic code, persisted history%s; elapsed %.2fs. Usage is available in the portal.'%(' (idempotent replay, no new inference)' if args.replay_turn else '',time.perf_counter()-started))
print('No credential values or JWT were printed.')
