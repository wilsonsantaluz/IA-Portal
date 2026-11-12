"""Initialize only the dedicated loopback Atlas Memory catalog; never the portal DB."""
from datetime import date
import argparse
import hashlib
import json
import time
import urllib.request
from urllib.parse import urlparse
from pathlib import Path
import re
import os

from atlas_dataset import generate

ENDPOINT = os.environ.get('ATLAS_TRINO_ENDPOINT', 'http://127.0.0.1:18081').rstrip('/')
if ENDPOINT not in ('http://127.0.0.1:18081', 'http://atlas-trino:8080'):
    raise ValueError('Only the dedicated local or Compose Atlas Trino endpoint is allowed')
HTTP = urllib.request.build_opener(urllib.request.ProxyHandler({}))


def query(sql, user='atlas_reader', default_schema=None):
    if user not in ('atlas_reader', 'atlas_loader'):
        raise ValueError('Only dedicated demo identities are allowed')
    headers = {'X-Trino-User': user, 'Content-Type': 'text/plain'}
    if default_schema is not None:
        if default_schema != 'atlas':
            raise ValueError('Only the dedicated Atlas schema is allowed')
        headers.update({'X-Trino-Catalog': 'memory', 'X-Trino-Schema': 'atlas'})
    request = urllib.request.Request(ENDPOINT + '/v1/statement', data=sql.encode(), headers=headers)
    deadline = time.monotonic() + 60
    rows = []
    while True:
        if time.monotonic() > deadline:
            raise TimeoutError('Atlas Trino query timeout')
        with HTTP.open(request, timeout=30) as response:
            packet = json.load(response)
        if 'error' in packet:
            raise RuntimeError('Trino demo rejected query: ' + packet['error'].get('errorName', 'unknown'))
        rows.extend(packet.get('data', []))
        next_uri = packet.get('nextUri')
        if not next_uri:
            return rows
        target = urlparse(next_uri)
        if target.scheme != 'http' or target.netloc != urlparse(ENDPOINT).netloc or not target.path.startswith('/v1/statement/') or target.username or target.fragment:
            raise RuntimeError('Unexpected nextUri destination')
        request = urllib.request.Request(next_uri, headers={'X-Trino-User': user})


def load(quiet=False):
    tables = generate()
    fingerprint = hashlib.sha256(json.dumps(tables, sort_keys=True, separators=(',', ':')).encode()).hexdigest()
    existing = {r[0] for r in query("SELECT table_name FROM memory.information_schema.tables WHERE table_schema='atlas'", 'atlas_loader')}
    if existing:
        if existing != set(tables) | {'atlas_manifest'}:
            raise RuntimeError('Existing/partial schema: refusing to overwrite. Use a fresh dedicated demo Trino instance.')
        if query('SELECT fingerprint FROM memory.atlas.atlas_manifest', 'atlas_loader') != [[fingerprint]]:
            raise RuntimeError('Different dataset present: refusing to overwrite')
        for name, rows in tables.items():
            if query(f'SELECT count(*) FROM memory.atlas.{name}', 'atlas_loader') != [[len(rows)]]:
                raise RuntimeError('Dataset changed: refusing to overwrite')
        if not quiet:
            print('PASS: dataset already initialized; no writes, no duplicate rows.')
        return False
    query('CREATE SCHEMA IF NOT EXISTS memory.atlas', 'atlas_loader')
    for name, rows in tables.items():
        fields = list(rows[0])
        types = {field: 'BIGINT' if field == 'id' or field.endswith(('_id', '_cents'))
                 else 'DATE' if field.endswith('_on') else 'VARCHAR' for field in fields}
        definitions = ','.join(f'"{field}" {types[field]}' for field in fields)
        query(f'CREATE TABLE memory.atlas.{name} ({definitions})', 'atlas_loader')
        def literal(field, value):
            if value == '':
                return 'NULL'
            if types[field] == 'BIGINT':
                return str(int(value))
            if types[field] == 'DATE':
                return "DATE '" + date.fromisoformat(value).isoformat() + "'"
            return "'" + str(value).replace("'", "''") + "'"
        for offset in range(0, len(rows), 200):
            values = ','.join('(' + ','.join(literal(field, row[field]) for field in fields) + ')'
                              for row in rows[offset:offset + 200])
            query(f'INSERT INTO memory.atlas.{name} VALUES {values}', 'atlas_loader')
        actual = query(f'SELECT count(*) FROM memory.atlas.{name}')
        if actual != [[len(rows)]]:
            raise RuntimeError('Loaded row count mismatch')
        print(f'PASS: {name}={actual[0][0]}')
    query('CREATE TABLE memory.atlas.atlas_manifest (fingerprint VARCHAR)', 'atlas_loader')
    query("INSERT INTO memory.atlas.atlas_manifest VALUES ('" + fingerprint + "')", 'atlas_loader')
    print('PASS: deterministic synthetic dataset loaded into isolated local Memory catalog.')
    return True


def verify():
    sql = (Path(__file__).resolve().parents[1] / 'trino/examples/atlas-verification.sql').read_text(encoding='utf-8')
    statements = [s.strip() for s in re.sub(r'--[^\n]*', '', sql).split(';') if s.strip()]
    results = [query(statement, default_schema='atlas') for statement in statements]
    expected = {'customers': 500, 'plans': 3, 'contracts': 500, 'invoices': 5896,
                'payments': 5561, 'disputes': 587, 'collection_actions': 2071}
    assert dict(results[0]) == expected
    assert results[1] == [[0]] and results[2] == [[0]]
    assert {r[0]: r[2] for r in results[3]} == {'01-15': 15605108, '16-30': 1140108,
        '31-60': 11006177, '61-90': 9354212, '>90': 60303330}
    print('PASS: all four checked-in verification SELECTs match row counts, referential/financial invariants and exact aging amounts in real local Trino.')


def watch():
    """Recover the disposable demo after Trino restarts; fail closed on partial data."""
    ready = Path('/tmp/atlas-ready')
    healthy = False
    while True:
        ready.unlink(missing_ok=True)
        try:
            created = load(quiet=healthy)
            if not healthy or created:
                verify()
                print('Atlas ready; monitoring for Memory catalog loss.', flush=True)
            ready.touch()
            healthy = True
        except Exception as error:
            # Do not log query bodies or connection configuration.
            print(f'Atlas not ready ({type(error).__name__}); retrying without overwriting existing data.', flush=True)
            healthy = False
        time.sleep(30 if healthy else 5)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--verify', action='store_true')
    parser.add_argument('--watch', action='store_true', help='Monitor and restore the dedicated Compose demo after restarts')
    args = parser.parse_args()
    if args.verify and args.watch:
        parser.error('--verify and --watch are mutually exclusive')
    watch() if args.watch else verify() if args.verify else load()
