"""Run in an isolated helper container against a read-only portal data volume."""
import argparse
from datetime import datetime,timezone
import hashlib
import json
from pathlib import Path
import sqlite3

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument('mode',choices=['before','after'])
args=parser.parse_args()
source=Path('/data/portal.db')
if not source.is_file():raise SystemExit('Existing SQLite file missing; refusing to initialize one.')
directory=Path('/backups');directory.mkdir(exist_ok=True)
with sqlite3.connect(source.as_uri()+'?mode=ro',uri=True,timeout=30) as db:
    version=db.execute('SELECT max(Version) FROM SchemaVersions').fetchone()[0]
    queries={
        'users':'SELECT UserId,UserName,UserEmail,PasswordHash,IsActive,CreatedAt FROM Users ORDER BY UserId',
        'knowledge':'SELECT Id,KeyName,Section,Subsection,Title,Content,IsActive,CreatedAt FROM KnowledgeBaseItem ORDER BY Id',
        'sessions':'SELECT Id,UserId,Title,CreatedAt FROM ChatSession ORDER BY Id',
        'messages':'SELECT Id,SessionId,Role,Content,CreatedAt FROM ChatMessage ORDER BY Id'}
    digests={};counts={}
    for name,query in queries.items():
        rows=db.execute(query).fetchall();counts[name]=len(rows)
        digests[name]=hashlib.sha256(json.dumps(rows,ensure_ascii=True).encode()).hexdigest()
    if args.mode=='before':
        target=directory/('pre-local-channels-'+datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%S')+'.db')
        with sqlite3.connect(target) as backup:
            db.backup(backup)
            assert backup.execute('PRAGMA integrity_check').fetchone()[0]=='ok'
        (directory/'baseline.json').write_text(json.dumps({'version':version,'counts':counts,'digests':digests}))
        print('Consistent private backup created. Schema version:',version,'Counts:',counts)
    else:
        baseline=json.loads((directory/'baseline.json').read_text())
        assert digests==baseline['digests'],'Existing identities/content/history differ; investigate without overwriting data.'
        assert db.execute('PRAGMA integrity_check').fetchone()[0]=='ok'
        assert not db.execute('PRAGMA foreign_key_check').fetchall()
        print('PASS: existing users/password hashes, KB and chat history unchanged; integrity/FKs ok. Schema:',baseline['version'],'->',version,'Counts:',counts)
