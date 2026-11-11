"""Isolated signup/authorization HTTP checks. No live database, SMTP or LLM calls."""
import hashlib
import json
import os
from pathlib import Path
import secrets
import socket
import sqlite3
import subprocess
import tempfile
import time
import urllib.error
import urllib.request

root = Path(__file__).resolve().parents[1]
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    port = sock.getsockname()[1]
opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
with tempfile.TemporaryDirectory(prefix='portal-signup-http-') as folder:
    database = str(Path(folder) / 'portal.db')
    password = secrets.token_urlsafe(24)
    env = {k: v for k, v in os.environ.items() if not k.startswith(('DEEPSEEK_', 'OPENAI_', 'OLLAMA_', 'Bootstrap__', 'ConnectionStrings__', 'Smtp__', 'Signup__'))}
    env.update({
        'ASPNETCORE_URLS': f'http://127.0.0.1:{port}',
        'ConnectionStrings__PortalConnection': f'Data Source={database};Foreign Keys=True',
        'Jwt__Key': secrets.token_hex(32), 'Bootstrap__AdminEmail': 'owner@example.test',
        'Bootstrap__AdminPassword': password, 'Demo__Atlas__SeedKnowledge': 'true',
        'Demo__Atlas__PreferOrganization': 'true', 'Chat__UseAdvancedRag': 'false',
        'Signup__Enabled': 'true', 'Signup__PublicUrl': 'https://portal.example.test',
        'Smtp__Username': 'sender@example.test', 'Smtp__AppPassword': 'synthetic-unused',
        'DEEPSEEK_ENABLED': 'true', 'DEEPSEEK_API_KEY': 'synthetic-never-used', 'OPENAI_ENABLED': 'false', 'OLLAMA_ENABLED': 'false',
    })
    token = ''
    organization = None

    def request(path, data=None, method=None, expected=200, anonymous=False):
        headers = {'Content-Type': 'application/json'}
        if token and not anonymous:
            headers.update({'Authorization': 'Bearer ' + token, 'X-Organization-Id': str(organization)})
        req = urllib.request.Request(f'http://127.0.0.1:{port}' + path,
            data=json.dumps(data).encode() if data is not None else None, headers=headers, method=method)
        try:
            with opener.open(req, timeout=15) as response:
                assert response.status == expected, (path, response.status, expected)
                body = response.read()
                return json.loads(body) if body else None
        except urllib.error.HTTPError as ex:
            assert ex.code == expected, (path, ex.code, expected)

    with open(Path(folder) / 'gateway.log', 'w') as log:
        process = subprocess.Popen(['dotnet', str(root / 'ai-gateway/bin/Debug/net8.0/AiGateway.dll')],
            cwd=root / 'ai-gateway', env=env, stdout=log, stderr=subprocess.STDOUT)
        try:
            for _ in range(80):
                if process.poll() is not None:
                    raise RuntimeError('Isolated gateway exited during startup')
                try:
                    request('/health/ready')
                    break
                except OSError:
                    time.sleep(.25)
            else:
                raise RuntimeError('Isolated gateway did not start')
            owner = request('/api/auth/login', {'email': 'owner@example.test', 'password': password})
            token, organization = owner['token'], owner['organizationId']
            owner_session = request('/api/chatBot/sessions', {}, expected=201)['id']
            assert request('/api/auth/signup/status', anonymous=True)['enabled']
            code = secrets.token_hex(32).upper()
            with sqlite3.connect(database) as db:
                owner_hash = db.execute('SELECT PasswordHash FROM Users WHERE UserEmail=?', ('owner@example.test',)).fetchone()[0]
                db.execute('INSERT INTO SignupRequests VALUES (?,?,?,?,datetime("now"),datetime("now","+30 minutes"))',
                    (hashlib.sha256(code.encode()).hexdigest().upper(), 'visitor@example.test', 'Synthetic Visitor', owner_hash))
            db.close()
            request('/api/auth/login', {'email': 'visitor@example.test', 'password': password}, expected=401, anonymous=True)
            request('/api/auth/signup/confirm', {'token': code, 'organizationId': 1, 'role': 'Admin'}, anonymous=True)
            request('/api/auth/signup/confirm', {'token': code}, expected=400, anonymous=True)
            visitor = request('/api/auth/login', {'email': 'visitor@example.test', 'password': password}, anonymous=True)
            token, organization = visitor['token'], visitor['organizationId']
            assert organization == owner['organizationId'] and organization != 1
            assert 'kb.edit' in visitor['permissions'] and 'kb.manage' not in visitor['permissions']
            request('/api/kb/all')
            item = request('/api/kb/upsert', {'keyName': 'signup-test', 'section': 'Synthetic', 'content': 'Created', 'isActive': False})
            assert request('/api/kb/' + str(item['id']))['isActive']
            request('/api/kb/upsert', {'id': item['id'], 'keyName': 'signup-test', 'section': 'Synthetic', 'content': 'Edited', 'isActive': False})
            result = request('/api/kb/' + str(item['id']))
            assert result['content'] == 'Edited' and result['isActive']
            request('/api/kb/toggle/' + str(item['id']), {}, expected=403)
            request('/api/kb/reindex-all', {}, expected=403)
            request('/api/organizations/current/members', expected=403)
            request('/api/roles/1', method='DELETE', expected=403)
            request('/api/chatBot/sessions/' + owner_session, expected=404)
            request('/api/chatBot/sessions/' + owner_session, method='DELETE', expected=404)
            organization = 1
            request('/api/kb/all', expected=401)
            for _ in range(3):
                request('/api/auth/signup/confirm', {'token': 'invalid'}, expected=400, anonymous=True)
            request('/api/auth/signup/confirm', {'token': 'invalid'}, expected=429, anonymous=True)
            request('/api/auth/signup/status', anonymous=True)
            print('PASS: real HTTP confirmation/login, tenant injection ignored, create/edit allowed, IsActive bypass blocked, deactivate/reindex/admin denied, private chat isolation, other tenant denied, signup rate limit. No SMTP/LLM calls.')
        finally:
            process.terminate()
            try:
                process.wait(timeout=15)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
