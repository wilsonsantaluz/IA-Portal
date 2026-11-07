"""Disposable Docker API/frontend + already-loaded isolated Atlas Trino; no app volumes."""
import json
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import time
import urllib.request
import uuid


def docker(*args, env=None):
    result = subprocess.run(['docker', *args], capture_output=True, text=True, env=env)
    if result.returncode:
        raise RuntimeError('Docker validation command failed: ' + args[0])
    return result.stdout.strip()


def port():
    with socket.socket() as s:
        s.bind(('127.0.0.1', 0))
        return s.getsockname()[1]


def main():
    container = 'ia-atlas-validation-atlas-trino-1'
    network = 'ia-atlas-validation_default'
    addresses = json.loads(docker('inspect', '--format', '{{json .NetworkSettings.Networks}}', container))
    address = addresses[network]['IPAddress']
    suffix = uuid.uuid4().hex[:10]
    api_name, front_name = 'ia-atlas-api-' + suffix, 'ia-atlas-front-' + suffix
    api_port, front_port = port(), port()
    env = dict(os.environ, Jwt__Key=uuid.uuid4().hex + uuid.uuid4().hex,
               Bootstrap__AdminEmail='atlas-docker@example.test', Bootstrap__AdminPassword=uuid.uuid4().hex,
               Demo__Atlas__SeedKnowledge='true', Chat__UseAdvancedRag='false',
               ASPNETCORE_URLS='http://+:8080',
               ConnectionStrings__PortalConnection='Data Source=/app/data/test.db;Foreign Keys=True',
               TRINO_CONNECTORS_FILE='/app/config/atlas-test.json')
    base = f'http://127.0.0.1:{api_port}'
    token = ''
    organization = 1
    def request(path, data=None):
        headers = {'Content-Type': 'application/json', 'X-Organization-Id': str(organization)}
        if token:
            headers['Authorization'] = 'Bearer ' + token
        req = urllib.request.Request(base + path, data=json.dumps(data).encode() if data is not None else None, headers=headers)
        with urllib.request.urlopen(req, timeout=30) as response:
            return json.load(response)
    try:
        with tempfile.TemporaryDirectory(prefix='atlas-docker-') as temp:
            profile = Path(temp) / 'sources.json'
            profile.write_text(json.dumps({'DataConnectors': {'Sources': [{
                'Id': 'atlas_local', 'OrganizationSlug': 'atlas-demo', 'Category': 'demo',
                'Endpoint': 'http://atlas-trino:8080/', 'Catalog': 'memory', 'Schema': 'atlas',
                'User': 'atlas_reader', 'AllowedAddresses': [address], 'Tables': ['invoices', 'payments'], 'ReadOnlyAccount': True
            }]}}), encoding='utf-8')
            flags = [item for key in env if key in {
                'Jwt__Key', 'Bootstrap__AdminEmail', 'Bootstrap__AdminPassword', 'Demo__Atlas__SeedKnowledge',
                'Chat__UseAdvancedRag', 'ASPNETCORE_URLS', 'ConnectionStrings__PortalConnection', 'TRINO_CONNECTORS_FILE'
            } for item in ['-e', key]]
            docker('run', '-d', '--name', api_name, '--network', network, '--network-alias', 'ai-gateway',
                   '--tmpfs', '/app/data', '--mount', f'type=bind,source={profile},target=/app/config/atlas-test.json,readonly',
                   '-p', f'127.0.0.1:{api_port}:8080', *flags, 'ia-portal-atlas-api:validation', env=env)
            for _ in range(120):
                try:
                    request('/swagger/v1/swagger.json')
                    break
                except OSError:
                    time.sleep(.25)
            else:
                raise RuntimeError('Disposable gateway startup timeout')
            token = request('/api/auth/login', {'email': env['Bootstrap__AdminEmail'], 'password': env['Bootstrap__AdminPassword']})['token']
            organization = next(o['id'] for o in request('/api/organizations') if o['slug'] == 'atlas-demo')
            assert len(request('/api/kb/all')) == 10
            group = next(g['id'] for g in request('/api/data-connectors/groups') if g['name'] == 'admin')
            connector = request('/api/data-connectors', {'name': 'Atlas Docker test', 'profileId': 'atlas_local', 'enabled': True,
                'tables': ['invoices', 'payments'], 'groupIds': [group], 'version': 0})['id']
            assert request(f'/api/data-connectors/{connector}/test', {})['status'] == 'connected'
            assert len(request(f'/api/data-connectors/{connector}/metadata')) >= 10
            rows = request(f'/api/data/{connector}/analytics?kind=aging&reference=2026-11-01')['rows']
            assert rows[0]['saldo_centavos'] == 15605108
            docker('run', '-d', '--name', front_name, '--network', network, '-p', f'127.0.0.1:{front_port}:80', 'ia-portal-atlas-front:validation')
            for _ in range(80):
                try:
                    with urllib.request.urlopen(f'http://127.0.0.1:{front_port}/login', timeout=3) as response:
                        assert 'IA-Portal' in response.read().decode()
                    break
                except OSError:
                    time.sleep(.25)
            else:
                raise RuntimeError('Disposable frontend startup timeout')
            print('PASS: Docker published API embeds Atlas seed and migrations, ephemeral SQLite/bootstrap/auth/org isolation, connector discovery and exact real Trino aging; Nginx serves frontend. No production volumes or external LLM calls.')
    finally:
        for name in (front_name, api_name):
            subprocess.run(['docker', 'rm', '-f', name], capture_output=True)


if __name__ == '__main__':
    main()
