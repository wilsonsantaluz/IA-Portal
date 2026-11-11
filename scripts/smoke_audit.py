"""Real audit/auth HTTP checks against an isolated gateway and synthetic SQLite."""
import contextlib
import datetime
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
import urllib.parse
import urllib.request

root = Path(__file__).resolve().parents[1]
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    port = sock.getsockname()[1]
opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
with tempfile.TemporaryDirectory(prefix='portal-audit-http-') as folder:
    database = str(Path(folder) / 'portal.db')
    password = secrets.token_urlsafe(24)
    env = {k: v for k, v in os.environ.items() if not k.startswith(('DEEPSEEK_', 'OPENAI_', 'OLLAMA_', 'Bootstrap__', 'ConnectionStrings__', 'Smtp__', 'Signup__', 'BrowserBridge__'))}
    env.update({
        'ASPNETCORE_URLS': f'http://127.0.0.1:{port}',
        'ConnectionStrings__PortalConnection': f'Data Source={database};Foreign Keys=True',
        'Jwt__Key': secrets.token_hex(32), 'Bootstrap__AdminEmail': 'owner@example.test',
        'Bootstrap__AdminPassword': password, 'Demo__Atlas__SeedKnowledge': 'true',
        'Demo__Atlas__PreferOrganization': 'true', 'Chat__UseAdvancedRag': 'false',
        'Signup__Enabled': 'false', 'BrowserBridge__Enabled': 'false',
        'DEEPSEEK_ENABLED': 'false', 'OPENAI_ENABLED': 'false', 'OLLAMA_ENABLED': 'false',
    })
    token, organization = '', 1

    def request(path, data=None, method=None, expected=200, anonymous=False):
        headers = {'Content-Type': 'application/json', 'X-User-Id': '999999'}
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
            request('/api/auth/login?token=SECRET_QUERY', {'email': 'unknown@example.test', 'password': 'SECRET_PASSWORD'}, expected=401, anonymous=True)
            request('/api/kb/all', expected=401, anonymous=True)
            owner = request('/api/auth/login', {'email': 'owner@example.test', 'password': password})
            token, atlas = owner['token'], owner['organizationId']
            assert 'audit.read' in owner['permissions']
            organization = 1
            with contextlib.closing(sqlite3.connect(database)) as db:
                owner_hash = db.execute('SELECT PasswordHash FROM Users WHERE UserEmail=?', ('owner@example.test',)).fetchone()[0]
                for name, role in [('tenant-admin', 'Admin'), ('member', 'Member')]:
                    cursor = db.execute('INSERT INTO Users (UserName,UserEmail,PasswordHash,IsActive,CreatedAt) VALUES (?,?,?,1,datetime("now"))', (name, name + '@example.test', owner_hash))
                    db.execute('INSERT INTO OrganizationMemberships (OrganizationId,UserId,IsActive,PolicyRole,Version) VALUES (?,?,1,?,0)', (atlas, cursor.lastrowid, role))
                db.commit()
            item = request('/api/kb/save', {'keyName': 'audit-only', 'section': 'Synthetic', 'title': 'Synthetic', 'content': 'SECRET_DOCUMENT'})
            request('/api/kb/' + str(item['id']))
            request('/api/secret-raw-token/SECRET_RAW_PATH?token=SECRET_QUERY', expected=404)
            root_events = request('/api/audit/actions')
            assert root_events['scope'] == 'installation'
            assert any(e['operation'] == 'Auth.Login' and e['outcome'] == 'failure' and e['actorUserId'] is None for e in root_events['items'])
            assert any(e['operation'] == 'Auth.Login' and e['actorUserId'] == owner['userId'] for e in root_events['items'])
            assert any(e['organizationId'] == 1 and e['actorUserId'] == owner['userId'] for e in root_events['items'])
            assert any(e['operation'] == 'KnowledgeBase.Save' and e['targetId'] == 'document:' + str(item['id']) for e in root_events['items'])
            assert 'SECRET_' not in json.dumps(root_events)
            request('/api/audit/actions?limit=101', expected=400)
            request('/api/audit/actions?from=2020-01-01T00:00:00Z&to=2026-01-01T00:00:00Z', expected=400)
            login_only = request('/api/audit/actions?q=LOGIN&outcome=failure')
            assert login_only['items'] and all(e['operation'] == 'Auth.Login' and e['outcome'] == 'failure' for e in login_only['items'])
            cutoff = datetime.datetime.now(datetime.timezone.utc).isoformat()
            query = '/api/audit/actions?' + urllib.parse.urlencode({'limit': 2, 'to': cutoff})
            first = request(query)
            second = request(query + '&beforeId=' + str(first['nextBeforeId']))
            assert not set(e['id'] for e in first['items']) & set(e['id'] for e in second['items'])
            admin = request('/api/auth/login', {'email': 'tenant-admin@example.test', 'password': password}, anonymous=True)
            token, organization = admin['token'], admin['organizationId']
            scoped = request('/api/audit/actions?organizationId=1')
            assert scoped['scope'] == 'organization' and scoped['items'] and all(e['organizationId'] == atlas for e in scoped['items'])
            member = request('/api/auth/login', {'email': 'member@example.test', 'password': password}, anonymous=True)
            token, organization = member['token'], member['organizationId']
            request('/api/audit/actions', expected=403)
            request('/api/auth/logout', {}, expected=204)
            with contextlib.closing(sqlite3.connect(database)) as db:
                for _ in range(30):
                    found = db.execute("SELECT count(*) FROM ActionAuditEvents WHERE Operation='Auth.Logout' AND ActorUserId=?", (member['userId'],)).fetchone()[0]
                    if found:
                        break
                    time.sleep(.1)
                assert found
                assert not db.execute('SELECT count(*) FROM ActionAuditEvents WHERE ActorUserId=999999').fetchone()[0]
                encoded = json.dumps(db.execute('SELECT * FROM ActionAuditEvents').fetchall())
                assert all(secret not in encoded for secret in ['SECRET_PASSWORD', 'SECRET_DOCUMENT', 'SECRET_QUERY', 'SECRET_RAW_PATH', password, token])
            print('PASS: real HTTP login success/failure/logout; admin search/filter/cursor/bounds; tenant isolation and forged org/user ignored; member 403; no passwords/tokens/query/document/raw-path contents persisted. No external services.')
        finally:
            process.terminate()
            try:
                process.wait(timeout=15)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()
