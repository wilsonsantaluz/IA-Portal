"""Consistent on-VM SQLite recovery snapshot; never prints account data/secrets."""
from datetime import datetime,timezone
import os
from pathlib import Path
import shutil
import sqlite3

if os.geteuid()!=0:raise SystemExit('Run through sudo on the deployment VM.')
os.umask(0o077)
root=Path('/opt/ia-portal')
source=root/'data/portal.db'
if not source.is_file():raise SystemExit('No existing portal database; refusing an empty snapshot.')
destination=root/'recovery-backups'/datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
destination.mkdir(mode=0o700,parents=True,exist_ok=False)
with sqlite3.connect(source.as_uri()+'?mode=ro',uri=True,timeout=30) as live:
    with sqlite3.connect(destination/'portal.db') as backup:
        live.backup(backup)
        if backup.execute('PRAGMA integrity_check').fetchone()[0]!='ok':raise SystemExit('Backup integrity failed.')
        violations=backup.execute('PRAGMA foreign_key_check').fetchall()
        if violations:raise SystemExit('Backup foreign-key integrity failed.')
        for table in ['Users','Organizations','OrganizationMemberships','KnowledgeBaseItem','ChatSession','ChatMessage']:
            count=backup.execute('SELECT count(*) FROM '+table).fetchone()[0]
            print(table+': '+str(count)+' rows preserved')
if (root/'runtime.env').is_file():shutil.copyfile(root/'runtime.env',destination/'runtime.env')
print('Private consistent recovery snapshot:',destination)
print('Integrity and foreign keys: ok. No credentials, content or password hashes displayed.')
