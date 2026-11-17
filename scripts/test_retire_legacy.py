"""Linux-only plan checks; never touches the real deployment or Docker."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('retire', Path(__file__).resolve().parents[1] / 'infra/local/retire-legacy-stack.py')
retire = importlib.util.module_from_spec(spec)
spec.loader.exec_module(retire)


class RetirementTests(unittest.TestCase):
    def test_protect_data_active_mounts_and_symlinks(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp).resolve()
            releases = root / 'releases'
            releases.mkdir()
            active = releases / ('a' * 40 + '.AAAAAA')
            active.mkdir()
            old = releases / ('b' * 40 + '.BBBBBB')
            old.mkdir()
            mounted = releases / ('c' * 40 + '.CCCCCC')
            mounted.mkdir()
            (mounted / 'mounted.json').touch()
            for name in ('models', 'migration-backups', 'data', 'uploads'):
                (root / name).mkdir()
                (root / name / 'keep').touch()
            (root / '.env').write_text('fixture-only')
            (root / '.env.production').write_text('fixture-only')
            code = root / 'front'
            code.mkdir()
            (code / 'app.js').touch()
            private = root / 'unclassified'
            private.mkdir()
            (private / 'saved.db').touch()
            link = root / 'symlink'
            link.symlink_to(root / 'models', target_is_directory=True)
            delete, keep = retire.plan_paths(root, active, [mounted / 'mounted.json'])
            self.assertEqual(set(delete), {code, old})
            self.assertEqual({Path(p) for p, _ in keep}, {mounted, private, link, root / '.env.production'})


if __name__ == '__main__':
    unittest.main()
