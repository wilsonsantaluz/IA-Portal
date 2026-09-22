"""Integration checks: real gateway + temporary SQLite; LLM/OCR HTTP doubles.

Run after: dotnet build ai-gateway/AiGateway.csproj
No paid services, real documents or production databases are accessed.
"""
from pathlib import Path
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import os
import socket
import sqlite3
import subprocess
import tempfile
import threading
import time
import urllib.request
import urllib.error
import uuid
from contextlib import closing

ROOT = Path(__file__).resolve().parents[1]
PROMPTS = []

class FakeServices(BaseHTTPRequestHandler):
    def log_message(self, *_):
        pass

    def do_POST(self):
        if self.headers.get("Transfer-Encoding", "").lower() == "chunked":
            chunks = []
            while True:
                size = int(self.rfile.readline().strip().split(b";", 1)[0], 16)
                if size == 0:
                    while self.rfile.readline().strip():
                        pass
                    break
                chunks.append(self.rfile.read(size))
                self.rfile.read(2)
            body = b"".join(chunks)
        else:
            body = self.rfile.read(int(self.headers.get("Content-Length", 0)))
        if self.path == "/ocr":
            assert b"fixture.pdf" in body
            payload = json.dumps({"texto": "Documento sintetico: recebivel de 125 reais."}).encode()
            content_type = "application/json"
        elif self.path == "/chat/completions":
            data = json.loads(body)
            PROMPTS.append(data["messages"][-1]["content"])
            if data.get("stream"):
                chunks = ["Resposta ", "de teste."]
                payload = b"".join(
                    ("data: " + json.dumps({"choices": [{"delta": {"content": chunk}}]}) + "\n\n").encode()
                    for chunk in chunks
                ) + b"data: [DONE]\n\n"
                content_type = "text/event-stream"
            else:
                payload = json.dumps({"choices": [{"message": {"content": "Analise sintetica do documento."}}]}).encode()
                content_type = "application/json"
        else:
            self.send_error(404)
            return
        self.send_response(200)
        self.send_header("Content-Type", content_type)
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

