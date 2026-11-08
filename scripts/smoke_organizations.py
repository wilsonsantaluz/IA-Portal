"""Real HTTP/JWT/SQLite workspace isolation smoke. Temporary data; no paid calls."""
import json
import os
from pathlib import Path
import socket
import sqlite3
import subprocess
import tempfile
import threading
import time
import urllib.error
import urllib.request
import uuid
from contextlib import closing
from http.server import ThreadingHTTPServer
from smoke_local import FakeServices, PROMPTS

ROOT = Path(__file__).resolve().parents[1]

def main():
    services = ThreadingHTTPServer(("127.0.0.1", 0), FakeServices)
    threading.Thread(target=services.serve_forever, daemon=True).start()
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        port = probe.getsockname()[1]
    base = f"http://127.0.0.1:{port}"
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    with tempfile.TemporaryDirectory(prefix="portal-workspace-http-") as folder:
        database = Path(folder) / "portal.db"
        password = uuid.uuid4().hex
        env = dict(os.environ)
        env.update({
            "ASPNETCORE_URLS": base, "ASPNETCORE_ENVIRONMENT": "Production",
            "ConnectionStrings__PortalConnection": f"Data Source={database};Foreign Keys=True",
            "SQLITE_BACKUP_DIRECTORY": str(Path(folder)/"backups"),
            "Bootstrap__AdminEmail": "owner@example.test", "Bootstrap__AdminPassword": password,
            "Jwt__Key": uuid.uuid4().hex + uuid.uuid4().hex, "Chat__UseAdvancedRag": "false",
            "Demo__Atlas__SeedKnowledge": "false", "TRINO_CONNECTORS_FILE": "",
            "DEEPSEEK_API_KEY": "test-only", "DEEPSEEK_ENABLED": "true",
            "DEEPSEEK_BASE_URL": f"http://127.0.0.1:{services.server_port}/",
            "DEEPSEEK_ALLOWED_ORIGINS": f"http://127.0.0.1:{services.server_port}",
            "DEEPSEEK_MODELS": "deepseek-chat,fixture", "DEEPSEEK_DEFAULT_MODEL": "deepseek-chat",
            "OPENAI_ENABLED": "false", "OLLAMA_ENABLED": "false", "AI_DEFAULT_PROVIDER": "deepseek",
        })
        token = ""
        organization = 1
        def request(path, data=None, method=None, expected=200, raw=False, selected=None):
            headers = {"Content-Type": "application/json", "X-Organization-Id": str(organization if selected is None else selected)}
            if token: headers["Authorization"] = "Bearer " + token
            req = urllib.request.Request(base+path, headers=headers, method=method,
                                         data=json.dumps(data).encode() if data is not None else None)
            try:
                with opener.open(req, timeout=30) as response:
                    text = response.read().decode()
                    assert response.status == expected, (path, response.status)
                    return text if raw else json.loads(text) if text else None
            except urllib.error.HTTPError as error:
                assert error.code == expected, (path, error.code, error.read().decode()[:300])
                return None
        log_path = Path(folder)/"gateway.log"
        with log_path.open("w", encoding="utf8") as log:
            process = subprocess.Popen(["dotnet", str(ROOT/"ai-gateway/bin/Debug/net8.0/AiGateway.dll")],
                                       cwd=ROOT/"ai-gateway", env=env, stdout=log, stderr=log,
                                       creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
            try:
                for _ in range(100):
                    if process.poll() is not None: raise RuntimeError("Gateway failed: " + log_path.read_text(encoding="utf8")[-1800:])
                    try: request("/swagger/v1/swagger.json"); break
                    except OSError: time.sleep(.2)
                else: raise RuntimeError("Gateway startup timeout")
                owner = request("/api/auth/login", {"email":"owner@example.test", "password":password})
                token = owner["token"]
                assert "Owner" in owner["roles"]
                a = request("/api/organizations", {"name":"Workspace A", "slug":"workspace-a"}, expected=201)["id"]
                b = request("/api/organizations", {"name":"Workspace B", "slug":"workspace-b"}, expected=201)["id"]
                request("/api/organizations", {"name":"Duplicate", "slug":"workspace-a"}, expected=409)
                request("/api/organizations", {"name":"Bad slug", "slug":"../default"}, expected=400)
                assert len(request("/api/organizations")) == 3
                organization = a
                request("/api/organizations/current/ai-default", {"provider":"deepseek", "model":"fixture", "version":0}, method="PUT")
                request("/api/me/ai-preference", {"provider":"deepseek", "model":"deepseek-chat"}, method="PUT")
                session = request("/api/chatBot/sessions", {}, expected=201)["id"]
                marker = "SYNTHETIC_ORGANIZATION_A_ONLY"
                item = request("/api/kb/upsert", {"keyName":"private-a", "section":"Synthetic", "content":marker, "organizationId":b})
                first = request("/api/knowledge-templates/technical-support/apply", {})
                assert first["imported"] == 2
                doc = next(x for x in request("/api/kb/all") if x["keyName"] == "template-support-triage-v1")
                edited = request(f"/api/kb/{doc['id']}")
                edited["content"] += "\nHuman edit must survive reapply."
                request("/api/kb/upsert", edited)
                second = request("/api/knowledge-templates/technical-support/apply", {})
                assert second["imported"] == 0 and second["skipped"] == 2
                assert "Human edit" in request(f"/api/kb/{doc['id']}")["content"]
                stream = request("/api/ia/ask/stream", {"pergunta":"Como funciona a triagem de solicitações de suporte?", "sessionId":session, "requestId":str(uuid.uuid4())}, raw=True)
                assert "[*ENDSTREAN*]" in stream
                assert any("Triagem de solicitações" in prompt and "Human edit must survive" in prompt for prompt in PROMPTS)
                bob_password = uuid.uuid4().hex
                bob = request("/api/users", {"userName":"Member A", "userEmail":"member@example.test", "password":bob_password, "groupIds":[]}, expected=201)["userId"]
                request(f"/api/organizations/current/members/{bob}", {"role":"Member", "isActive":True, "version":0}, method="PUT", expected=204)
                request("/api/organizations/current/members/1", {"role":"Viewer", "isActive":True, "version":0}, method="PUT", expected=409)
                organization = b
                assert request("/api/organizations/current/ai-default")["inherited"]
                assert request("/api/me/ai-preference")["model"] == "deepseek-chat"
                assert request("/api/kb/all") == []
                request(f"/api/kb/{item['id']}", expected=404)
                request(f"/api/chatBot/sessions/{session}", expected=404)
                request(f"/api/usage/summary?sessionId={session}", expected=404)
                assert request("/api/data-connectors") == []
                assert request("/api/chatBot/sessions")["items"] == []
                request("/api/admin/ai/providers", expected=403)
                member = request("/api/auth/login", {"email":"member@example.test", "password":bob_password})
                token = member["token"]; organization = a
                assert request("/api/auth/me")["roles"] == ["Member"]
                request("/api/kb/all", selected=b, expected=401)
                request("/api/organizations", {"name":"Unauthorized", "slug":"unauthorized"}, expected=403)
                request("/api/organizations/current/ai-default", {"provider":"deepseek", "model":"fixture", "version":1}, method="PUT", expected=403)
                request(f"/api/chatBot/sessions/{session}", expected=404)  # same tenant, different owner
                request("/api/organizations/current/members", expected=403)
                token = owner["token"]
                request(f"/api/organizations/current/members/{bob}", {"role":"Admin", "isActive":True, "version":1}, method="PUT", expected=204)
                token = member["token"]
                assert "Admin" in request("/api/auth/me")["roles"]
                request("/api/organizations/current/members/1", {"role":"Member", "isActive":True, "version":0}, method="PUT", expected=403)
                request("/api/users/1", {"userName":"Administrador", "userEmail":"owner@example.test", "isActive":True, "groupIds":[], "password":uuid.uuid4().hex}, method="PUT", expected=403)
                token = owner["token"]
                request(f"/api/organizations/current/members/{bob}", {"role":"Viewer", "isActive":True, "version":1}, method="PUT", expected=409)
                request(f"/api/organizations/current/members/{bob}", {"role":"Viewer", "isActive":True, "version":2}, method="PUT", expected=204)
                token = member["token"]
                assert request("/api/auth/me")["roles"] == ["Viewer"]  # existing JWT, DB permissions refreshed
                request("/api/chatBot/sessions", expected=403)
                request("/api/kb/upsert", {"keyName":"bad", "section":"bad", "content":"bad"}, expected=403)
                assert request("/api/kb/all")
                token = owner["token"]
                request(f"/api/organizations/current/members/{bob}", {"role":"Viewer", "isActive":False, "version":3}, method="PUT", expected=204)
                token = member["token"]
                request("/api/auth/me", expected=401)
                # New recipient already has an account in B, but no access to A.
                token = owner["token"]; organization = b
                recipient_secret = uuid.uuid4().hex
                recipient_id = request("/api/users", {"userName":"Recipient B", "userEmail":"recipient@example.test", "password":recipient_secret, "groupIds":[]}, expected=201)["userId"]
                recipient_token = request("/api/auth/login", {"email":"recipient@example.test", "password":recipient_secret})["token"]
                organization = a
                invite = request("/api/organizations/current/invitations", {"role":"Viewer"}, expected=201)
                listed = request("/api/organizations/current/invitations")
                assert invite["token"] not in json.dumps(listed) and "tokenHash" not in json.dumps(listed)
                token = ""
                request("/api/organization-invitations/preview", {"token":invite["token"]}, expected=401)
                token = recipient_token; organization = b
                request("/api/kb/all", selected=a, expected=401)
                preview = request("/api/organization-invitations/preview", {"token":invite["token"]})
                assert preview["name"] == "Workspace A" and preview["role"] == "Viewer"
                assert len(request("/api/organizations")) == 1, "Preview must not join"
                joined = request("/api/organization-invitations/accept", {"token":invite["token"], "organizationId":b, "role":"Owner"})
                assert joined["organizationId"] == a and not joined["alreadyMember"]
                assert request("/api/auth/me", selected=a)["roles"] == ["Viewer"]
                request("/api/chatBot/sessions", selected=a, expected=403)
                request("/api/organization-invitations/accept", {"token":invite["token"]}, expected=404)
                request("/api/organization-invitations/preview", {"token":"invalid"}, expected=404)
                token = owner["token"]; organization = a
                keep = request("/api/organizations/current/invitations", {"role":"Member"}, expected=201)
                token = recipient_token; organization = b
                assert request("/api/organization-invitations/accept", {"token":keep["token"]})["alreadyMember"]
                assert request("/api/auth/me", selected=a)["roles"] == ["Viewer"], "Existing role is preserved"
                token = owner["token"]; organization = a
                revoke = request("/api/organizations/current/invitations", {"role":"Member"}, expected=201)
                request(f"/api/organizations/current/invitations/{revoke['id']}/revoke", {"version":0}, selected=b, expected=404)
                request(f"/api/organizations/current/invitations/{revoke['id']}/revoke", {"version":0}, expected=204)
                token = recipient_token; organization = b
                request("/api/organization-invitations/preview", {"token":revoke["token"]}, expected=404)
                token = owner["token"]; organization = a
                request(f"/api/organizations/current/members/{recipient_id}", {"role":"Admin", "isActive":True, "version":0}, method="PUT", expected=204)
                token = recipient_token
                request("/api/organizations/current/invitations", expected=403)
                request("/api/organizations/current/invitations", {"role":"Member"}, expected=403)
                request("/api/organizations/current/transfer-ownership", {"targetUserId":1,"ownerVersion":0,"targetVersion":0}, expected=403)
                token = owner["token"]
                pending = request("/api/organizations/current/invitations", {"role":"Member"}, expected=201)
                request("/api/organizations/current/transfer-ownership", {"targetUserId":recipient_id,"ownerVersion":99,"targetVersion":1}, expected=409)
                assert "Owner" in request("/api/auth/me")["roles"], "Failed transfer must be atomic"
                request("/api/organizations/current/transfer-ownership", {"targetUserId":recipient_id,"ownerVersion":0,"targetVersion":1}, expected=204)
                assert request("/api/auth/me")["roles"] == ["Admin"]
                assert "Owner" in request("/api/auth/me", selected=b)["roles"], "Other memberships untouched"
                request("/api/organizations/current/invitations", expected=403)
                token = recipient_token; organization = b
                request("/api/organization-invitations/preview", {"token":pending["token"]}, expected=404)
                organization = a
                assert request("/api/auth/me")["roles"] == ["Owner"]
                request(f"/api/organizations/current/members/{recipient_id}", {"role":"Viewer","isActive":True,"version":2}, method="PUT", expected=409)
                with closing(sqlite3.connect(database)) as db:
                    assert db.execute("PRAGMA integrity_check").fetchone()[0] == "ok"
                    assert db.execute("SELECT count(*) FROM OrganizationAiDefaults WHERE OrganizationId=?", (a,)).fetchone()[0] == 1
                    assert db.execute("SELECT count(*) FROM OrganizationAiDefaults WHERE OrganizationId=?", (b,)).fetchone()[0] == 0
                    assert db.execute("SELECT count(*) FROM UserRoles WHERE UserId=?", (recipient_id,)).fetchone()[0] == 0
                    assert all(value[0] not in (invite["token"],keep["token"],revoke["token"],pending["token"]) for value in db.execute("SELECT TokenHash FROM OrganizationInvitations"))
                assert all(i["token"] not in log_path.read_text(encoding="utf8") for i in (invite,keep,revoke,pending)), "Capabilities must not be logged"
                print("PASS: real JWT/HTTP/SQLite create+switch, role revocation on existing JWT, last Owner, Viewer/Member denials, organization defaults/preferences, cross-tenant KB/session/usage/connectors and insert-only template preservation. No paid calls.")
                print("PASS: authenticated explicit invitation consent across tenants, no preview grant/replay/downgrade/owner overposting/group grant/token logging, tenant-scoped revocation, Admin denial, stale/atomic ownership transfer and issuer invalidation.")
            except Exception:
                # Only this temporary test process's synthetic log; never application logs or .env.
                print(log_path.read_text(encoding="utf8")[-2000:])
                raise
            finally:
                process.terminate(); process.wait(timeout=15)
                services.shutdown(); services.server_close()

if __name__ == "__main__":
    main()
