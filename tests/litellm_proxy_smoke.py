"""Real pinned LiteLLM proxy + synthetic upstream containers, no production volumes."""
import json
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import time
import urllib.request
import urllib.error
import uuid

ROOT = Path(__file__).resolve().parents[1]
IMAGE = 'docker.litellm.ai/berriai/litellm:v1.90.2@sha256:19a879f9c4ed466f2438aa08b22e2b472053ab57c6325db73c71d0624d2d4953'
HTTP = urllib.request.build_opener(urllib.request.ProxyHandler({}))


def docker(*args):
    result = subprocess.run(['docker', *args], text=True, capture_output=True)
    if result.returncode:
        raise RuntimeError('Docker command failed: ' + result.stderr[-1500:])
    return result.stdout.strip()


def main():
    suffix = uuid.uuid4().hex[:10]
    network, fake, proxy = (p + suffix for p in ('portal-llm-net-', 'portal-llm-fake-', 'portal-llm-proxy-'))
    with socket.socket() as sock:
        sock.bind(('127.0.0.1', 0)); port = sock.getsockname()[1]
    base = f'http://127.0.0.1:{port}'
    key = 'sk-' + uuid.uuid4().hex
    with tempfile.TemporaryDirectory(prefix='portal-litellm-') as temp:
        folder = Path(temp)
        try:
            docker('network', 'create', network)
            docker('run', '-d', '--name', fake, '--network', network, '--network-alias', 'upstream',
                   '--mount', f'type=bind,source={ROOT / "tests/LiteLlm/fake_upstream.py"},target=/fake.py,readonly',
                   'python:3.12-slim', 'python', '/fake.py')
            models, fallbacks = [], []
            for alias, failure in [('fast', 'fail429'), ('balanced', 'fail503'), ('reasoning', 'timeout')]:
                for group, model in [(alias, failure), (alias+'-backup', 'healthy')]:
                    models.append({'model_name': group, 'litellm_params': {'model': 'openai/'+model,
                        'api_base': 'http://upstream:8080/v1', 'api_key': 'synthetic', 'max_retries': 0,
                        'timeout': .5 if model == 'timeout' else 10, 'cooldown_time': 60}, 'model_info': {'id': 'fixture|'+model+'|'+group}})
                fallbacks.append({alias: [alias+'-backup']})
            models.append({'model_name': 'groq-check', 'litellm_params': {'model': 'groq/openai/gpt-oss-20b',
                'api_base': 'http://upstream:8080/v1', 'api_key': 'synthetic', 'max_retries': 0},
                'model_info': {'id': 'groq|openai/gpt-oss-20b|groq-check'}})
            # Use the same retry/cooldown structure as production, with synthetic deployments.
            import importlib.util
            spec = importlib.util.spec_from_file_location('configure', ROOT / 'infra/litellm/configure.py')
            configure = importlib.util.module_from_spec(spec); spec.loader.exec_module(configure)
            policy = json.loads((ROOT / 'infra/litellm/routes.json').read_text())
            config = configure.compile_config(policy, {'OPENROUTER_API_KEY': 'fixture', 'OPENROUTER_FREE_MODEL': 'openrouter/free'})
            config['model_list'] = models
            config['router_settings'].update(fallbacks=fallbacks, cooldown_time=60, allowed_fails=2)
            (folder/'config.json').write_text(json.dumps(config), encoding='utf-8')
            docker('run', '-d', '--name', proxy, '--network', network,
                   '-p', f'127.0.0.1:{port}:4000', '-e', 'LITELLM_MASTER_KEY='+key,
                   '-e', 'PYTHONPATH=/app/portal', '-e', 'PORTAL_LLM_AUDIT_PATH=/test/attempts.jsonl', '-e', 'LITELLM_LOG=CRITICAL',
                   '--mount', f'type=bind,source={folder},target=/test',
                   '--mount', f'type=bind,source={ROOT / "infra/litellm"},target=/app/portal,readonly',
                   IMAGE, '--config', '/test/config.json', '--port', '4000')
            deadline = time.monotonic()+90
            while True:
                try:
                    with HTTP.open(base+'/health/liveliness', timeout=2): break
                except OSError:
                    if time.monotonic()>deadline:
                        raise RuntimeError('Proxy startup failed: '+docker('logs','--tail','15',proxy))
                    time.sleep(1)

            def request(model, stream=False, tools=False, organization=7, token=key):
                body = {'model': model, 'messages': [{'role': 'user', 'content': 'SYNTHETIC_PRIVATE_PROMPT'}], 'stream': stream,
                    'metadata': {'organization_id': organization, 'request_id': 'synthetic-request', 'portal_call_id': 'call-'+model+'-'+str(organization)}}
                if stream: body['stream_options'] = {'include_usage': True}
                if tools:
                    body['tools'] = [{'type':'function','function':{'name':'synthetic_lookup','parameters':{'type':'object','properties':{'id':{'type':'integer'}}}}}]
                req = urllib.request.Request(base+'/v1/chat/completions', data=json.dumps(body).encode(), headers={'Authorization':'Bearer '+token,'Content-Type':'application/json'})
                try:
                    with HTTP.open(req, timeout=45) as response:
                        return response.read().decode(), dict(response.headers)
                except urllib.error.HTTPError as error:
                    if model == 'groq-check' and token == key:
                        print('Synthetic Groq error:', error.read().decode()[:1800].replace(key,'[redacted]'))
                    raise

            for alias in ['fast','balanced','reasoning']:
                body, headers = request(alias)
                data = json.loads(body)
                assert data['choices'][0]['message']['content']=='Synthetic answer', alias
                assert data['usage']['total_tokens']==10, 'Winning tokens duplicated'
                assert 'healthy' in headers.get('x-litellm-model-id',''), headers
            def counts():
                return json.loads(docker('exec',fake,'python','-c',"import urllib.request; print(urllib.request.urlopen('http://localhost:8080').read().decode())"))
            before = counts()
            request('fast'); request('fast')
            after = counts()
            # The router counts failures at its call boundary; SDK attempts are counted separately.
            assert after['fail429'] <= before['fail429'] + 4, f'Retry budget exceeded: {before} -> {after}'
            request('fast')
            assert counts()['fail429'] == after['fail429'], 'Cooled deployment received another call'
            assert 1 <= before['fail429'] <= 2, 'Retry budget exceeded'
            body, _ = request('groq-check')
            assert json.loads(body)['usage']['total_tokens']==10
            body, _ = request('groq-check',stream=True)
            assert 'Synthetic answer' in body and '[DONE]' in body
            body, _ = request('groq-check',tools=True)
            assert json.loads(body)['choices'][0]['message']['tool_calls'][0]['function']['name']=='synthetic_lookup'
            body, _ = request('groq-check',stream=True,tools=True,organization=8)
            assert 'synthetic_lookup' in body and '[DONE]' in body
            before_auth = counts()
            try:
                request('groq-check',token='invalid')
                raise AssertionError('Unauthenticated inference allowed')
            except urllib.error.HTTPError as error:
                # This pinned proxy uses 400 for an unknown virtual key without a DB.
                assert error.code in (400,401,403)
            assert counts() == before_auth, 'Invalid authentication reached upstream'
            try:
                request('unauthorized-paid')
                raise AssertionError('Unconfigured paid deployment allowed')
            except urllib.error.HTTPError as error:
                assert error.code in (400,403,404)
            deadline=time.monotonic()+10
            while True:
                events=[json.loads(line) for line in (folder/'attempts.jsonl').read_text().splitlines()]
                if any(e['phase']=='completed' and e.get('organization_id')==8 for e in events): break
                if time.monotonic()>deadline: raise AssertionError('Tenant 8 attempt missing')
                time.sleep(.5)
            assert any(e['phase']=='failed' for e in events), 'Failed upstream attempts missing'
            assert any(e['phase']=='completed' and e.get('organization_id')==7 for e in events)
            assert any(e['phase']=='completed' and e.get('provider')=='groq' for e in events), 'Effective Groq provider missing'
            assert any(e['phase']=='fallback_completed' for e in events), 'Fallback events missing'
            audit=(folder/'attempts.jsonl').read_text()
            assert 'SYNTHETIC_PRIVATE_PROMPT' not in audit and key not in audit and 'arguments' not in audit
            print('PASS: real LiteLLM proxy, 429/503/timeout fallbacks, Groq adapter, SSE + tool calls (plain/stream), winning tokens, auth/unknown paid route denied, tenant-correlated attempt ledger without prompt/key/tool arguments. Synthetic providers only.')
        finally:
            for name in (proxy,fake):
                subprocess.run(['docker','rm','-f',name],capture_output=True)
            subprocess.run(['docker','network','rm',network],capture_output=True)


if __name__=='__main__':
    main()
