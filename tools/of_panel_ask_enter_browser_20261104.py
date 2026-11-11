"""Real Chromium DOM acceptance of OpenFill showAsk Enter/Shift+Enter.
No external service, WebView2 host is stubbed and outgoing messages recorded.
"""
from pathlib import Path
from playwright.sync_api import sync_playwright

html = Path(__file__).resolve().parents[1] / "src" / "OpenFill.Core" / "Assets" / "panel__v0.18__2026-10-12_2034.html"
chrome = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
checks = 0

def ok(cond, label):
    global checks
    if not cond:
        raise AssertionError("OPENFILL_ASK_BROWSER_" + label)
    checks += 1

with sync_playwright() as p:
    browser = p.chromium.launch(executable_path=chrome, headless=True, args=["--no-sandbox"])
    page = browser.new_page()
    page.add_init_script("""
        window.__outgoing = [];
        window.chrome = window.chrome || {};
        window.chrome.webview = {
            postMessage: o => window.__outgoing.push(o),
            addEventListener: () => {}
        };
    """)
    page.goto(html.as_uri(), wait_until="domcontentloaded")
    ok(page.evaluate("typeof window.openfillPanel?.handle") == "function", "panel_loaded")

    page.evaluate("""window.openfillPanel.handle({
        kind:"ask", id:"ask-a", question:"Local prompt?", options:[] })""")
    card = page.locator('[data-card="ask-a"]')
    ok(card.count() == 1, "card_rendered")
    card.locator('input').fill("Answer from field")
    card.locator('textarea.note').fill("Additional note")
    card.locator('textarea.note').press("Enter")
    outbound = page.evaluate("window.__outgoing.filter(x => x.kind==='answer')")
    ok(len(outbound) == 1, "enter_one_message")
    ok(outbound[0]["id"] == "ask-a", "enter_card_id")
    ok(outbound[0]["text"] == "Answer from field\n\nMessage from the person: Additional note", "with_note_content")
    card.locator('textarea.note').press("Enter")
    ok(page.evaluate("window.__outgoing.filter(x => x.kind==='answer').length") == 1, "duplicate_suppressed")

    page.evaluate("""window.openfillPanel.handle({
        kind:"ask", id:"ask-b", question:"Second prompt?", options:[] })""")
    card2 = page.locator('[data-card="ask-b"]')
    card2.locator('textarea.note').fill("First")
    card2.locator('textarea.note').press("Shift+Enter")
    ok(card2.locator('textarea.note').input_value() == "First\n", "shift_enter_newline")
    ok(page.evaluate("window.__outgoing.filter(x => x.kind==='answer').length") == 1, "shift_not_submitted")
    page.evaluate("""document.querySelector('[data-card="ask-b"] textarea.note')
        .dispatchEvent(new KeyboardEvent("keydown",{key:"Enter",repeat:true,bubbles:true,cancelable:true}))""")
    ok(page.evaluate("window.__outgoing.filter(x => x.kind==='answer').length") == 1, "held_key_ignored")
    page.evaluate("""document.querySelector('[data-card="ask-b"] textarea.note')
        .dispatchEvent(new KeyboardEvent("keydown",{key:"Enter",isComposing:true,bubbles:true,cancelable:true}))""")
    ok(page.evaluate("window.__outgoing.filter(x => x.kind==='answer').length") == 1, "ime_ignored")
    card2.locator('input').fill("Second answer")
    card2.locator('textarea.note').press("Enter")
    outbound = page.evaluate("window.__outgoing.filter(x => x.kind==='answer')")
    ok(len(outbound) == 2 and outbound[1]["id"] == "ask-b", "second_card_sent")
    ok("Message from the person: First" in outbound[1]["text"], "second_note_present")
    browser.close()

print(f"OPENFILL_ASK_BROWSER_{checks}_CHECKS_OK")
