"""HTTP-only synthetic LLM/OCR fixtures; never forwards requests to an external provider."""
from pathlib import Path
import sys
from http.server import ThreadingHTTPServer
sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'scripts'))
from smoke_local import FakeServices
class Provider(FakeServices):
    def do_GET(self):
        if self.path != '/':
            return super().do_GET()
        self.send_response(200)
        self.end_headers()
        self.wfile.write(b'ready')
ThreadingHTTPServer(('0.0.0.0', 19091), Provider).serve_forever()
