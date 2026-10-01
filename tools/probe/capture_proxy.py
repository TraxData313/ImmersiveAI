"""A local pass-through proxy that records what Codex really sends to the model.

Codex is pointed at it per call with the thread/start config override
    {"chatgpt_base_url": "http://127.0.0.1:8899/backend-api"}
Every request BODY (decompressed) is written to runs/<date>/capture/; headers are forwarded to
chatgpt.com untouched and are NEVER written anywhere (they carry the login's bearer token).
WebSocket upgrades are refused so Codex falls back to plain HTTP streaming.

    python tools/probe/capture_proxy.py            (Ctrl+C to stop)
"""
import datetime, http.server, json, os, pathlib, socketserver, sys, threading
import requests

try:
    import zstandard
except ImportError:
    zstandard = None

UPSTREAM = "https://chatgpt.com"
PORT = int(os.environ.get("PROBE_PROXY_PORT", "8899"))
OUT = pathlib.Path(__file__).parent / "runs" / datetime.date.today().isoformat() / "capture"
OUT.mkdir(parents=True, exist_ok=True)
SEQ = [0]
LOCK = threading.Lock()


class Handler(http.server.BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def log_message(self, fmt, *args):
        sys.stdout.write("[proxy] " + (fmt % args) + "\n")

    def _forward(self, method):
        if self.headers.get("Upgrade", "").lower() == "websocket":
            self.send_response(426)
            self.send_header("Content-Length", "0")
            self.end_headers()
            return
        length = int(self.headers.get("Content-Length") or 0)
        body = self.rfile.read(length) if length else b""
        enc = (self.headers.get("Content-Encoding") or "").lower()
        plain = body
        if enc == "zstd" and zstandard is not None and body:
            plain = zstandard.ZstdDecompressor().decompressobj().decompress(body)
        if body and self.path.rstrip("/").endswith("/responses"):
            with LOCK:
                SEQ[0] += 1
                n = SEQ[0]
            name = OUT / f"{datetime.datetime.now():%H%M%S}_{n:03d}_request.json"
            try:
                name.write_text(json.dumps(json.loads(plain), ensure_ascii=False, indent=1), encoding="utf-8")
            except Exception:
                name.write_bytes(plain)
            print("[proxy] captured", name.name, len(plain), "bytes")
        headers = {k: v for k, v in self.headers.items() if k.lower() not in ("host", "content-length", "connection")}
        r = requests.request(method, UPSTREAM + self.path, data=body, headers=headers, stream=True, timeout=300)
        self.send_response(r.status_code)
        for k, v in r.headers.items():
            if k.lower() in ("content-length", "transfer-encoding", "connection", "content-encoding"):
                continue
            self.send_header(k, v)
        self.send_header("Transfer-Encoding", "chunked")
        self.send_header("Connection", "close")
        self.end_headers()
        for chunk in r.raw.stream(4096, decode_content=True):
            if chunk:
                self.wfile.write(b"%x\r\n%s\r\n" % (len(chunk), chunk))
                self.wfile.flush()
        self.wfile.write(b"0\r\n\r\n")
        self.close_connection = True

    def do_POST(self):
        self._forward("POST")

    def do_GET(self):
        self._forward("GET")


class Server(socketserver.ThreadingMixIn, http.server.HTTPServer):
    daemon_threads = True


if __name__ == "__main__":
    print("[proxy] listening on", PORT, "->", UPSTREAM, "capturing to", OUT)
    Server(("127.0.0.1", PORT), Handler).serve_forever()
