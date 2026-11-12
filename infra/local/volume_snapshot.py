"""Consistent offline volume snapshots. Stop every writer before invoking this tool."""
import argparse
from contextlib import closing
import hashlib
import json
from pathlib import Path
import sqlite3
import tarfile
import tempfile
import shutil

EPHEMERAL = {'SingletonLock', 'SingletonCookie', 'SingletonSocket', 'DevToolsActivePort', 'portal.db-wal', 'portal.db-shm'}


def inventory(root):
    files = {}
    for path in root.rglob('*'):
        if path.is_file() and not path.is_symlink() and path.name not in EPHEMERAL:
            with path.open('rb') as stream:
                files[path.relative_to(root).as_posix()] = hashlib.file_digest(stream, 'sha256').hexdigest()
    result = {'files': files}
    database = root / 'portal.db'
    if database.exists():
        with closing(sqlite3.connect(database.as_uri() + '?mode=ro', uri=True)) as db:
            if db.execute('PRAGMA integrity_check').fetchone() != ('ok',):
                raise ValueError('SQLite integrity check failed')
            tables = [r[0] for r in db.execute("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'")]
            result['tables'] = {name: db.execute('SELECT count(*) FROM "' + name.replace('"','""') + '"').fetchone()[0] for name in tables}
    return result


def create(root, archive):
    if (root/'portal.db').exists():
        # Normalize committed WAL pages through SQLite backup, never raw-copy a live DB.
        with tempfile.TemporaryDirectory(prefix='portal-snapshot-', dir=archive.parent) as temp:
            staged=Path(temp)
            assert staged.resolve().is_relative_to(archive.parent.resolve())
            shutil.copytree(root,staged,dirs_exist_ok=True,symlinks=True,
                ignore=lambda folder,names: {'portal.db','portal.db-wal','portal.db-shm'} if Path(folder)==root else set())
            with closing(sqlite3.connect((root/'portal.db').as_uri()+'?mode=ro',uri=True)) as source, closing(sqlite3.connect(staged/'portal.db')) as target:
                source.backup(target)
            return pack(staged,archive)
    return pack(root,archive)


def pack(root, archive):
    manifest = inventory(root)
    def select(member):
        if Path(member.name).name in EPHEMERAL or not (member.isfile() or member.isdir() or member.issym() or member.islnk()):
            return None
        return member
    with tarfile.open(archive, 'w:gz', compresslevel=1) as tar:
        tar.add(root, arcname='.', filter=select)
    archive.with_suffix(archive.suffix + '.json').write_text(json.dumps(manifest), encoding='utf-8')
    return manifest


def restore(root, archive, manifest):
    if any(root.iterdir()):
        raise ValueError('Destination volume is not empty; refusing overwrite')
    def safe(member, target):
        checked = tarfile.data_filter(member, target)
        if checked is not None:
            # Numeric ownership is necessary for the unprivileged browser container.
            if not 0 <= member.uid <= 65535 or not 0 <= member.gid <= 65535:
                raise ValueError('Unexpected numeric owner')
            checked.uid, checked.gid = member.uid, member.gid
        return checked
    with tarfile.open(archive, 'r:gz') as tar:
        tar.extractall(root, filter=safe)
    if inventory(root) != manifest:
        raise ValueError('Restored volume verification failed')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action', choices=['create','restore','verify'])
    parser.add_argument('root', type=Path)
    parser.add_argument('archive', type=Path)
    args = parser.parse_args()
    if args.action == 'create':
        result = create(args.root.resolve(), args.archive)
    else:
        result = json.loads(args.archive.with_suffix(args.archive.suffix + '.json').read_text())
        if args.action == 'restore':
            restore(args.root.resolve(), args.archive, result)
        elif inventory(args.root.resolve()) != result:
            raise ValueError('Volume verification failed')
    print('PASS:', args.action, 'snapshot; files:', len(result['files']), '; database integrity/counts checked:', 'tables' in result)
