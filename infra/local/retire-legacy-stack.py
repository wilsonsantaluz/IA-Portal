"""Retire only unreferenced legacy code, stopped loader and unused local image tags.

Dry-run by default. Never deletes volumes, models, environment or backups.
"""
import argparse
import fcntl
import json
import os
from pathlib import Path
import re
import shutil
import subprocess

PROTECTED = {'releases', 'models', 'migration-backups', '.env', 'data', 'uploads', 'backups', 'logs'}
PRIVATE_SUFFIXES = {'.db', '.sqlite', '.sqlite3', '.key', '.pem', '.pfx', '.p12', '.bak', '.back'}


def plan_paths(root, active, mounts):
    delete, keep = [], []
    candidates = [p for p in root.iterdir() if p.name not in PROTECTED]
    candidates += [p for p in (root / 'releases').iterdir()
                   if re.fullmatch(r'[a-f0-9]{40}\.[A-Za-z0-9]{6}', p.name) and p != active]
    for p in candidates:
        if p.is_symlink():
            keep.append((str(p), 'symlink'))
            continue
        # Verify the final path before any recursive operation.
        resolved = p.resolve(strict=True)
        assert resolved.parent in (root, root / 'releases') and resolved != active
        if any(m == resolved or resolved in m.parents for m in mounts):
            keep.append((str(p), 'container mount'))
            continue
        entries = p.rglob('*') if p.is_dir() else [p]
        if any(f.is_symlink() or (f.is_file() and (
                (f.name.startswith('.env') and f.name != '.env.example')
                or f.name.endswith('.env') or f.suffix in PRIVATE_SUFFIXES
                or f.name.endswith(('.db-wal', '.db-shm')))) for f in entries):
            keep.append((str(p), 'private/data file or nested symlink'))
            continue
        delete.append(p)
    return delete, keep


def docker(*args):
    return subprocess.check_output(['docker', *args], text=True).strip()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply', action='store_true')
    parser.add_argument('--expected-active', required=True)
    args = parser.parse_args()
    root = Path('/opt/ia-portal')
    assert not root.is_symlink() and root.resolve() == root
    if args.apply:
        lock = (root / 'releases/.deploy.lock').open('a')
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
    active = (root / 'releases/active').resolve(strict=True)
    assert active.parent == root / 'releases' and active.name == args.expected_active
    assert (active / 'docker-compose.yml').is_file()
    boot = subprocess.check_output(['systemctl', 'show', 'ia-portal-stack', '-p', 'ExecStart', '--value'], text=True)
    assert '/usr/local/libexec/ia-portal-deploy-stack start' in boot, 'Release boot must be installed first'
    ids = docker('ps', '-aq').split()
    containers = json.loads(docker('inspect', *ids))
    mounts = [Path(m['Source']).resolve() for c in containers for m in c['Mounts'] if m.get('Source')]
    delete, keep = plan_paths(root, active, mounts)
    loaders = [c for c in containers if (c['Config'].get('Labels') or {}).get('com.docker.compose.project') == 'ia-portal'
               and c['Config']['Labels'].get('com.docker.compose.service') == 'atlas-loader']
    assert all(not c['State']['Running'] for c in loaders), 'Refuse to remove a running loader'
    used_images = {c['Image'] for c in containers if c not in loaders}
    images = []
    for line in docker('image', 'ls', '--format', '{{.Repository}}:{{.Tag}}').splitlines():
        if line.startswith(('ia-portal-migrated/', 'ia-portal-test/')) or line == 'ia-portal-atlas-loader:latest':
            image_id = json.loads(docker('image', 'inspect', line))[0]['Id']
            if image_id not in used_images:
                images.append(line)
            else:
                keep.append((line, 'image referenced by a retained container'))
    manifest = {'active': str(active), 'delete_code': [str(p) for p in delete],
                'delete_containers': [c['Name'] for c in loaders], 'delete_image_tags': images, 'keep': keep}
    print(json.dumps(manifest, indent=2))
    if not args.apply:
        return
    assert os.geteuid() == 0
    assert (root / 'releases/active').resolve(strict=True) == active
    source = Path(__file__).resolve().parents[2] / 'scripts/deploy-stack.sh'
    subprocess.run(['bash', '-n', str(source)], check=True)
    subprocess.run(['install', '-m', '0755', '-o', 'root', '-g', 'root', str(source),
                    '/usr/local/libexec/ia-portal-deploy-stack'], check=True)
    report = Path('/var/log/ia-portal-legacy-retirement.json')
    report.write_text(json.dumps(manifest, indent=2))
    # Explicit IDs and paths only. No rm -rf root, Docker prune or volume deletion.
    for c in loaders:
        docker('rm', c['Id'])
    for p in delete:
        assert p.resolve(strict=True) == p and p != active
        if p.is_dir():
            shutil.rmtree(p)
        else:
            p.unlink()
    for tag in images:
        result = subprocess.run(['docker', 'image', 'rm', tag], capture_output=True, text=True)
        if result.returncode:
            manifest.setdefault('retained_image_tags', []).append(tag)
    report.write_text(json.dumps(manifest, indent=2))
    print('LEGACY_RETIRED; volumes, private env, models and backups retained.')


if __name__ == '__main__':
    main()
