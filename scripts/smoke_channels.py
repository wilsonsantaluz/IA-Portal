"""Actual JWT/HTTP/webhook/worker/SQLite channel MVP; temporary data, no Meta calls."""
import hashlib
import hmac
import json
import os
from pathlib import Path
import socket
import sqlite3
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import uuid
from contextlib import closing
from browser_bridge_fixture import BridgeFixture

ROOT = Path(__file__).resolve().parents[1]

def main():
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0)); port = probe.getsockname()[1]
    base = f"http://127.0.0.1:{port}"
    opener = urllib.request.build_opener(urllib.request.ProxyHandler({}))
    with tempfile.TemporaryDirectory(prefix="portal-channels-http-") as folder:
        database = Path(folder)/"portal.db"
        password, secret, verification = uuid.uuid4().hex, uuid.uuid4().hex, uuid.uuid4().hex
        env = dict(os.environ)
        env.update({"ASPNETCORE_URLS":base,"ASPNETCORE_ENVIRONMENT":"Production",
            "ConnectionStrings__PortalConnection":f"Data Source={database};Foreign Keys=True;Pooling=False",
            "SQLITE_BACKUP_DIRECTORY":str(Path(folder)/"backups"),"Bootstrap__AdminEmail":"owner@example.test",
            "Bootstrap__AdminPassword":password,"Jwt__Key":uuid.uuid4().hex+uuid.uuid4().hex,
            "Demo__Atlas__SeedKnowledge":"false","Chat__UseAdvancedRag":"false",
            "DEEPSEEK_ENABLED":"false","OPENAI_ENABLED":"false","OLLAMA_ENABLED":"false",
            "WhatsApp__AppSecret":secret,"WhatsApp__VerifyToken":verification,
            "WhatsApp__AppId":"55555","WhatsApp__AccessToken":"synthetic-access-token-only",
            "WhatsApp__PublicBaseUrl":"https://synthetic-webhook.example.test/",
            "WhatsApp__Routes__12345__OrganizationId":"1","WhatsApp__Routes__12345__AccountId":"98765"})
        bridge_secret=uuid.uuid4().hex+uuid.uuid4().hex
        fixture=BridgeFixture(bridge_secret)
        env.update({"BrowserBridge__Enabled":"true","BrowserBridge__Url":fixture.url,"BrowserBridge__Secret":bridge_secret})
        token, organization = "", 1
        def request(path,data=None,method=None,expected=200,anonymous=False,signature=None):
            headers={"Content-Type":"application/json","X-Organization-Id":str(organization)}
            if token and not anonymous: headers["Authorization"]="Bearer "+token
            body=json.dumps(data).encode() if data is not None else None
            if signature is not None:
                headers["X-Hub-Signature-256"]="sha256="+hmac.new(signature.encode(),body,hashlib.sha256).hexdigest()
            try:
                with opener.open(urllib.request.Request(base+path,headers=headers,data=body,method=method),timeout=20) as response:
                    text=response.read().decode(); assert response.status==expected,(path,response.status)
                    if response.headers.get_content_type()=="application/json": return json.loads(text) if text else None
                    return text
            except urllib.error.HTTPError as error:
                assert error.code==expected,(path,error.code,error.read().decode()[:200]); return None
        log_path=Path(folder)/"gateway.log"
        with log_path.open("w",encoding="utf8") as log:
            process=subprocess.Popen(["dotnet",str(ROOT/"ai-gateway/bin/Debug/net8.0/AiGateway.dll")],cwd=ROOT/"ai-gateway",env=env,stdout=log,stderr=log,creationflags=subprocess.CREATE_NO_WINDOW if os.name=="nt" else 0)
            try:
                for _ in range(100):
                    if process.poll() is not None: raise RuntimeError(log_path.read_text(encoding="utf8")[-1600:])
                    try: request("/swagger/v1/swagger.json"); break
                    except OSError: time.sleep(.2)
                else: raise RuntimeError("Gateway startup timeout")
                request("/api/channels",expected=401)
                request("/api/channels/whatsapp/setup",expected=401)
                token=request("/api/auth/login",{"email":"owner@example.test","password":password})["token"]
                owner=token
                setup=request("/api/channels/whatsapp/setup")
                assert setup["authorizedNumbers"]==[{"phoneNumberId":"12345","accountId":"98765"}]
                assert setup["receiveConfigurationReady"] and not setup["liveSendingAvailable"]
                assert setup["appSecretConfigured"] and setup["verifyTokenConfigured"] and setup["accessTokenConfigured"]
                assert setup["webhookUrl"]=="https://synthetic-webhook.example.test/api/webhooks/whatsapp"
                assert all(value not in json.dumps(setup) for value in (secret,verification,"synthetic-access-token-only"))
                item=request("/api/kb/upsert",{"keyName":"warranty","section":"Synthetic","title":"Garantia","content":"garantia sintética autorizada"})
                doc=item["id"]
                c=request("/api/channels",{"displayName":"Synthetic local","provider":"simulator","externalAccountId":"demo","externalPhoneNumberId":"one","knowledgeIds":[doc],"organizationId":999})
                assert not c["autoReply"] and c["status"]=="simulated"
                policy={"version":0,"isEnabled":True,"autoReply":True,"agentEnabled":True,"retentionDays":30,"monthlyLimit":3}
                request(f'/api/channels/{c["id"]}',policy,"PUT",204)
                incoming={"externalId":"synthetic-1","contactId":"synthetic-contact","text":"garantia","timestamp":time.strftime("%Y-%m-%dT%H:%M:%SZ",time.gmtime())}
                assert request(f'/api/channels/{c["id"]}/simulate',incoming)["accepted"]
                assert not request(f'/api/channels/{c["id"]}/simulate',incoming)["accepted"]
                def wait_output():
                    for _ in range(30):
                        conversations=request(f'/api/channels/{c["id"]}/conversations')
                        if conversations:
                            messages=request(f'/api/channels/conversations/{conversations[0]["id"]}/messages')
                            if any(m["direction"]=="outbound" and m["state"]=="simulated" for m in messages): return conversations[0],messages
                        time.sleep(.2)
                    raise AssertionError("Worker did not produce simulated output")
                conv,messages=wait_output(); assert len(messages)==2
                answer=next(m for m in messages if m["direction"]=="outbound")
                assert "autorizada" in answer["text"] and str(doc) in answer["sourcesJson"] and answer["tokenUsage"] is None
                request(f'/api/channels/conversations/{conv["id"]}/handoff',{"version":conv["version"],"humanHandoff":True},expected=204)
                incoming["externalId"]="human-held"; request(f'/api/channels/{c["id"]}/simulate',incoming)
                time.sleep(2.3); assert len(request(f'/api/channels/conversations/{conv["id"]}/messages'))==3
                assert request(f'/api/channels/{c["id"]}/usage')["outbound"]==1
                # Official envelope/signature path, synthetic secrets only; no outgoing HTTP call.
                cloud=request("/api/channels",{"displayName":"Synthetic Cloud receive","provider":"whatsapp-cloud","externalAccountId":"98765","externalPhoneNumberId":"12345","knowledgeIds":[doc]})
                request(f'/api/channels/{cloud["id"]}',policy,"PUT",409)
                request("/api/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token=wrong&hub.challenge=123",anonymous=True,expected=401)
                assert request(f"/api/webhooks/whatsapp?hub.mode=subscribe&hub.verify_token={verification}&hub.challenge=123",anonymous=True)=="123"
                payload={"object":"whatsapp_business_account","entry":[{"id":"98765","changes":[{"field":"messages","value":{"metadata":{"phone_number_id":"12345"},"messages":[{"id":"wamid.HBgSyntheticExample+/==","from":"synthetic-contact","type":"text","timestamp":str(int(time.time())),"text":{"body":"garantia"}}]}}]}]}
                request("/api/webhooks/whatsapp",payload,anonymous=True,signature="wrong",expected=401)
                request("/api/webhooks/whatsapp",payload,anonymous=True,expected=401)
                request("/api/webhooks/whatsapp",{"oversized":"x"*270000},anonymous=True,signature=secret,expected=413)
                request("/api/webhooks/whatsapp",payload,anonymous=True,signature=secret)
                request("/api/webhooks/whatsapp",payload,anonymous=True,signature=secret)
                request("/api/webhooks/whatsapp",["malformed"],anonymous=True,signature=secret,expected=400)
                request("/api/webhooks/whatsapp",{"object":"whatsapp_business_account","entry":[None,{"id":12,"changes":[None,{"field":"messages","value":None}]}]},anonymous=True,signature=secret)
                cloud_conv=request(f'/api/channels/{cloud["id"]}/conversations')[0]
                assert len(request(f'/api/channels/conversations/{cloud_conv["id"]}/messages'))==1
                # Echo/history ignored: never treated as customer input.
                payload["entry"][0]["changes"][0]["field"]="smb_message_echoes"
                request("/api/webhooks/whatsapp",payload,anonymous=True,signature=secret)
                assert len(request(f'/api/channels/conversations/{cloud_conv["id"]}/messages'))==1
                # Real portal/JWT/worker against a synthetic bridge; never contacts WhatsApp.
                assert request('/api/channels/browser/capabilities')['enabled']
                browser=request('/api/channels',{'displayName':'Browser synthetic','provider':'whatsapp-browser','externalAccountId':'','externalPhoneNumberId':'','knowledgeIds':[doc]})
                bid=browser['id']; key='1/'+bid
                assert not browser['autoReply'] and browser['externalAccountId']=='org:1'
                request(f'/api/channels/{bid}/browser',anonymous=True,expected=401)
                assert request(f'/api/channels/{bid}/browser/start',{})['state']=='connected'
                request(f'/api/channels/{bid}/browser/arbitrary',{},expected=400)
                fixture.inbox[key]=[{'externalId':'browser-text','contactId':'5511999999999@c.us','text':'garantia','timestamp':time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime())}]
                for _ in range(40):
                    convs=request(f'/api/channels/{bid}/conversations')
                    if convs: break
                    time.sleep(.2)
                else: raise AssertionError('Browser inbox not ingested')
                bc=convs[0]
                request(f'/api/channels/conversations/{bc["id"]}/reply',{'text':'manual','requestId':str(uuid.uuid4())},expected=409)
                request(f'/api/channels/conversations/{bc["id"]}/handoff',{'version':bc['version'],'humanHandoff':True},expected=204)
                reply={'text':'manual synthetic','requestId':str(uuid.uuid4())}
                request(f'/api/channels/conversations/{bc["id"]}/reply',reply)
                request(f'/api/channels/conversations/{bc["id"]}/reply',reply)
                for _ in range(40):
                    bm=request(f'/api/channels/conversations/{bc["id"]}/messages')
                    if any(m['state']=='sent' for m in bm): break
                    time.sleep(.2)
                else: raise AssertionError('Browser outbox not delivered')
                assert fixture.sends==1 and len(bm)==2
                assert not fixture.inbox[key]
                bc=request(f'/api/channels/{bid}/conversations')[0]
                request(f'/api/channels/conversations/{bc["id"]}/handoff',{'version':bc['version'],'humanHandoff':False},expected=204)
                request(f'/api/channels/{bid}',policy,'PUT',204)
                def browser_message(external):
                    fixture.inbox[key]=[{'externalId':external,'contactId':'5511999999999@c.us','text':'garantia','timestamp':time.strftime('%Y-%m-%dT%H:%M:%SZ',time.gmtime())}]
                def browser_output(state, minimum):
                    for _ in range(60):
                        rows=request(f'/api/channels/conversations/{bc["id"]}/messages')
                        if sum(m['direction']=='outbound' for m in rows)>=minimum and any(m['state']==state and m['category']!='manual' for m in rows):return rows
                        time.sleep(.2)
                    raise AssertionError('Browser automatic output missing: '+state)
                browser_message('browser-auto')
                rows=browser_output('sent',2)
                assert any('autorizada' in m['text'] and str(doc) in m['sourcesJson'] for m in rows if m['direction']=='outbound' and m['category']!='manual')
                assert fixture.sends==2
                fixture.ambiguous=True;browser_message('browser-ambiguous');browser_output('unknown',3)
                time.sleep(2.3);assert fixture.sends==3,'Ambiguous browser sends must not be replayed'
                other=request("/api/organizations",{"name":"Other synthetic","slug":"other-synthetic"},expected=201)["id"]
                organization=other
                setup=request("/api/channels/whatsapp/setup")
                assert setup["authorizedNumbers"]==[] and not setup["receiveConfigurationReady"]
                before=len(fixture.calls)
                request(f"/api/channels/{bid}/browser",expected=404)
                request(f"/api/channels/{bid}/browser/start",{},expected=404)
                request(f'/api/channels/conversations/{bc["id"]}/reply',reply,expected=404)
                assert not any(k.startswith(str(other)+"/") for k,a in fixture.calls[before:])
                assert request("/api/channels")==[]
                request(f'/api/channels/{c["id"]}/conversations',expected=404)
                request(f'/api/channels/conversations/{conv["id"]}/messages',expected=404)
                request(f'/api/channels/{c["id"]}/simulate',incoming,expected=404)
                request("/api/channels",{"displayName":"Unauthorized binding","provider":"whatsapp-cloud","externalAccountId":"98765","externalPhoneNumberId":"12345","knowledgeIds":[]},expected=409)
                member_password=uuid.uuid4().hex
                member=request("/api/users",{"userName":"Synthetic member","userEmail":"member@example.test","password":member_password,"groupIds":[]},expected=201)["userId"]
                request(f"/api/organizations/current/members/{member}",{"role":"Member","isActive":True,"version":0},"PUT",204)
                token=request("/api/auth/login",{"email":"member@example.test","password":member_password})["token"]
                request("/api/channels",expected=403)
                request("/api/channels/whatsapp/setup",expected=403)
                request("/api/channels/browser/capabilities",expected=403)
                request(f"/api/channels/{bid}/browser",expected=403)
                with closing(sqlite3.connect(database)) as db:
                    assert db.execute("PRAGMA integrity_check").fetchone()[0]=="ok"
                    assert db.execute("SELECT count(*) FROM ChannelMessages WHERE ExternalId='wamid.HBgSyntheticExample+/=='").fetchone()[0]==1
                    assert not db.execute("PRAGMA foreign_key_check").fetchall()
                print("PASS: real JWT/HTTP temporary SQLite/worker, simulation consent/default-off, duplicates, grounding/sources, human handoff, signed official-envelope webhook/verification, ignored echoes, operator-owned routing, tenant read/write denial and Member authorization. Browser inbox/outbox idempotency, private bridge routing, manual handoff and tenant denial passed. No Meta/LLM calls.")
            finally:
                fixture.close()
                process.terminate()
                try: process.wait(timeout=10)
                except subprocess.TimeoutExpired: process.kill(); process.wait(timeout=5)

if __name__=="__main__": main()
