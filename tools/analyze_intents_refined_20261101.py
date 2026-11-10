"""OF-IMP E01: reproducible, conservative classification of historical task intents.
Reads the existing aggregate audit; never claims individual UI effects were verified.
"""
import collections
import json
import re
import sys
import unicodedata
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs" / "OPENFILL_TOKEN_AUDIT_DATA_20261031.json"
OUT = ROOT / "docs" / "OPENFILL_INTENTS_REFINED_20261101.json"
REPORT = ROOT / "docs" / "OPENFILL_INTENTS_REFINED_20261101.md"

def plain(value):
    text = unicodedata.normalize("NFKD", str(value or "").lower())
    return "".join(ch for ch in text if not unicodedata.combining(ch))

def service_of(item):
    hosts = [plain(x) for x in (item.get("domains") or {}).keys()]
    hosts.append(plain(item.get("site")))
    text = " ".join(hosts)
    if "studio.youtube.com" in text: return "youtube_studio"
    if "youtube.com" in text: return "youtube_other"
    if "mail.google.com" in text or "gmail.com" in text: return "gmail"
    if "business.google.com" in text: return "google_business"
    if "messenger.com" in text: return "meta_messenger"
    if "business.facebook.com" in text: return "meta_business"
    if "facebook.com" in text or "instagram.com" in text or "facebook/meta" in text: return "meta_other"
    if "linkedin" in text: return "linkedin"
    if "whatsapp" in text: return "whatsapp"
    if "airbnb" in text: return "airbnb"
    if "google.com" in text or "google" in text: return "google_other"
    return "other"

def classify(item, service):
    text = plain(item.get("task_preview") or item.get("task") or "")
    def hit(pattern): return bool(re.search(pattern, text))
    # Avoid inferring actual send actions from tool calls alone: a send tool may fail.
    if service == "airbnb" and hit(r"\b(price|pricing|rate|cena|ceny|stawki|podnies|obniz|increase|decrease|calendar|kalendarz|discount)\b"):
        return "edit_price_calendar", "high"
    if service in ("meta_business", "meta_other") and hit(r"\b(zaplanuj|schedule|scheduled|planowane|postow)\b") and hit(r"\b(post|postow|posts|publikacj)\b"):
        return "schedule_posts", "high"
    if service == "youtube_studio" and hit(r"\b(branding|konfiguracja|konfiguracje|configure|configuration|ustawienia|settings)\b"):
        return "configure_channel", "high"
    if hit(r"\b(wyslij|wyslac|send|napisz|napisac|message|wiadomosc|wiadomosci|dm|reply|odpowiedz)\b") and hit(r"\b(contact|kontakt|osob|person|people|recipient|adresat|linkedin|whatsapp|messenger|email|mail|wiadomosc|wiadomosci|message|inbox|czat|chat|thread|watek|do|to)\b"):
        return "send_message", "high"
    if hit(r"\b(zapros|invite|invitation|connect with|connection request)"):
        return "send_invitation", "high"
    if hit(r"\b(publish|opublikuj|post|comment|komentarz|skomentuj)\b"):
        return "publish_comment", "high"
    if hit(r"\b(export|eksport|download|pobierz|extract|wyeksportuj|scrape|zapisz)\b"):
        return "export_extract", "high"
    if hit(r"\b(read|odczytaj|przeczytaj|pokaz|summarize|podsumuj|inbox|historia|historie|conversation|rozmow|messages|wiadomosci|thread|watek|czat|chat)\b"):
        return "read_thread_or_data", "medium"
    if hit(r"\b(find|search|znajdz|poszukaj|szukaj|lookup|wyszukaj|profile|profil|contact|kontakt|lead|prospect)\b"):
        return "search_resolve", "medium"
    if hit(r"\b(update|edit|change|zmien|zaktualizuj|edytuj|ustaw|configure|setting|formularz|fill)\b"):
        return "edit_record_settings", "medium"
    if hit(r"\b(debug|diagnos|diagnoz|error|test|sprawdz|problem|napraw|fix)\b"):
        return "diagnose_test", "medium"
    return "unknown", "low"

def metric(item):
    tools = item.get("tool_counts") or {}
    seq = item.get("tool_sequence") or []
    if not tools and seq: tools = dict(collections.Counter(seq))
    return {
        "calls": int(item.get("calls") or 0),
        "input_tokens": int(item.get("inputTokens") or 0),
        "cached_tokens": int(item.get("cachedTokens") or 0),
        "uncached_tokens": int(item.get("uncached") or 0),
        "cost_logged_usd": float(item.get("costUsd") or 0),
        "get_page": int(tools.get("get_page", 0)),
        "run_js": int(tools.get("run_js", 0)),
        "act": int(tools.get("act", 0)) + int(tools.get("act_many", 0)),
        "repeat_tool_invocations": sum(max(0, int(tools.get(k, 0)) - 1) for k in ("get_page", "run_js", "act")),
    }

