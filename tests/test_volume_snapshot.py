import importlib.util
from contextlib import closing
import json
from pathlib import Path
import sqlite3
import tarfile
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('snapshot', Path(__file__).resolve().parents[1]/'infra/local/volume_snapshot.py')
snapshot=importlib.util.module_from_spec(spec);spec.loader.exec_module(snapshot)


class SnapshotTests(unittest.TestCase):
    def test_sqlite_files_and_counts_roundtrip_no_overwrite(self):
        with tempfile.TemporaryDirectory() as temp:
            base=Path(temp);source=base/'source';target=base/'target';source.mkdir();target.mkdir()
            with closing(sqlite3.connect(source/'portal.db')) as db:
                db.execute('CREATE TABLE items(id INTEGER PRIMARY KEY, value TEXT)')
                db.execute("INSERT INTO items VALUES(1,'synthetic')")
                db.commit()
            (source/'file.txt').write_text('synthetic document')
            (source/'SingletonLock').write_text('ephemeral browser lock')
            archive=base/'snapshot.tar.gz';manifest=snapshot.create(source,archive)
            snapshot.restore(target,archive,manifest)
            self.assertEqual(snapshot.inventory(target),manifest)
            self.assertFalse((target/'SingletonLock').exists())
            self.assertEqual(manifest['tables']['items'],1)
            with self.assertRaisesRegex(ValueError,'not empty'):
                snapshot.restore(target,archive,manifest)

    def test_archive_cannot_escape_destination(self):
        with tempfile.TemporaryDirectory() as temp:
            base=Path(temp);target=base/'target';target.mkdir();archive=base/'bad.tar.gz'
            with tarfile.open(archive,'w:gz') as tar:
                member=tarfile.TarInfo('../escape.txt');member.size=0;tar.addfile(member)
            with self.assertRaises(tarfile.FilterError):
                snapshot.restore(target,archive,{'files':{}})
            self.assertFalse((base/'escape.txt').exists())

    def test_committed_wal_is_backed_up_without_deleting_source(self):
        with tempfile.TemporaryDirectory() as temp:
            base=Path(temp);source=base/'source';target=base/'target';source.mkdir();target.mkdir()
            with closing(sqlite3.connect(source/'portal.db')) as db:
                db.execute('PRAGMA journal_mode=WAL')
                db.execute('CREATE TABLE items(id INTEGER)');db.execute('INSERT INTO items VALUES(42)');db.commit()
                archive=base/'snapshot.tar.gz';manifest=snapshot.create(source,archive)
                snapshot.restore(target,archive,manifest)
                self.assertEqual(manifest['tables']['items'],1)
                with tarfile.open(archive) as tar:
                    self.assertFalse(any(m.name.endswith(('portal.db-wal','portal.db-shm')) for m in tar.getmembers()))
                self.assertEqual(db.execute('SELECT id FROM items').fetchone(),(42,))


if __name__=='__main__':unittest.main()
