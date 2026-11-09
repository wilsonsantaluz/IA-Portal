"""Submit synthetic text to a registered simulator. Token is read from environment."""
import argparse
import datetime
import json
import os
import urllib.request
import urllib.parse
import uuid

parser=argparse.ArgumentParser(description=__doc__)
parser.add_argument("connection_id")
parser.add_argument("--organization",type=int,required=True)
parser.add_argument("--url",default="http://127.0.0.1:9089")
parser.add_argument("--text",default="Como funciona a garantia sintética?")
parser.add_argument("--message-id",default=None,help="Repeat an ID to verify deduplication")
args=parser.parse_args()
token=os.environ.get("IA_PORTAL_TOKEN")
if not token: parser.error("Set IA_PORTAL_TOKEN locally; never put a real token in command arguments or Git.")
address=urllib.parse.urlparse(args.url)
if address.scheme!="http" or address.hostname not in ("127.0.0.1","localhost") or address.username or address.password or address.path not in ("", "/") or address.query or address.fragment:
    parser.error("Local simulator accepts a loopback portal URL only.")
try: uuid.UUID(args.connection_id)
except ValueError: parser.error("connection_id must be a UUID.")
data={"externalId":args.message_id or uuid.uuid4().hex,"contactId":"synthetic-local-contact",
      "text":args.text,"timestamp":datetime.datetime.now(datetime.timezone.utc).isoformat()}
request=urllib.request.Request(args.url.rstrip("/")+f"/api/channels/{args.connection_id}/simulate",
    data=json.dumps(data).encode(),headers={"Content-Type":"application/json","Authorization":"Bearer "+token,
    "X-Organization-Id":str(args.organization)})
opener=urllib.request.build_opener(urllib.request.ProxyHandler({}))
with opener.open(request,timeout=15) as response: print(response.read().decode())
