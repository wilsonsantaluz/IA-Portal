import copy
from datetime import date
import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('atlas', Path(__file__).resolve().parents[1] / 'scripts/atlas_dataset.py')
atlas = importlib.util.module_from_spec(spec)
spec.loader.exec_module(atlas)


class DatasetTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.tables = atlas.generate()

    def test_determinism_and_population(self):
        self.assertEqual(self.tables, atlas.generate())
        self.assertNotEqual(self.tables, atlas.generate(43))
        self.assertEqual(len(self.tables['customers']), 500)
        self.assertEqual(len({r['period'] for r in self.tables['invoices']}), 12)

    def test_scenarios_and_aging(self):
        paid = {}
        for p in self.tables['payments']:
            paid[p['invoice_id']] = paid.get(p['invoice_id'], 0) + p['amount_cents'] * (-1 if p['status']=='reversed' else 1)
        buckets = set()
        partial = False
        for i in self.tables['invoices']:
            amount = paid.get(i['id'], 0)
            partial |= 0 < amount < i['net_cents']
            if i['status'] != 'cancelled' and amount < i['net_cents']:
                days = (atlas.REFERENCE - date.fromisoformat(i['due_on'])).days
                buckets.add(next((b for b in [15,30,60,90] if days <= b), 91))
        self.assertEqual(buckets, {15,30,60,90,91})
        self.assertTrue(partial)
        self.assertTrue(any(p['status']=='reversed' for p in self.tables['payments']))
        self.assertTrue(any(c['status']=='cancelled' for c in self.tables['contracts']))
        self.assertTrue(self.tables['disputes'] and self.tables['collection_actions'])

    def test_corruption_rejected(self):
        bad = copy.deepcopy(self.tables)
        bad['payments'][0]['amount_cents'] = 10**12
        with self.assertRaises(AssertionError): atlas.validate(bad)
        bad = copy.deepcopy(self.tables)
        bad['contracts'][0]['customer_id'] = -1
        with self.assertRaises(AssertionError): atlas.validate(bad)

    def test_files_reproducible_and_no_overwrite(self):
        with tempfile.TemporaryDirectory() as tmp:
            a, b = Path(tmp)/'a', Path(tmp)/'b'
            self.assertEqual(atlas.write_dataset(a), atlas.write_dataset(b))
            for path in a.iterdir(): self.assertEqual(path.read_bytes(), (b/path.name).read_bytes())
            with self.assertRaises(FileExistsError): atlas.write_dataset(a)


if __name__ == '__main__': unittest.main()