def analyze(data):
    groups = {}
    totals = collections.Counter()
    for run_id, item in sorted(data["runs"].items()):
        service = service_of(item)
        intent, confidence = classify(item, service)
        key = (service, intent)
        if key not in groups:
            groups[key] = {"service": service, "intent": intent, "tasks": 0,
                           "confidence_counts": {"high": 0, "medium": 0, "low": 0},
                           "logged_outcomes": {}, "examples": [], **{k: 0 for k in metric(item)}}
        row = groups[key]
        row["tasks"] += 1
        row["confidence_counts"][confidence] += 1
        outcome = str(item.get("outcome") or "unknown")
        row["logged_outcomes"][outcome] = row["logged_outcomes"].get(outcome, 0) + 1
        for k, v in metric(item).items(): row[k] += v
        if len(row["examples"]) < 5: row["examples"].append(run_id)
        totals["runs"] += 1
        totals["calls"] += int(item.get("calls") or 0)
    rows = sorted(groups.values(), key=lambda r: (-r["calls"], -r["tasks"], r["service"], r["intent"]))
    for row in rows:
        row["calls_per_task"] = round(row["calls"] / row["tasks"], 1)
        row["confidence_nonlow_share"] = round(1 - row["confidence_counts"]["low"] / row["tasks"], 3)
        row["cost_logged_usd"] = round(row["cost_logged_usd"], 5)
    return {"source": SOURCE.name, "method": "heuristic, service from observed hosts; intent from task text",
            "limitations": ["Outcome ok is a log label, not verified external effect.",
                            "Repeated tool invocations are not proof of identical DOM reads.",
                            "Zero logged cost may mean missing price data.",
                            "Target counts and verified delivery cannot be inferred from this aggregate audit."],
            "totals": dict(totals), "groups": rows}

def selftest():
    cases = [
        ({"site": "LinkedIn", "task_preview": "Send a message to 10 contacts"}, "linkedin", "send_message"),
        ({"site": "Airbnb", "task_preview": "Podnieś ceny w kalendarzu"}, "airbnb", "edit_price_calendar"),
        ({"site": "Google", "domains": {"mail.google.com": 1}, "task_preview": "Read inbox"}, "gmail", "read_thread_or_data"),
        ({"site": "Facebook/Meta", "domains": {"messenger.com": 1}, "task_preview": "Find contacts"}, "meta_messenger", "search_resolve"),
        ({"site": "Google", "domains": {"studio.youtube.com": 1}, "task_preview": "Publish post"}, "youtube_studio", "publish_comment"),
        ({"site": "LinkedIn", "task_preview": "Please do something"}, "linkedin", "unknown"),
        ({"site": "YouTube", "domains": {"studio.youtube.com": 1}, "task_preview": "YouTube Studio branding kanału"}, "youtube_studio", "configure_channel"),
        ({"site": "Facebook/Meta", "domains": {"business.facebook.com": 1}, "task_preview": "Zaplanuj 12 postów na stronie"}, "meta_business", "schedule_posts"),
    ]
    for item, service, intent in cases:
        assert service_of(item) == service, (item, service_of(item))
        assert classify(item, service)[0] == intent, (item, classify(item, service))
    print("E01_REFINED_SELFTEST_PASS", len(cases))

def main():
    if "--selftest" in sys.argv:
        selftest()
        return
    data = json.loads(SOURCE.read_text(encoding="utf-8"))
    result = analyze(data)
    assert result["totals"]["runs"] == len(data["runs"])
    assert result["totals"]["calls"] == sum(int(x.get("calls") or 0) for x in data["runs"].values())
    OUT.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    lines = ["# OF-IMP E01: intencje z historii, klasyfikacja v2", "",
             f"Zadania: {result['totals']['runs']}; model calls: {result['totals']['calls']}.",
             "To klasyfikacja heurystyczna, a nie potwierdzenie skutków działań w serwisach.", "",
             "| Serwis | Intencja | Zadania | Calls | Calls/zadanie | get_page | run_js | Koszt logowany USD |",
             "| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |"]
    for r in result["groups"]:
        lines.append(f"| {r['service']} | {r['intent']} | {r['tasks']} | {r['calls']} | {r['calls_per_task']} | {r['get_page']} | {r['run_js']} | {r['cost_logged_usd']:.4f} |")
    lines += ["", "## Ograniczenia", *["- " + x for x in result["limitations"]],
              "", "Przykładowe run IDs, wyniki logowane i pewność heurystyki są w JSON."]
    REPORT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(json.dumps({"status": "E01_REFINED_PASS", "runs": result["totals"]["runs"],
                      "calls": result["totals"]["calls"], "groups": len(result["groups"]),
                      "top": [{"service": x["service"], "intent": x["intent"],
                               "tasks": x["tasks"], "calls": x["calls"]} for x in result["groups"][:10]]},
                     ensure_ascii=False))

if __name__ == "__main__":
    main()
