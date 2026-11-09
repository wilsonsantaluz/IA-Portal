"""Synthetic private bridge double for HTTP integration tests, never a WhatsApp session."""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import threading


class BridgeFixture:
    def __init__(self, secret):
        self.inbox = {}
        self.outbox = {}
        self.calls = []
        self.sends = 0
        self.ambiguous = False
        fixture = self

        class Handler(BaseHTTPRequestHandler):
            def log_message(self, *_): pass

            def do_GET(self): self.handle_request()
            def do_POST(self): self.handle_request()

            def handle_request(self):
                if self.headers.get('Authorization') != 'Bearer ' + secret:
                    self.send_response(401); self.end_headers(); return
                _, _, organization, connection, action = self.path.split('/')
                key = organization + '/' + connection
                fixture.calls.append((key, action))
                raw = b''
                if self.headers.get('Transfer-Encoding') == 'chunked':
                    while True:
                        size = int(self.rfile.readline().strip().split(b';')[0], 16)
                        if not size:
                            self.rfile.readline(); break
                        raw += self.rfile.read(size); self.rfile.read(2)
                else:
                    raw = self.rfile.read(int(self.headers.get('Content-Length', 0)))
                body = json.loads(raw or '{}')
                result = {}
                if action == 'inbox': result = fixture.inbox.get(key, [])
                elif action == 'ack': fixture.inbox[key] = [m for m in fixture.inbox.get(key, []) if m['externalId'] not in body['ids']]
                elif action in ('status', 'start', 'stop', 'logout'):
                    result = {'state': 'connected' if action in ('start', 'status') else 'disconnected', 'qr': None}
                elif action in ('send', 'delivery'):
                    message = (key, body['id'])
                    if action == 'send' and message not in fixture.outbox:
                        fixture.sends += 1
                        fixture.outbox[message] = {'state': 'unknown' if fixture.ambiguous else 'sent', 'external': 'synthetic:' + body['id']}
                    result = fixture.outbox.get(message, {'state': 'absent'})
                value = json.dumps(result).encode()
                self.send_response(200); self.send_header('Content-Type', 'application/json'); self.send_header('Content-Length', str(len(value))); self.end_headers(); self.wfile.write(value)

        self.server = ThreadingHTTPServer(('127.0.0.1', 0), Handler)
        self.url = f'http://127.0.0.1:{self.server.server_port}/'
        threading.Thread(target=self.server.serve_forever, daemon=True).start()

    def close(self):
        self.server.shutdown(); self.server.server_close()
