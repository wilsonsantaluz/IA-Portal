"""Disposable Docker + browser checks for AI settings. Synthetic credentials only."""
from pathlib import Path
import os
import subprocess
import sys
import tempfile
import uuid
import json

ROOT = Path(__file__).resolve().parents[1]

def main():
    tools = Path(tempfile.gettempdir()) / "ia-portal-ui-tools/node_modules"
    env = dict(os.environ)
    env.setdefault("IA_PLAYWRIGHT_MODULE", str(tools / "playwright"))
    env.setdefault("IA_AXE_MODULE", str(tools / "@axe-core/playwright"))
    env.setdefault("IA_CHROME_PATH", str(Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "Google/Chrome/Application/chrome.exe"))
    owned = []
    network = "ia-portal-ai-test"
    network_created = False
    helper = None
    def docker(*args):
        result = subprocess.run(["docker", *args], cwd=ROOT, text=True, capture_output=True)
        if result.returncode:
            # Do not echo full launch arguments; even test credentials should not clutter logs.
            raise RuntimeError("Docker action failed: " + args[0] + ": " + result.stderr[:500])
        return result.stdout.strip()
    try:
        if "--skip-build" not in sys.argv:
            for image, context in [("ia-portal-ai-final-api", "./ai-gateway"), ("ia-portal-ai-final-front", "./front")]:
                subprocess.run(["docker", "build", "-t", image, context], cwd=ROOT, check=True)
        docker("network", "create", network); network_created = True
        helper = subprocess.Popen([sys.executable, "-u", "tests/ChatSessions/mock_provider.py"], cwd=ROOT,
                                  stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
                                  creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
        with tempfile.TemporaryDirectory(prefix="portal-ui-credentials-") as temporary:
            secret = uuid.uuid4().hex + "!Aa9"
            credential_file = Path(temporary) / "credentials.json"
            credential_file.write_text(json.dumps({"email": "providers@example.test", "password": secret}), encoding="utf-8")
            values = {
                "ASPNETCORE_URLS": "http://+:8080", "Chat__UseAdvancedRag": "false",
                "ConnectionStrings__PortalConnection": "Data Source=/app/data/portal.db;Foreign Keys=True",
                "Jwt__Key": uuid.uuid4().hex + uuid.uuid4().hex,
                "Bootstrap__AdminEmail": "providers@example.test", "Bootstrap__AdminPassword": secret,
                "DEEPSEEK_BASE_URL": "http://host.docker.internal:19091/", "DEEPSEEK_API_KEY": "synthetic-ui-only",
                "DEEPSEEK_MODELS": "deepseek-chat,deepseek-fixture",
                "OPENAI_BASE_URL": "http://host.docker.internal:19091/openai/", "OPENAI_API_KEY": "synthetic-ui-only",
                "OPENAI_ENABLED": "true", "OPENAI_MODELS": "openai-fixture",
                "OLLAMA_BASE_URL": "http://host.docker.internal:19091/", "OLLAMA_ENABLED": "true", "OLLAMA_MODELS": "local-fixture",
                "OLLAMA_ALLOWED_ORIGINS": "http://host.docker.internal:19091", "OLLAMA_LOCAL_ORIGINS": "http://host.docker.internal:19091",
                "OCR_SERVICE_URL": "http://host.docker.internal:19091", "TRINO_URL": "http://127.0.0.1:1"
            }
            arguments = ["create", "--name", "ia-portal-ai-test-api", "--network", network, "--network-alias", "ai-gateway", "--tmpfs", "/app/data", "-p", "127.0.0.1:19089:8080"]
            for key, value in values.items():
                arguments.extend(["-e", f"{key}={value}"])
            docker(*arguments, "ia-portal-ai-final-api"); owned.append("ia-portal-ai-test-api")
            docker("start", "ia-portal-ai-test-api")
            docker("create", "--name", "ia-portal-ai-test-front", "--network", network, "-p", "127.0.0.1:19080:80", "ia-portal-ai-final-front"); owned.append("ia-portal-ai-test-front")
            docker("start", "ia-portal-ai-test-front")
            env["IA_SESSION_CREDENTIALS"] = str(credential_file)
            env["IA_UI_BASE_URL"] = "http://127.0.0.1:19080"
            subprocess.run(["node", "tests/Providers/ui.cjs"], cwd=ROOT, env=env, check=True)
    finally:
        for name in reversed(owned):
            subprocess.run(["docker", "rm", "-f", name], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        if network_created:
            subprocess.run(["docker", "network", "rm", network], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        if helper is not None and helper.poll() is None:
            helper.terminate(); helper.wait(timeout=10)

if __name__ == "__main__":
    main()
