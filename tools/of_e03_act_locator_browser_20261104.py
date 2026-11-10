"""Focused real Chromium execution of C# embedded act_locator JS.
Tests no action when expected_text mismatches, bounded observed text, and valid click.
"""
from pathlib import Path
import re, json, threading
from http.server import ThreadingHTTPServer, BaseHTTPRequestHandler
from playwright.sync_api import sync_playwright
source=Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\src\OpenFill.Core\Agent\ToolRegistry__v0.22__2026-10-29_2314.cs").read_text(encoding="utf-8-sig")
body=source.split('Name = "act_locator"',1)[1].split('Name = "verify_view"',1)[0]
m=re.search(r'var js = """(.*?)"""\.Replace\("__HOST__"',body,re.S)
if not m: raise RuntimeError("ACT_LOCATOR_EMBEDDED_JS_NOT_FOUND")
template=m.group(1)
class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        html="""<!doctype html><html><head><title>QA</title></head>
<body data-clicks="0"><button id="entry" onclick="document.body.dataset.clicks=String(Number(document.body.dataset.clicks)+1)">Other person <span>+48 555 000 111</span></button></body></html>"""
        raw=html.encode()
        self.send_response(200);self.send_header("Content-Type","text/html; charset=UTF-8");self.send_header("Content-Length",str(len(raw)));self.end_headers();self.wfile.write(raw)
    def log_message(self,*args): pass
http=ThreadingHTTPServer(("127.0.0.1",0),Handler)
thread=threading.Thread(target=http.serve_forever,daemon=True);thread.start()
def run(page,expected):
    script=template
    for key,value in {"__HOST__":"127.0.0.1","__SELECTOR__":"#entry","__VERB__":"click","__VALUE__":"","__EXPECTED__":expected}.items():
        script=script.replace(key,json.dumps(value))
    return page.evaluate(script)
checks=0
def check(condition,label):
    global checks
    if not condition:raise AssertionError("E03_ACT_LOCATOR_"+label)
    checks+=1
try:
    with sync_playwright() as p:
        browser=p.chromium.launch(executable_path=r"C:\Program Files\Google\Chrome\Application\chrome.exe",headless=True)
        page=browser.new_page()
        page.goto(f"http://127.0.0.1:{http.server_port}/")
        wrong=run(page,"+48 575 898 178 (You)")
        check(wrong.get("reason_code")=="target_text_mismatch","MISMATCH_CAUSE")
        check(wrong.get("action_attempted") is False,"NO_ACTION")
        check("Other person" in wrong.get("observed_text",""),"VISIBLE_TEXT")
        check(wrong.get("observed_count")==1,"OBSERVED_COUNT")
        check(len(wrong.get("observed_text",""))<=180,"BOUNDED")
        check(page.locator("body").get_attribute("data-clicks")=="0","NO_SIDE_EFFECT")
        good=run(page,"Other person")
        check(good.get("ok") is True and good.get("action_attempted") is True,"CORRECT_CLICK")
        check(page.locator("body").get_attribute("data-clicks")=="1","ONE_CLICK")
        wrong2=run(page,"Unknown recipient")
        check(wrong2.get("action_attempted") is False,"SECOND_MISMATCH_SAFE")
        check(page.locator("body").get_attribute("data-clicks")=="1","NO_REPLAY")
        browser.close()
finally: http.shutdown();http.server_close()
print(f"E03_ACT_LOCATOR_CHROMIUM_{checks}_CHECKS_OK")
