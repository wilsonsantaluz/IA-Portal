"""Synthetic OpenAI/Groq-compatible upstream, no external network or secrets."""
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import time
from collections import Counter

calls = Counter()


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args):
        pass

    def do_GET(self):
        self.send_response(200)
        self.end_headers()
        self.wfile.write(json.dumps(dict(calls)).encode())

    def do_POST(self):
        request = json.loads(self.rfile.read(int(self.headers['Content-Length'])))
        model = request['model']
        calls[model] += 1
        if model.startswith('fail'):
            self.send_response(int(model[4:]))
            self.send_header('Content-Type', 'application/json')
            self.send_header('Retry-After', '0')
            self.end_headers()
            self.wfile.write(b'{"error":{"message":"Synthetic temporary failure","type":"rate_limit_error","code":"fixture"}}')
            return
        if model == 'timeout':
            time.sleep(3)
        message = {'role': 'assistant', 'content': 'Synthetic answer'}
        if request.get('tools'):
            assert request['tools'][0]['function']['name'] == 'synthetic_lookup'
            message = {'role': 'assistant', 'content': None, 'tool_calls': [
                {'id': 'call_fixture', 'type': 'function', 'function': {'name': 'synthetic_lookup', 'arguments': '{"id":1}'}}]}
        usage = {'prompt_tokens': 7, 'completion_tokens': 3, 'total_tokens': 10}
        base = {'id': 'fixture-id', 'created': 1700000000, 'model': 'fixture-effective', 'service_tier': 'on_demand'}
        self.send_response(200)
        self.send_header('Content-Type', 'text/event-stream' if request.get('stream') else 'application/json')
        self.end_headers()
        try:
            if request.get('stream'):
                delta = {k: v for k, v in message.items() if v is not None}
                chunk = {**base, 'object': 'chat.completion.chunk', 'choices': [{'index': 0, 'delta': delta, 'finish_reason': None}]}
                end = {**base, 'object': 'chat.completion.chunk', 'choices': [{'index': 0, 'delta': {}, 'finish_reason': 'tool_calls' if request.get('tools') else 'stop'}], 'usage': usage}
                for item in (chunk, end):
                    self.wfile.write(('data: ' + json.dumps(item) + '\n\n').encode())
                    self.wfile.flush()
                self.wfile.write(b'data: [DONE]\n\n')
            else:
                self.wfile.write(json.dumps({**base, 'object': 'chat.completion', 'choices': [{'index': 0, 'message': message, 'finish_reason': 'tool_calls' if request.get('tools') else 'stop'}], 'usage': usage}).encode())
        except (BrokenPipeError, ConnectionResetError):
            pass


ThreadingHTTPServer(('0.0.0.0', 8080), Handler).serve_forever()
