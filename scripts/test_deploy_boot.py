"""Linux-only boot orchestration tests; no real Docker, secrets or systemd writes."""
import os
from pathlib import Path
import subprocess
import tempfile
import unittest

SCRIPT = Path(__file__).with_name('deploy-stack.sh').resolve()


class BootTests(unittest.TestCase):
    def test_only_modern_start_retires_legacy_loader(self):
        for case in ('modern', 'legacy', 'stop', 'missing-active'):
            with self.subTest(case=case), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp)
                if case != 'missing-active':
                    (root / 'active').mkdir()
                binary = root / 'bin'
                binary.mkdir()
                docker = binary / 'docker'
                docker.write_text('''#!/bin/bash
echo "$*" >> "$BOOT_LOG"
case "$1" in
 compose)
   if [[ " $* " == *' --services '* ]]; then
     printf '%s\\n' ai-gateway atlas-trino
     [[ "$BOOT_CASE" != legacy ]] || echo atlas-loader
   fi ;;
 ps) echo legacy-container ;;
esac
exit 0
''')
                docker.chmod(0o755)
                conf = root / 'fixture.conf'
                conf.write_text(f'''PORTAL_RELEASE_ROOT='{root}'
PORTAL_PROJECT_NAME=fixture
PORTAL_RUNTIME_ENV=/unused-fixture.env
PORTAL_DATA_VOLUME=fixture
PORTAL_COMPOSE_FILES=()
COMPOSE_PROFILES=
''')
                # Intercept source in this child shell so the real /etc config
                # is never opened. This hook is not used by the deployed script.
                hook = root / 'bash-env'
                hook.write_text('source() { builtin source "$BOOT_FIXTURE"; }\n')
                log = root / 'calls'
                result = subprocess.run(['bash', str(SCRIPT), 'stop' if case == 'stop' else 'start'],
                                        env={**os.environ, 'PATH': str(binary) + ':' + os.environ['PATH'],
                                             'BASH_ENV': str(hook), 'BOOT_FIXTURE': str(conf),
                                             'BOOT_LOG': str(log), 'BOOT_CASE': case},
                                        capture_output=True, text=True)
                if case == 'missing-active':
                    self.assertNotEqual(result.returncode, 0)
                    self.assertFalse(log.exists())
                    continue
                self.assertEqual(result.returncode, 0, result.stderr)
                calls = log.read_text()
                if case == 'modern':
                    self.assertIn('update --restart=no legacy-container', calls)
                    self.assertLess(calls.index('stop --time 30 legacy-container'), calls.index('up -d'))
                else:
                    self.assertNotIn('update --restart=', calls)
                if case != 'stop':
                    self.assertIn('up -d --no-build --pull never --wait', calls)
                else:
                    self.assertIn('stop --timeout 30', calls)
                self.assertNotIn('down', calls)
                self.assertNotIn('rm ', calls)


if __name__ == '__main__':
    unittest.main()