def main():
    dll = ROOT / "ai-gateway/bin/Debug/net8.0/AiGateway.dll"
    if not dll.exists():
        raise SystemExit("Build the gateway first.")
    services = ThreadingHTTPServer(("127.0.0.1", 0), FakeServices)
    threading.Thread(target=services.serve_forever, daemon=True).start()
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        port = probe.getsockname()[1]
    base = f"http://127.0.0.1:{port}"
    process = None
    # Temporary data is isolated from any real installation.
    with tempfile.TemporaryDirectory(prefix="portal-smoke-") as temp:
        dbfile = Path(temp) / "portal.db"
        admin_secret = uuid.uuid4().hex
        env = dict(os.environ)
        env.update({
            "ASPNETCORE_URLS": base,
            "ConnectionStrings__PortalConnection": f"Data Source={dbfile};Foreign Keys=True",
            "Bootstrap__AdminEmail": "admin@example.test",
            "Bootstrap__AdminPassword": admin_secret,
            "Jwt__Key": uuid.uuid4().hex + uuid.uuid4().hex,
            "Chat__UseAdvancedRag": "false",
            "DEEPSEEK_BASE_URL": f"http://127.0.0.1:{services.server_port}",
            "DEEPSEEK_API_KEY": "test-only",
            "OCR_SERVICE_URL": f"http://127.0.0.1:{services.server_port}",
        })
        token = ""
        def request(path, data=None, raw=False, content_type="application/json"):
            body = data if isinstance(data, bytes) else (json.dumps(data).encode() if data is not None else None)
            headers = {"Content-Type": content_type}
            if token:
                headers["Authorization"] = f"Bearer {token}"
            req = urllib.request.Request(base + path, data=body, headers=headers)
            with urllib.request.urlopen(req, timeout=20) as response:
                text = response.read().decode()
                return text if raw else (json.loads(text) if text else None)

        def start(log):
            nonlocal process
            process = subprocess.Popen(["dotnet", str(dll)], cwd=ROOT / "ai-gateway",
                                       env=env, stdout=log, stderr=log)
            for _ in range(100):
                if process.poll() is not None:
                    raise RuntimeError("Gateway startup failed; inspect build/configuration.")
                try:
                    request("/api/chatkb/metrics/summary")
                    return
                except (OSError, urllib.error.URLError):
                    time.sleep(.2)
            raise RuntimeError("Gateway startup timeout")

        def stop():
            if process is not None and process.poll() is None:
                process.terminate()
                process.wait(timeout=15)

        try:
            with open(Path(temp) / "gateway.log", "w", encoding="utf-8") as log:
                start(log)
                assert "/api/ocr/analisar" in request("/swagger/v1/swagger.json")["paths"]
                login = request("/api/auth/login", {"email": "admin@example.test", "password": admin_secret})
                token = login["token"]
                assert "admin" in login["roles"]
                assert len(request("/api/kb/all")) == 1
                item = request("/api/kb/upsert", {
                    "keyName": "test_receivables", "section": "Auditoria", "title": "Recebiveis teste",
                    "content": "Protocolo TEST-MARKER para conciliacao de recebiveis.", "isActive": True
                })
                assert request(f"/api/kb/{item['id']}")["content"].startswith("Protocolo")
                session = str(uuid.uuid4())
                stream = request("/api/ia/ask/stream", {"pergunta": "Explique recebiveis", "sessionId": session}, raw=True)
                assert "event: error" not in stream, stream
                assert "[*ENDSTREAN*]" in stream
                assert "TEST-MARKER" in PROMPTS[-1]
                history = request(f"/api/chatBot/history/{session}")
                assert len(history) == 2 and history[-1]["content"] == "Resposta de teste."
                request("/api/ia/ask/stream", {"pergunta": "Continue", "sessionId": session}, raw=True)
                assert "Resposta de teste." in PROMPTS[-1]
                request("/api/chatkb/rate", {"userId": session, "question": "Teste", "rating": 4})
                assert request("/api/chatkb/metrics/summary")["totalRatings"] == 1
                assert request("/api/chatkb/metrics/by-month")[0]["count"] == 1
                boundary = "boundary" + uuid.uuid4().hex
                body = (f"--{boundary}\r\nContent-Disposition: form-data; name=\"sessionId\"\r\n\r\n{session}\r\n"
                        f"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"fixture.pdf\"\r\n"
                        f"Content-Type: application/pdf\r\n\r\n%PDF-test-fixture\r\n--{boundary}--\r\n").encode()
                ocr = request("/api/ocr/analisar", body, content_type=f"multipart/form-data; boundary={boundary}")
                assert "125" in ocr["texto"] and "Analise" in ocr["analise"]
                assert len(request(f"/api/chatBot/history/{session}")) == 6
                try:
                    request("/api/ia/ask/stream", {"pergunta": "Teste", "sessionId": "invalid"})
                    raise AssertionError("Invalid session accepted")
                except urllib.error.HTTPError as error:
                    assert error.code == 400
                stop()
                env["Bootstrap__AdminPassword"] = uuid.uuid4().hex
                start(log)
                request("/api/auth/login", {"email": "admin@example.test", "password": admin_secret})
                assert len(request(f"/api/chatBot/history/{session}")) == 6
                assert len(request("/api/kb/all")) == 2
                with closing(sqlite3.connect(dbfile)) as db:
                    assert db.execute("PRAGMA integrity_check").fetchone()[0] == "ok"
                    assert db.execute("SELECT COUNT(*) FROM Users").fetchone()[0] == 1
                print("PASS: SQLite creation, bootstrap/login, KB CRUD, chat streaming/history, ratings, OCR routing and restart persistence.")
                print("LLM and OCR were HTTP doubles; external inference and real text extraction were not exercised.")
        finally:
            stop()
            services.shutdown()
            services.server_close()

if __name__ == "__main__":
    main()
