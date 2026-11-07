"""Offline synthetic Atlas v1. Monetary values are integer BRL cents."""
import argparse
import csv
from datetime import date, timedelta
import hashlib
import json
from pathlib import Path
import random

REFERENCE = date(2026, 11, 1)


def generate(seed=42):
    rng = random.Random(seed)
    tables = {name: [] for name in (
        'customers', 'plans', 'contracts', 'invoices', 'payments', 'disputes', 'collection_actions')}
    tables['plans'] = [dict(id=i, name=name, monthly_cents=cents) for i, name, cents in
                       [(1, 'Essencial', 19900), (2, 'Profissional', 59900), (3, 'Enterprise', 249900)]]
    for customer in range(1, 501):
        tables['customers'].append(dict(id=customer, name=f'Cliente ficticio {customer:04}', region=['N','NE','CO','SE','S'][customer % 5]))
        plan = tables['plans'][rng.choices([0, 1, 2], [60, 32, 8])[0]]
        cancel_month = 8 if customer % 19 == 0 else 13
        tables['contracts'].append(dict(id=customer, customer_id=customer, plan_id=plan['id'],
            started_on='2025-11-01', cancelled_on='2026-07-01' if cancel_month == 8 else '',
            status='cancelled' if cancel_month == 8 else 'active'))
        for month in range(12):
            if month >= cancel_month: break
            issued = date(2025 + (month + 10)//12, (month + 10) % 12 + 1, 1)
            due = issued + timedelta(days=14 + customer % 15)
            gross = plan['monthly_cents'] * (5 if customer % 41 == 0 else 1)
            # Synthetic seasonal additional service, explicitly represented in gross.
            if issued.month == 12: gross += gross // 10
            discount = gross // 20 if customer % 13 == 0 else 0
            net = gross - discount
            invoice = len(tables['invoices']) + 1
            cancelled = customer % 47 == 0 and month == 10
            tables['invoices'].append(dict(id=invoice, customer_id=customer, contract_id=customer,
                period=issued.strftime('%Y-%m'), issued_on=issued.isoformat(), due_on=due.isoformat(),
                gross_cents=gross, discount_cents=discount, net_cents=net,
                status='cancelled' if cancelled else 'issued'))
            def payment(amount, paid_on, status='confirmed'):
                if paid_on <= REFERENCE:
                    tables['payments'].append(dict(id=len(tables['payments'])+1, invoice_id=invoice,
                        paid_on=paid_on.isoformat(), amount_cents=amount, status=status))
            scenario = customer % 10
            if not cancelled:
                if scenario < 6: payment(net, due + timedelta(days=[-4, 0, 4, 12, 25, 45][scenario]))
                elif scenario == 6: payment(net // 2, due + timedelta(days=7))
                elif scenario == 7:
                    payment(net, due)
                    payment(net, due + timedelta(days=5), 'reversed')
                elif scenario == 8: payment(net, due + timedelta(days=100))
                if scenario == 9:
                    tables['disputes'].append(dict(id=len(tables['disputes'])+1, invoice_id=invoice,
                        opened_on=(due+timedelta(days=2)).isoformat(), reason='Revisao sintetica do servico', status='open'))
                confirmed = sum(p['amount_cents'] * (-1 if p['status']=='reversed' else 1)
                                for p in tables['payments'] if p['invoice_id']==invoice)
                if net > confirmed and due < REFERENCE:
                    tables['collection_actions'].append(dict(id=len(tables['collection_actions'])+1,
                        invoice_id=invoice, action_on=REFERENCE.isoformat(),
                        action='negotiation_proposed' if scenario == 6 else 'human_review', status='proposed'))
    validate(tables)
    return tables


def validate(tables):
    keys = {name: {r['id'] for r in rows} for name, rows in tables.items()}
    for name, rows in tables.items():
        assert len(keys[name]) == len(rows), f'Duplicate {name}'
    for contract in tables['contracts']:
        assert contract['customer_id'] in keys['customers'] and contract['plan_id'] in keys['plans']
    contracts = {r['id']: r for r in tables['contracts']}
    for invoice in tables['invoices']:
        assert invoice['customer_id'] == contracts[invoice['contract_id']]['customer_id']
        assert invoice['net_cents'] == invoice['gross_cents'] - invoice['discount_cents'] > 0
        assert invoice['issued_on'] < invoice['due_on'] <= REFERENCE.isoformat()
        cancelled = contracts[invoice['contract_id']]['cancelled_on']
        assert not cancelled or invoice['issued_on'] < cancelled
    invoices = {r['id']: r for r in tables['invoices']}
    balances = {i: 0 for i in invoices}
    for p in tables['payments']:
        assert p['invoice_id'] in invoices and p['amount_cents'] > 0
        assert invoices[p['invoice_id']]['issued_on'] <= p['paid_on'] <= REFERENCE.isoformat()
        assert invoices[p['invoice_id']]['status'] != 'cancelled'
        balances[p['invoice_id']] += p['amount_cents'] * (-1 if p['status']=='reversed' else 1)
        assert 0 <= balances[p['invoice_id']] <= invoices[p['invoice_id']]['net_cents']
    for name, field in [('disputes','opened_on'), ('collection_actions','action_on')]:
        for row in tables[name]:
            assert row['invoice_id'] in invoices
            assert invoices[row['invoice_id']]['issued_on'] <= row[field] <= REFERENCE.isoformat()


def write_dataset(output, seed=42):
    # Refuse existing output: no silent replacement of operator files.
    output = Path(output)
    output.mkdir(parents=True, exist_ok=False)
    tables = generate(seed)
    manifest = dict(version=1, seed=seed, reference_date=REFERENCE.isoformat(),
                    origin='synthetic-demo', currency='BRL', money_unit='cents', tables={})
    for name, rows in tables.items():
        path = output / (name + '.csv')
        with path.open('w', newline='', encoding='utf-8') as handle:
            writer = csv.DictWriter(handle, fieldnames=list(rows[0]), lineterminator='\n')
            writer.writeheader()
            writer.writerows(rows)
        manifest['tables'][name] = dict(rows=len(rows), sha256=hashlib.sha256(path.read_bytes()).hexdigest())
    (output / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    return manifest


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True)
    parser.add_argument('--seed', type=int, default=42)
    args = parser.parse_args()
    print(json.dumps(write_dataset(args.output, args.seed), indent=2))
