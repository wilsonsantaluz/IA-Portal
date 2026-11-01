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
SERVICE_CALLS = []
TITLE_PROMPTS = []

class FakeServices(BaseHTTPRequestHandler):
    def log_message(self, *_):
        pass

    def do_GET(self):
        SERVICE_CALLS.append(self.path)
        payload = json.dumps({"models": []} if self.path == "/api/tags" else {"data": []}).encode()
        self.send_response(200)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(payload)))
        self.end_headers()
        self.wfile.write(payload)

    def do_POST(self):
        SERVICE_CALLS.append(self.path)
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
        elif self.path == "/api/generate":
            data = json.loads(body)
            last = {"model": data["model"], "done": True, "response": "Titulo local" if data.get("system") else "Resposta local.", "prompt_eval_count": 11, "eval_count": 9}
            payload = (json.dumps(last) + ("\n" if data.get("stream") else "")).encode()
            content_type = "application/x-ndjson" if data.get("stream") else "application/json"
        elif self.path in ["/chat/completions", "/openai/chat/completions"]:
            data = json.loads(body)
            if data.get("max_tokens") == 64 or data.get("max_completion_tokens") == 64:
                assert not data.get("stream") and len(data["messages"]) == 2
                TITLE_PROMPTS.append(data["messages"][-1]["content"])
                if "__title_failure__" in TITLE_PROMPTS[-1]:
                    self.send_error(503, "synthetic-title-secret-must-not-leak")
                    return
                payload = json.dumps({"model": data["model"], "choices": [{"message": {"content": "Organizacao de recebiveis"}}], "usage": {"prompt_tokens": 35, "completion_tokens": 6, "total_tokens": 41, "prompt_cache_hit_tokens": 20}}).encode()
                self.send_response(200)
                self.send_header("Content-Type", "application/json")
                self.send_header("Content-Length", str(len(payload)))
                self.end_headers()
                self.wfile.write(payload)
                return
            PROMPTS.append(data["messages"][-1]["content"])
            if "__provider_failure__" in PROMPTS[-1]:
                self.send_error(503, "synthetic-provider-detail-must-not-leak")
                return
            if data.get("stream"):
                chunks = ["Resposta ", "de teste."]
                payload = b"".join(
                    ("data: " + json.dumps({"choices": [{"delta": {"content": chunk}}]}) + "\n\n").encode()
                    for chunk in chunks
                ) + ("data: " + json.dumps({"model": data["model"], "choices": [], "usage": {"prompt_tokens": 17, "completion_tokens": 5, "total_tokens": 22, "prompt_cache_hit_tokens": 7}}) + "\n\ndata: [DONE]\n\n").encode()
                content_type = "text/event-stream"
            else:
                payload = json.dumps({"model": data["model"], "choices": [{"message": {"content": "Analise sintetica do documento."}}], "usage": {"prompt_tokens": 23, "completion_tokens": 6, "total_tokens": 29, "prompt_cache_hit_tokens": 5}}).encode()
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
            "DEEPSEEK_MODELS": "deepseek-chat,deepseek-fixture",
            "OPENAI_BASE_URL": f"http://127.0.0.1:{services.server_port}/openai/",
            "OPENAI_API_KEY": "test-only",
            "OPENAI_MODELS": "openai-fixture",
            "OPENAI_ENABLED": "true",
            "OLLAMA_BASE_URL": f"http://127.0.0.1:{services.server_port}/",
            "OLLAMA_ALLOWED_ORIGINS": f"http://127.0.0.1:{services.server_port}",
            "OLLAMA_LOCAL_ORIGINS": f"http://127.0.0.1:{services.server_port}",
            "OLLAMA_MODELS": "local-fixture",
            "OLLAMA_ENABLED": "true",
            "OCR_SERVICE_URL": f"http://127.0.0.1:{services.server_port}",
            "TRINO_URL": "http://127.0.0.1:1",
            "LLM_PRICES_FILE": str(ROOT / "ai-gateway/config/model-prices.json"),
        })
        token = ""
        def request(path, data=None, raw=False, content_type="application/json", method=None):
            body = data if isinstance(data, bytes) else (json.dumps(data).encode() if data is not None else None)
            headers = {"Content-Type": content_type}
            if token:
                headers["Authorization"] = f"Bearer {token}"
            req = urllib.request.Request(base + path, data=body, headers=headers, method=method)
            with urllib.request.urlopen(req, timeout=20) as response:
                text = response.read().decode()
                return text if raw else (json.loads(text) if text else None)

        def start(log):
            nonlocal process
            process = subprocess.Popen(["dotnet", str(dll)], cwd=ROOT / "ai-gateway",
                                       env=env, stdout=log, stderr=log,
                                       creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
            for _ in range(100):
                if process.poll() is not None:
                    raise RuntimeError("Gateway startup failed; inspect build/configuration.")
                try:
                    request("/swagger/v1/swagger.json")
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
                session = request("/api/chatBot/sessions", {})["id"]
                stream = request("/api/ia/ask/stream", {"pergunta": "Explique recebiveis", "sessionId": session}, raw=True)
                assert "event: error" not in stream, stream
                assert "[*ENDSTREAN*]" in stream
                assert "TEST-MARKER" in PROMPTS[-1]
                title = request(f"/api/chatBot/sessions/{session}")
                assert title["title"] == "Organizacao de recebiveis" and title["titleSource"] == "automatic"
                assert TITLE_PROMPTS == ["Explique recebiveis"], "Title received full RAG/history"
                with closing(sqlite3.connect(dbfile)) as db:
                    assert db.execute("SELECT Purpose, Source, InputTokens, OutputTokens, TotalTokens, CacheTokens FROM LlmUsage WHERE Purpose='title'").fetchone() == ("title", "reported", 35, 6, 41, 20)
                measured = request(f"/api/usage/summary?sessionId={session}")
                assert measured["requests"] == 2 and measured["totalTokens"] == 63 and measured["cacheTokens"] == 27 and measured["tokensComplete"]
                assert measured["estimatedCostUsd"] is None and not measured["costComplete"]
                history = request(f"/api/chatBot/history/{session}")
                assert len(history) == 2 and history[-1]["content"] == "Resposta de teste."
                request("/api/ia/ask/stream", {"pergunta": "Continue", "sessionId": session}, raw=True)
                assert "Resposta de teste." in PROMPTS[-1]
                assert len(TITLE_PROMPTS) == 1, "Title regenerated on follow-up"
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
                # Session operations use real JWT ownership before touching any external service.
                def rejected(path, data=None, method=None, expected=404, content_type="application/json"):
                    calls = len(SERVICE_CALLS)
                    try:
                        request(path, data, method=method, content_type=content_type)
                        raise AssertionError("Forbidden request accepted: " + path)
                    except urllib.error.HTTPError as err:
                        assert err.code == expected, (path, err.code)
                    assert len(SERVICE_CALLS) == calls, "Forbidden request reached LLM/OCR"
                admin_token = token
                group = request("/api/roles", {"name": "Chat test", "permissions": ["chat.access"]})
                request("/api/users", {"userName": "Synthetic B", "userEmail": "b@example.test", "password": admin_secret, "groupIds": [group["id"]]})
                token = request("/api/auth/login", {"email": "b@example.test", "password": admin_secret})["token"]
                empty_usage = request("/api/usage/summary")
                assert empty_usage["requests"] == 0 and empty_usage["totalTokens"] is None
                assert request("/api/usage/calls")["items"] == []
                rejected(f"/api/usage/summary?sessionId={session}")
                rejected(f"/api/usage/calls?sessionId={session}")
                rejected("/api/usage/admin/summary", expected=403)
                rejected("/api/usage/admin/groups", expected=403)
                rejected("/api/usage/summary?userId=1", expected=400)
                rejected("/api/usage/groups?by=user", expected=400)
                assert request("/api/chatBot/sessions")["items"] == []
                rejected(f"/api/chatBot/sessions/{session}")
                rejected(f"/api/chatBot/sessions/{session}/messages")
                rejected(f"/api/chatBot/history/{session}")
                rejected(f"/api/chatBot/sessions/{session}", {"title": "Intrusão", "version": 0}, method="PATCH")
                rejected(f"/api/chatBot/sessions/{session}", method="DELETE")
                rejected(f"/api/chatBot/sessions/{session}/model", {"provider": "openai", "model": "openai-fixture", "version": 0}, method="PATCH")
                rejected("/api/ia/ask/stream", {"pergunta": "Intrusão", "sessionId": session})
                rejected("/api/chatBot/ask", {"question": "Intrusão", "sessionId": session})
                rejected("/api/sql/ask", {"question": "Intrusão", "sessionId": session})
                rejected("/api/sql/ask-rag", {"question": "Intrusão", "sessionId": session})
                rejected("/api/ocr/analisar", body, content_type=f"multipart/form-data; boundary={boundary}")
                own_b = request("/api/chatBot/sessions", {"ownerUserId": 1, "userId": "1", "id": session})
                assert own_b["id"] != session
                token = admin_token
                aggregate = request("/api/usage/admin/summary")
                assert aggregate["requests"] == 4 and aggregate["totalTokens"] == 114
                for by in ["day", "month", "model", "provider", "purpose", "user"]:
                    groups = request("/api/usage/admin/groups?by=" + by)
                    assert sum(item["totals"]["requests"] for item in groups["items"]) == 4
                first_calls = request("/api/usage/calls?limit=1")
                next_calls = request("/api/usage/calls?limit=1&page=2")
                assert first_calls["nextPage"] == 2 and first_calls["items"][0]["id"] != next_calls["items"][0]["id"]
                assert not {"content", "prompt", "userEmail", "apiKey"}.intersection(first_calls["items"][0])
                rejected("/api/usage/summary?from=2020-01-01T00:00:00Z&to=2026-01-01T00:00:00Z", expected=400)
                rejected(f"/api/chatBot/sessions/{own_b['id']}")
                rejected("/api/ia/ask/stream", {"pergunta": "Desconhecida", "sessionId": str(uuid.uuid4())})
                request_id = str(uuid.uuid4())
                payload = {"pergunta": "Envio idempotente", "sessionId": session, "requestId": request_id}
                request("/api/ia/ask/stream", payload, raw=True)
                calls = len(PROMPTS)
                replay = request("/api/ia/ask/stream", payload, raw=True)
                assert "Resposta de teste." in replay and len(PROMPTS) == calls
                rejected("/api/ia/ask/stream", {**payload, "pergunta": "Outro texto"}, expected=409)
                assert len(request(f"/api/chatBot/history/{session}")) == 8
                failed = request("/api/ia/ask/stream", {"pergunta": "__provider_failure__", "sessionId": session}, raw=True)
                assert "event: error" in failed and "[*ENDSTREAN*]" not in failed
                assert "synthetic-provider-detail-must-not-leak" not in failed
                assert request(f"/api/chatBot/sessions/{session}/messages")["items"][-1]["status"] == "failed"
                page = request(f"/api/chatBot/sessions/{session}/messages?limit=1")
                previous = request(f"/api/chatBot/sessions/{session}/messages?limit=1&before={page['nextCursor']}")
                assert previous["items"][0]["id"] < page["items"][0]["id"]
                rejected(f"/api/chatBot/sessions/{session}/messages?before=bad", expected=400)
                metadata = request(f"/api/chatBot/sessions/{session}")
                renamed = request(f"/api/chatBot/sessions/{session}", {"title": "Conversa de teste", "version": metadata["version"]}, method="PATCH")
                assert renamed["titleSource"] == "manual"
                rejected(f"/api/chatBot/sessions/{session}", {"title": "Versão obsoleta", "version": metadata["version"]}, method="PATCH", expected=409)
                assert request("/api/chatBot/sessions?search=Conversa")["items"][0]["id"] == session
                request(f"/api/chatBot/sessions/{session}", method="DELETE")
                rejected(f"/api/chatBot/sessions/{session}")
                rejected("/api/ia/ask/stream", payload)
                with closing(sqlite3.connect(dbfile)) as db:
                    assert db.execute("SELECT COUNT(*) FROM ChatMessage WHERE SessionId=?", (session.upper(),)).fetchone()[0] == 9
                print("PASS: session CRUD/search/pagination, cross-user isolation including OCR/SQL before external calls, logical deletion, and replay without duplicate LLM calls.")
                fallback_id = request("/api/chatBot/sessions", {})["id"]
                fallback_stream = request("/api/ia/ask/stream", {"pergunta": "__title_failure__ organizar dados", "sessionId": fallback_id}, raw=True)
                assert "event: done" in fallback_stream and "event: error" not in fallback_stream and "synthetic-title-secret" not in fallback_stream
                fallback = request(f"/api/chatBot/sessions/{fallback_id}")
                assert fallback["titleSource"] == "fallback" and fallback["title"] == "__title_failure__ organizar dados"
                stop()
                start(log)
                assert request(f"/api/chatBot/sessions/{fallback_id}")["title"] == fallback["title"]
                calls = len(TITLE_PROMPTS)
                request("/api/chatBot/ask", {"question": "Continuar conversa", "sessionId": fallback_id})
                assert len(TITLE_PROMPTS) == calls, "Restart retried a paid title attempt"
                with closing(sqlite3.connect(dbfile)) as db:
                    assert db.execute("SELECT Status, TotalTokens FROM LlmUsage WHERE SessionId=? AND Purpose='title'", (fallback_id.upper(),)).fetchone() == ("failed", None)
                final_usage = request("/api/usage/summary")
                assert final_usage["errors"] >= 2 and not final_usage["tokensComplete"] and final_usage["totalTokens"] > 114
                print("PASS: real HTTP usage summary/groups/call pagination, A/B and administrative authorization, measured totals/cache, unknown costs, failures and restart persistence.")
                print("PASS: automatic title via real SSE/API, reported usage persisted separately, failure fallback preserves chat success and restart never repeats title charges.")
                providers = request("/api/ai/providers")
                assert {p["id"] for p in providers} == {"deepseek", "openai", "ollama"}
                assert all("endpoint" not in p and "apiKey" not in p for p in providers)
                for provider, model in [("openai", "openai-fixture"), ("ollama", "local-fixture")]:
                    request("/api/me/ai-preference", {"provider": provider, "model": model}, method="PUT")
                    selected = request("/api/chatBot/sessions", {})
                    assert selected["provider"] == provider and selected["model"] == model
                    reply = request("/api/ia/ask/stream", {"pergunta": "Assunto sintetico", "sessionId": selected["id"]}, raw=True)
                    assert "event: done" in reply and "event: error" not in reply
                    answer = request(f"/api/chatBot/sessions/{selected['id']}/messages")["items"][-1]
                    assert answer["provider"] == provider and answer["model"] == model
                    current = request(f"/api/chatBot/sessions/{selected['id']}")
                    changed = request(f"/api/chatBot/sessions/{selected['id']}/model", {"provider": "deepseek", "model": "deepseek-fixture", "version": current["version"]}, method="PATCH")
                    assert changed["provider"] == "deepseek" and len(request(f"/api/chatBot/sessions/{selected['id']}/messages")["items"]) == 2
                    if provider == "ollama":
                        local = request(f"/api/usage/summary?sessionId={selected['id']}")
                        assert local["totalTokens"] == 40 and local["localApiRequests"] == 2 and local["estimatedCostUsd"] == 0
                stop()
                start(log)
                assert request("/api/me/ai-preference")["provider"] == "ollama"
                print("PASS: provider list/preferences, OpenAI/Ollama chat+title via mocks, explicit versioned switch preserving history, measured local tokens/API billing and preference restart persistence.")
                print("PASS: SQLite creation, bootstrap/login, KB CRUD, chat streaming/history, ratings, OCR routing and restart persistence.")
                print("LLM and OCR were HTTP doubles; external inference and real text extraction were not exercised.")
        finally:
            stop()
            services.shutdown()
            services.server_close()

if __name__ == "__main__":
    main()
