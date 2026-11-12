"""Deployment orchestration tests using fake Docker/HTTP, never production data."""
import os
from pathlib import Path
import subprocess
import tempfile
import unittest


SCRIPT = Path(__file__).with_name('deploy-local.sh').resolve()


class DeployTests(unittest.TestCase):
    def test_existing_volume_and_failure_boundaries(self):
        for case in ('missing', 'wrong-volume', 'build-failure', 'success', 'legacy', 'existing-manifest'):
            with self.subTest(case=case), tempfile.TemporaryDirectory() as tmp:
                root = Path(tmp)
                repo = root / 'repo'
                repo.mkdir()
                (repo / 'infra/deploy').mkdir(parents=True)
                (repo / 'infra/deploy/production.yml').write_text('services: {}\n')
                (repo / 'docker-compose.yml').write_text('services: {}\n')
                for args in (['init', '-q'], ['add', '.'],
                             ['-c', 'user.name=Fixture', '-c', 'user.email=fixture@example.invalid',
                              'commit', '-qm', 'fixture']):
                    subprocess.run(['git', *args], cwd=repo, check=True, capture_output=True)
                binary = root / 'bin'
                binary.mkdir()
                docker = binary / 'docker'
                docker.write_text('''#!/bin/bash
echo "$*" >> "$TEST_LOG"
case "$1" in
 ps)
   if [[ " $* " == *'service=atlas-loader'* ]]; then
     [[ "$TEST_CASE" != legacy ]] || echo old-loader
   else
     [[ "$TEST_CASE" == missing ]] || echo gateway
   fi ;;
 inspect) if [[ "$TEST_CASE" == wrong-volume ]]; then echo other; else echo existing; fi ;;
 compose)
   [[ " $* " != *" --services "* ]] || printf '%s\\n' ai-gateway portal-front
   if [[ " $* " == *" --services "* && "$TEST_CASE" == legacy ]]; then echo atlas-trino; fi
   if [[ " $* " == *" build "* && "$TEST_CASE" == build-failure ]]; then exit 1; fi ;;
esac
exit 0
''')
                docker.chmod(0o755)
                curl = binary / 'curl'
                curl.write_text('#!/bin/bash\nexit 0\n')
                curl.chmod(0o755)
                (root / 'runtime.env').touch()
                (root / 'runtime.env').chmod(0o600)
                if case == 'existing-manifest':
                    (root / 'releases').mkdir()
                    manifest = root / 'releases/current-release'
                    manifest.write_text('previous-release\n')
                    manifest.chmod(0o444)
                conf = root / 'deploy.conf'
                conf.write_text(f'''PORTAL_PROJECT_NAME=fixture
PORTAL_DATA_VOLUME=existing
PORTAL_RUNTIME_ENV='{root}/runtime.env'
PORTAL_RELEASE_ROOT='{root}/releases'
PORTAL_COMPOSE_FILES=(docker-compose.yml)
COMPOSE_PROFILES=
PORTAL_FRONT_URL=http://localhost
PORTAL_READY_URL=http://localhost/health/ready
''')
                log = root / 'docker.log'
                result = subprocess.run(['bash', str(SCRIPT), str(conf)], cwd=repo,
                                        env={**os.environ, 'PATH': str(binary) + ':' + os.environ['PATH'],
                                             'TEST_CASE': case, 'TEST_LOG': str(log)},
                                        capture_output=True, text=True)
                calls = log.read_text()
                active = root / 'releases/active'
                if case in ('success', 'legacy', 'existing-manifest'):
                    self.assertEqual(result.returncode, 0, result.stderr)
                    self.assertTrue(active.is_symlink())
                    self.assertTrue((active / 'docker-compose.yml').is_file())
                    # Containers with a different UID must read public code even
                    # though deployment uses umask 027. Private env is excluded.
                    self.assertTrue((active / 'docker-compose.yml').stat().st_mode & 0o004)
                    self.assertTrue((active / 'infra/deploy').stat().st_mode & 0o001)
                    self.assertEqual((root / 'runtime.env').stat().st_mode & 0o777, 0o600)
                    self.assertEqual((root / 'releases/current-release').read_text().strip(), str(active.resolve()))
                    self.assertIn('up -d --no-build --wait', calls)
                    self.assertIn('--env-file ' + str(root / 'runtime.env'), calls)
                    if case == 'legacy':
                        self.assertIn('update --restart=no old-loader', calls)
                        self.assertLess(calls.index('stop --time 30 old-loader'), calls.index('up -d'))
                else:
                    self.assertNotEqual(result.returncode, 0)
                    self.assertFalse(active.exists())
                    self.assertNotIn('up -d', calls)
                self.assertNotIn('down', calls)
                self.assertNotIn('prune', calls)


if __name__ == '__main__':
    unittest.main()
