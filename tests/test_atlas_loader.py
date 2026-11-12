"""Startup readiness and fail-closed behavior without Docker or real data."""
import contextlib
import io
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'scripts'))
import atlas_trino_load as loader


class StopWatch(Exception):
    pass


class LoaderTests(unittest.TestCase):
    def test_partial_schema_is_never_overwritten(self):
        with patch.object(loader, 'query', return_value=[['invoices']]) as query:
            with self.assertRaisesRegex(RuntimeError, 'refusing to overwrite'):
                loader.load()
            self.assertEqual(query.call_count, 1)
            self.assertTrue(query.call_args.args[0].startswith('SELECT'))

    def test_restart_clears_readiness_then_recovers(self):
        with tempfile.TemporaryDirectory() as temp:
            ready = Path(temp) / 'ready'
            observed = []

            def sleep(_):
                observed.append(ready.exists())
                if len(observed) == 3:
                    raise StopWatch()

            with patch.object(loader, 'Path', return_value=ready), \
                 patch.object(loader, 'load', side_effect=[None, OSError(), None]), \
                 patch.object(loader, 'verify') as verify, \
                 patch.object(loader.time, 'sleep', side_effect=sleep), \
                 contextlib.redirect_stdout(io.StringIO()):
                with self.assertRaises(StopWatch):
                    loader.watch()
            self.assertEqual(observed, [True, False, True])
            self.assertEqual(verify.call_count, 2)

    def test_failed_verification_does_not_mark_ready(self):
        with tempfile.TemporaryDirectory() as temp:
            ready = Path(temp) / 'ready'
            with patch.object(loader, 'Path', return_value=ready), \
                 patch.object(loader, 'load'), \
                 patch.object(loader, 'verify', side_effect=AssertionError()), \
                 patch.object(loader.time, 'sleep', side_effect=StopWatch()), \
                 contextlib.redirect_stdout(io.StringIO()):
                with self.assertRaises(StopWatch):
                    loader.watch()
            self.assertFalse(ready.exists())

    def test_fast_restart_still_verifies_new_dataset(self):
        with tempfile.TemporaryDirectory() as temp:
            ready = Path(temp) / 'ready'
            with patch.object(loader, 'Path', return_value=ready), \
                 patch.object(loader, 'load', side_effect=[False, True]), \
                 patch.object(loader, 'verify') as verify, \
                 patch.object(loader.time, 'sleep', side_effect=[None, StopWatch()]), \
                 contextlib.redirect_stdout(io.StringIO()):
                with self.assertRaises(StopWatch):
                    loader.watch()
            self.assertEqual(verify.call_count, 2)


if __name__ == '__main__':
    unittest.main()
