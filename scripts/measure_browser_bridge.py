"""Read-only Docker resource samples; does not create sessions or send messages."""
import argparse
import csv
import json
from pathlib import Path
import subprocess
import time
from datetime import datetime, timezone

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--samples', type=int, default=3)
parser.add_argument('--interval', type=int, default=10)
parser.add_argument('--output', default='artifacts/whatsapp-browser-resources.csv')
args = parser.parse_args()
if not 1 <= args.samples <= 360 or not 1 <= args.interval <= 60:
    parser.error('samples:1..360; interval:1..60 seconds')
container_ids = subprocess.check_output(['docker', 'ps', '--filter', 'label=com.docker.compose.project=ia-portal', '--format', '{{.ID}}'], text=True).split()
if not container_ids: raise SystemExit('No running ia-portal Compose containers')
target = Path(args.output); target.parent.mkdir(parents=True, exist_ok=True)
with target.open('w', newline='', encoding='utf-8') as output:
    writer = csv.DictWriter(output, fieldnames=['utc', 'container', 'cpu', 'memory', 'memoryPercent', 'pids'])
    writer.writeheader()
    for sample in range(args.samples):
        rows = subprocess.check_output(['docker', 'stats', '--no-stream', '--format', '{{json .}}', *container_ids], text=True).splitlines()
        for line in rows:
            item = json.loads(line)
            writer.writerow(dict(utc=datetime.now(timezone.utc).isoformat(), container=item['Name'], cpu=item['CPUPerc'], memory=item['MemUsage'], memoryPercent=item['MemPerc'], pids=item['PIDs']))
        output.flush()
        if sample + 1 < args.samples: time.sleep(args.interval)
print(f'PASS: {args.samples} read-only Docker samples written to {target}. Not a capacity guarantee.')
