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
import sys
from contextlib import closing

ROOT = Path(__file__).resolve().parents[1]
PROMPTS = []
SERVICE_CALLS = []
TITLE_PROMPTS = []
TRINO_MODE = {"fail": False, "empty": False}

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
        if self.path == "/v1/statement":
            sql = body.decode()
            assert sql.startswith(("SELECT ", "WITH ")), sql
            assert ";" not in sql
            if sql.startswith("SELECT 1"):
                packet = {"columns": [{"name": "connected", "type": "integer"}], "data": [[1]]}
            elif "information_schema.columns" in sql:
                packet = {"columns": [{"name": n, "type": "varchar"} for n in ["table_name", "column_name", "data_type"]], "data": [["invoices", "id", "bigint"], ["payments", "invoice_id", "bigint"]]}
            else:
                assert '"memory"."atlas"."invoices"' in sql
                packet = {"columns": [{"name": "faixa", "type": "varchar"}, {"name": "saldo_centavos", "type": "bigint"}], "data": [["31-60", 1234500]]}
            if TRINO_MODE["fail"]:
                packet = {"error": {"errorName": "UNAVAILABLE", "message": "untrusted upstream body"}}
            elif TRINO_MODE["empty"]:
                packet["data"] = []
            payload = json.dumps(packet).encode()
            content_type = "application/json"
        elif self.path == "/ocr":
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
        atlas_count = 10 if "--atlas" in sys.argv else 0
        real_trino = "--real-trino" in sys.argv
        if real_trino and not atlas_count:
            raise ValueError("--real-trino requires --atlas and the dedicated loopback demo dataset")
        connector_file = Path(temp) / "connectors.json"
        connector_file.write_text(json.dumps({"DataConnectors": {"Sources": [{
            "Id": "atlas_local", "OrganizationSlug": "atlas-demo", "Category": "demo",
            "Endpoint": "http://127.0.0.1:18081/" if real_trino else f"http://127.0.0.1:{services.server_port}/", "Catalog": "memory", "Schema": "atlas",
            "User": "atlas_reader", "AllowedAddresses": ["127.0.0.1"], "Tables": ["invoices", "payments"], "ReadOnlyAccount": True
        }]}}), encoding="utf-8")
        env = dict(os.environ)
        env.update({
            "ASPNETCORE_URLS": base,
            "ConnectionStrings__PortalConnection": f"Data Source={dbfile};Foreign Keys=True",
            "Bootstrap__AdminEmail": "admin@example.test",
            "Bootstrap__AdminPassword": admin_secret,
            "Jwt__Key": uuid.uuid4().hex + uuid.uuid4().hex,
            "Chat__UseAdvancedRag": "false",
            "Demo__Atlas__SeedKnowledge": "true" if atlas_count else "false",
            "TRINO_CONNECTORS_FILE": str(connector_file),
            "DEEPSEEK_BASE_URL": f"http://127.0.0.1:{services.server_port}",
            "DEEPSEEK_API_KEY": "test-only",
            "DEEPSEEK_ENABLED": "true",
            "DEEPSEEK_ALLOWED_ORIGINS": f"http://127.0.0.1:{services.server_port}",
            "AI_DEFAULT_PROVIDER": "deepseek",
            "DEEPSEEK_MODELS": "deepseek-chat,deepseek-fixture",
            "OPENAI_BASE_URL": f"http://127.0.0.1:{services.server_port}/openai/",
            "OPENAI_API_KEY": "test-only",
            "OPENAI_MODELS": "openai-fixture",
            "OPENAI_ENABLED": "true",
            "OPENAI_ALLOWED_ORIGINS": f"http://127.0.0.1:{services.server_port}",
            "OLLAMA_BASE_URL": f"http://127.0.0.1:{services.server_port}/",
            "OLLAMA_ALLOWED_ORIGINS": f"http://127.0.0.1:{services.server_port}",
            "OLLAMA_LOCAL_ORIGINS": f"http://127.0.0.1:{services.server_port}",
            "OLLAMA_MODELS": "local-fixture",
            "OLLAMA_ENABLED": "true",
            "OLLAMA_API_KEY": "",
            "OCR_SERVICE_URL": f"http://127.0.0.1:{services.server_port}",
            "TRINO_URL": "http://127.0.0.1:1",
            "LLM_PRICES_FILE": str(ROOT / "ai-gateway/config/model-prices.json"),
        })
        organization_id = 1
        token = ""
        def request(path, data=None, raw=False, content_type="application/json", method=None):
            body = data if isinstance(data, bytes) else (json.dumps(data).encode() if data is not None else None)
            headers = {"Content-Type": content_type, "X-Organization-Id": str(organization_id)}
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
                assert "Owner" in login["roles"]
                assert len(request("/api/kb/all")) == 1
                if atlas_count:
                    assert len(request("/api/organizations")) == 2
                    organization_id = next(o["id"] for o in request("/api/organizations") if o["slug"] == "atlas-demo")
                    atlas_docs = [d for d in request("/api/kb/all") if d["keyName"].startswith("atlas-")]
                    assert len(atlas_docs) == 10
                    assert all("synthetic-demo" in request(f"/api/kb/{d['id']}")["content"] for d in atlas_docs)
                    atlas_organization = organization_id
                    organization_id = 1
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
                rejected("/api/admin/ai/providers", expected=403)
                rejected("/api/admin/ai/providers/deepseek/test", {}, expected=403)
                rejected("/api/admin/ai/providers/deepseek", {"enabled": True, "endpoint": f"http://127.0.0.1:{services.server_port}/", "models": ["deepseek-chat"], "defaultModel": "deepseek-chat", "version": 0}, method="PUT", expected=403)
                rejected("/api/admin/ai/default", expected=403)
                rejected("/api/admin/ai/default", {"provider": "deepseek", "model": "deepseek-chat", "version": 0}, method="PUT", expected=403)
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
                admin_settings = request("/api/admin/ai/providers")
                assert len(admin_settings) == 3 and "test-only" not in json.dumps(admin_settings)
                deepseek = next(p for p in admin_settings if p["id"] == "deepseek")
                update = {"enabled": True, "endpoint": deepseek["endpoint"], "models": deepseek["models"], "defaultModel": deepseek["defaultModel"], "version": deepseek["version"], "apiKey": "synthetic-must-not-be-stored"}
                rejected("/api/admin/ai/providers/deepseek", {**update, "endpoint": "http://untrusted.invalid/"}, method="PUT", expected=400)
                rejected("/api/admin/ai/providers/deepseek", {**update, "endpoint": "http://user:secret@127.0.0.1/"}, method="PUT", expected=400)
                saved = request("/api/admin/ai/providers/deepseek", update, method="PUT")
                assert saved["version"] == update["version"] + 1 and "synthetic-must-not-be-stored" not in json.dumps(saved)
                rejected("/api/admin/ai/providers/deepseek", update, method="PUT", expected=409)
                global_default = request("/api/admin/ai/default")
                request("/api/admin/ai/default", {"provider": "deepseek", "model": "deepseek-fixture", "version": global_default["version"]}, method="PUT")
                rejected("/api/admin/ai/default", {"provider": "deepseek", "model": "deepseek-chat", "version": global_default["version"]}, method="PUT", expected=409)
                inferred = len(PROMPTS) + len(TITLE_PROMPTS)
                accounted = request("/api/usage/summary")["requests"]
                for provider in ["deepseek", "openai", "ollama"]:
                    tested = request(f"/api/admin/ai/providers/{provider}/test", {})
                    assert tested["success"] and tested["status"] == "connected"
                assert inferred == len(PROMPTS) + len(TITLE_PROMPTS) and accounted == request("/api/usage/summary")["requests"], "Connectivity test invoked paid inference"
                request("/api/admin/ai/providers/deepseek", {**update, "version": saved["version"], "enabled": False}, method="PUT")
                rejected("/api/chatBot/ask", {"question": "No silent fallback", "sessionId": selected["id"]}, expected=409)
                with closing(sqlite3.connect(dbfile)) as db:
                    stored = db.execute("SELECT Endpoint, ModelsJson, DefaultModel FROM AiProviderSettings").fetchall()
                    assert "synthetic-must-not-be-stored" not in json.dumps(stored)
                    assert not {"ApiKey", "Secret", "Password"}.intersection(r[1] for r in db.execute("PRAGMA table_info(AiProviderSettings)"))
                stop()
                env["OPENAI_API_KEY"] = ""
                start(log)
                assert request("/api/admin/ai/default")["model"] == "deepseek-fixture"
                assert request("/api/admin/ai/providers/openai/test", {})["status"] == "not_configured"
                assert "openai" not in {p["id"] for p in request("/api/ai/providers")}
                print("PASS: administrative settings/default CAS, server endpoint allowlist, environment-only secrets, metadata-only connectivity, disabled provider rejection and settings restart persistence.")
                if atlas_count:
                    # Earlier provider tests deliberately disabled DeepSeek and selected Ollama.
                    current_provider = next(p for p in request("/api/admin/ai/providers") if p["id"] == "deepseek")
                    request("/api/admin/ai/providers/deepseek", {**update, "enabled": True, "version": current_provider["version"]}, method="PUT")
                    request("/api/me/ai-preference", {"provider": "deepseek", "model": "deepseek-fixture"}, method="PUT")
                    default_kb = request("/api/kb/all")[0]
                    default_role = request("/api/roles")[0]
                    default_session = request("/api/chatBot/sessions", {})["id"]
                    organization_id = atlas_organization
                    assert request("/api/auth/me")["organizationId"] == atlas_organization
                    source_profiles = request("/api/data-connectors/profiles")
                    assert len(source_profiles) == 1 and "password" not in json.dumps(source_profiles).lower()
                    atlas_group = next(g for g in request("/api/data-connectors/groups") if g["name"] == "admin")
                    connection = {"name": "Atlas HTTP fixture", "profileId": "atlas_local", "enabled": True, "tables": ["invoices", "payments"], "groupIds": [atlas_group["id"]], "version": 0}
                    connector = request("/api/data-connectors", connection)["id"]
                    assert request(f"/api/data-connectors/{connector}/test", {})["status"] == "connected"
                    discovered = request(f"/api/data-connectors/{connector}/metadata")
                    assert len(discovered) >= 10 if real_trino else len(discovered) == 2
                    assert request("/api/data/sources")[0]["id"] == connector
                    analysis = request(f"/api/data/{connector}/analytics?kind=aging&reference=2026-11-01")
                    expected_cents = 15605108 if real_trino else 1234500
                    assert analysis["rows"][0]["saldo_centavos"] == expected_cents and analysis["synthetic"]
                    rejected(f"/api/data/{connector}/analytics?kind=delete&reference=2026-11-01", expected=400)
                    rejected("/api/data-connectors", {**connection, "groupIds": [default_role["id"]]}, expected=400)
                    rejected("/api/data-connectors", {**connection, "tables": ["secret_table"]}, expected=400)
                    rejected("/api/data-connectors", {**connection, "profileId": "arbitrary_destination"}, expected=409)
                    request(f"/api/data-connectors/{connector}", {**connection, "enabled": False, "version": 1}, method="PUT")
                    rejected(f"/api/data/{connector}/analytics?kind=aging&reference=2026-11-01")
                    rejected(f"/api/data-connectors/{connector}", {**connection, "version": 1}, method="PUT", expected=409)
                    request(f"/api/data-connectors/{connector}", {**connection, "version": 2}, method="PUT")
                    assert len(request("/api/kb/all")) == 10
                    rejected(f"/api/kb/{default_kb['id']}")
                    rejected("/api/kb/save", {"id": default_kb["id"], "keyName": "intrusion", "section": "test", "content": "intrusion"})
                    rejected("/api/kb/upsert", {"id": default_kb["id"], "keyName": "intrusion", "section": "test", "content": "intrusion", "isActive": True})
                    rejected(f"/api/roles/{default_role['id']}", method="DELETE")
                    rejected(f"/api/chatBot/sessions/{default_session}")
                    rejected("/api/admin/ai/providers", expected=403)
                    atlas_session = request("/api/chatBot/sessions", {})["id"]
                    stream = request("/api/ia/ask/stream", {"pergunta": "Explique a política de cobrança e negociação da Atlas", "sessionId": atlas_session}, raw=True)
                    assert "event: error" not in stream and "synthetic-demo" in PROMPTS[-1]
                    assert "TEST-MARKER" not in PROMPTS[-1]
                    hybrid = {"pergunta": "Mostre o valor vencido e explique quais ações a política de cobrança recomenda.", "sessionId": atlas_session, "sourceId": connector, "referenceDate": "2026-11-01", "requestId": str(uuid.uuid4())}
                    stream = request("/api/ia/ask/stream", hybrid, raw=True)
                    assert "event: error" not in stream and str(expected_cents) in stream and "atlas-chart" in stream
                    assert "synthetic-demo" in PROMPTS[-1] and str(expected_cents) in PROMPTS[-1] and "TEST-MARKER" not in PROMPTS[-1]
                    persisted = request(f"/api/chatBot/sessions/{atlas_session}/messages")["items"][-1]["content"]
                    assert "atlas-collections-policy" in persisted and "2026-11-01" in persisted and "Plano de consulta" in persisted
                    assert any(str(expected_cents) in m["content"] for m in request(f"/api/chatBot/history/{atlas_session}")), "Legacy display history lost structured result"
                    rejected("/api/ia/ask/stream", {**hybrid, "referenceDate": "2026-10-01"}, expected=409)
                    request("/api/ia/ask/stream", {"pergunta": "Explique a política de cobrança da Atlas", "sessionId": atlas_session}, raw=True)
                    assert str(expected_cents) not in PROMPTS[-1] and "portal:structured-data" not in PROMPTS[-1], "Historical SQL data retransmitted without reauthorization"
                    inferred = len(PROMPTS) + len(TITLE_PROMPTS)
                    local_session = request("/api/chatBot/sessions", {})["id"]
                    no_source = request("/api/ia/ask/stream", {"pergunta": "Mostre o aging", "sessionId": local_session}, raw=True)
                    assert "Selecione uma fonte" in no_source and len(PROMPTS) + len(TITLE_PROMPTS) == inferred
                    assert request(f"/api/chatBot/sessions/{local_session}")["titleSource"] == "fallback"
                    assert request(f"/api/usage/summary?sessionId={local_session}")["requests"] == 0
                    plain_local = request("/api/chatBot/ask", {"question": "Mostre o aging", "sessionId": local_session})
                    assert "não calcula indicadores" in plain_local["texto"] and len(PROMPTS) + len(TITLE_PROMPTS) == inferred
                    for legacy_route in ["/api/sql/ask", "/api/sql/ask-rag"]:
                        rejected(legacy_route, {"question": "Mostre o aging", "sessionId": local_session}, expected=409)
                    assert len(PROMPTS) + len(TITLE_PROMPTS) == inferred
                    if not real_trino:
                        TRINO_MODE["fail"] = True
                        failed_data = request("/api/ia/ask/stream", {**hybrid, "requestId": str(uuid.uuid4())}, raw=True)
                        TRINO_MODE["fail"] = False
                        assert "Nenhum indicador" in failed_data and "1234500" not in failed_data and len(PROMPTS) + len(TITLE_PROMPTS) == inferred
                        TRINO_MODE["empty"] = True
                        empty_data = request("/api/ia/ask/stream", {**hybrid, "requestId": str(uuid.uuid4())}, raw=True)
                        TRINO_MODE["empty"] = False
                        assert "não retornou registros" in empty_data and "1234500" not in empty_data and len(PROMPTS) + len(TITLE_PROMPTS) == inferred
                    # Shared identity cannot be reset by an admin of only this tenant.
                    account = request("/api/users/1")
                    rejected("/api/users/1", {"userName": account["userName"], "userEmail": account["userEmail"], "password": uuid.uuid4().hex,
                        "isActive": True, "groupIds": [g["id"] for g in account["groups"]]}, method="PUT", expected=409)
                    organization_id = 1
                    rejected(f"/api/chatBot/sessions/{atlas_session}")
                    rejected(f"/api/data-connectors/{connector}/metadata")
                    rejected(f"/api/data/{connector}/analytics?kind=aging&reference=2026-11-01")
                    assert all(not d["keyName"].startswith("atlas-") for d in request("/api/kb/all"))
                    # Default-only user cannot select Atlas, even with a valid JWT.
                    token = request("/api/auth/login", {"email": "b@example.test", "password": admin_secret})["token"]
                    organization_id = atlas_organization
                    rejected("/api/kb/all", expected=401)
                    organization_id = 1
                    token = admin_token
                    print("PASS: organization membership validation, isolated KB/read/write/RAG/roles/conversations, shared-account reset denied, installation settings protected.")
                    print("PASS: Data Connectors registration/profile/group/table policies, connectivity/discovery, typed read-only analytics, disabled sources, CAS and cross-organization denial (" + ("real local Trino" if real_trino else "Trino HTTP double") + " through pinned socket handler).")
                    print("PASS: hybrid RAG+SQL/SSE grounding, exact persisted table/chart/provenance/document version, source/reference replay protection; missing data does not call LLM. Failure/empty fixtures " + ("skipped in real Trino run; covered by double run." if real_trino else "passed without invented metrics."))
                print("PASS: SQLite creation, bootstrap/login, KB CRUD, chat streaming/history, ratings, OCR routing and restart persistence.")
                print("LLM and OCR were HTTP doubles; external inference and real text extraction were not exercised.")
        finally:
            stop()
            services.shutdown()
            services.server_close()

if __name__ == "__main__":
    main()
