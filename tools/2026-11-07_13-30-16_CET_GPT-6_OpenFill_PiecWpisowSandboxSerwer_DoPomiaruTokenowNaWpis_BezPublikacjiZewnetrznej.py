"""Local-only five-post benchmark. No external posting."""
import html
import json
import os
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs
from pathlib import Path
from threading import RLock

ROOT = Path(__file__).resolve().parents[1]
STATE = ROOT / "staging" / "of_benchmark_5post_state_20261107.json"
STATE.parent.mkdir(parents=True, exist_ok=True)
PORT = 51887
LOCK = RLock()

def load():
    if not STATE.exists():
        return []
    try:
        return json.loads(STATE.read_text("utf-8"))
    except Exception:
        return []

def store(items):
    tmp = STATE.with_suffix(".tmp")
    tmp.write_text(json.dumps(items, ensure_ascii=False, indent=2), encoding="utf-8")
    os.replace(tmp, STATE)

class Handler(BaseHTTPRequestHandler):
    def log_message(self, fmt, *args):
        pass

    def do_GET(self):
        if self.path == "/api/posts":
            with LOCK:
                posts = load()
            data = json.dumps({"count":len(posts),"posts":posts},ensure_ascii=False).encode("utf-8")
            self.send_response(200); self.send_header("Content-Type","application/json; charset=utf-8")
            self.send_header("Content-Length",str(len(data)));self.end_headers();self.wfile.write(data);return
        if self.path != "/":
            self.send_error(404);return
        with LOCK: posts = load()
        parts = ['<!doctype html><html lang="pl"><head><meta charset="utf-8"><title>OpenFill: Test 5 wpisów</title></head><body>',
                 '<h1>OpenFill: Test pięciu wpisów</h1>',
                 '<p>Test lokalny. Nowe wpisy są zapisywane tylko w aplikacji testowej na PBOX1.</p>',
                 '<form method="post" action="/new"><label>Tytuł: <input id="title" name="title" required maxlength="120"></label>',
                 '<label>Treść: <textarea id="body" name="body" required maxlength="2000"></textarea></label>',
                 '<button type="submit" id="submit">Dodaj wpis</button></form>',
                 '<h2>Wpisy zapisane: '+str(len(posts))+'</h2><ol id="posts">']
        for p in posts:
            parts.append("<li><strong>"+html.escape(p["title"])+"</strong>: "+html.escape(p["body"])+"</li>")
        parts.append("</ol></body></html>")
        data = "".join(parts).encode("utf-8")
        self.send_response(200);self.send_header("Content-Type","text/html; charset=utf-8")
        self.send_header("Content-Length",str(len(data)));self.end_headers();self.wfile.write(data)

    def do_POST(self):
        if self.path != "/new":
            self.send_error(404);return
        length=int(self.headers.get("Content-Length") or "0")
        if length > 10000:
            self.send_error(413);return
        data=parse_qs(self.rfile.read(length).decode("utf-8",errors="replace"))
        title=(data.get("title") or [""])[0].strip()
        body=(data.get("body") or [""])[0].strip()
        if not title or not body:
            self.send_error(400,"Title and body required");return
        with LOCK:
            posts=load()
            if not any(p["title"] == title for p in posts):
                posts.append({"id":len(posts)+1,"title":title,"body":body})
                store(posts)
        self.send_response(303)
        self.send_header("Location","/")
        self.send_header("Content-Length","0")
        self.end_headers()

if __name__=="__main__":
    print("BENCHMARK_SANDBOX_READY",PORT,STATE,flush=True)
    ThreadingHTTPServer(("127.0.0.1",PORT),Handler).serve_forever()
