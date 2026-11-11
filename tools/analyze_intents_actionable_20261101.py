"""OF-IMP E01: conservative follow-up classification of previously unknown intents.
Read-only offline audit. No browser actions, messages, or ledger mutations.
"""
from __future__ import annotations
import collections
import importlib.util
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs" / "OPENFILL_TOKEN_AUDIT_DATA_20261031.json"
OUT = ROOT / "docs" / "OPENFILL_INTENTS_ACTIONABLE_20261101.json"
REPORT = ROOT / "docs" / "OPENFILL_INTENTS_ACTIONABLE_20261101.md"
BASE_SCRIPT = ROOT / "tools" / "analyze_intents_refined_20261101.py"
spec = importlib.util.spec_from_file_location("ofimp_refined", BASE_SCRIPT)
base = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(base)

def task_text(item):
    return base.plain(max((str(item.get("task_preview") or ""), str(item.get("task") or "")), key=len))

def service_of(item):
    old = base.service_of(item)
    if old != "google_other":
        return old
    hosts = " ".join(base.plain(x) for x in (item.get("domains") or {}).keys())
    if "tagmanager.google.com" in hosts or "analytics.google.com" in hosts:
        return "google_analytics_gtm"
    if "drive.google.com" in hosts or "docs.google.com" in hosts:
        return "google_drive_docs"
    return old

def classify(item):
    old_service = base.service_of(item)
    old_intent, old_conf = base.classify(item, old_service)
    service = service_of(item)
    if old_intent != "unknown":
        return service, old_intent, old_conf, "v2_preserved"
    t = task_text(item)
    has = lambda *terms: any(term in t for term in terms)
    read_only = has("tylko odczyt", "read only", "read-only", "nic nie zmieniaj")
    if service == "youtube_studio":
        if read_only and has("youtube", "kanal", "channel", "studio"):
            return service, "audit_channel_readonly", "high", "readonly_youtube"
        if has("short") and has("wgraj", "upload") and has("zaplan", "schedule"):
            return service, "upload_schedule_short", "high", "upload_schedule_short"
        if has("short") and has("dokoncz planowanie", "dokonc planowanie", "zmien termin", "reschedule"):
            return service, "schedule_existing_short", "high", "schedule_existing_short"
        if has("short") and has("jest zaplanowany", "ma byc zaplanowany", "is scheduled"):
            return service, "scheduled_short_followup", "medium", "scheduled_short_followup"
    if service in ("meta_business", "meta_other"):
        if read_only:
            return service, "audit_page_readonly", "medium", "readonly_meta"
        if has("reels/create", "rolki", "rolke") and has("zalaczanie pliku", "wgrania", "reel"):
            return service, "reel_editor_workflow", "high", "reel_editor"
        if has("zaplanuj", "schedule") and has("posty", "postow", "posts", "post"):
            return service, "schedule_page_posts", "high", "schedule_page_posts"
        if has("podlacz", "polacz", "connect") and has("instagram"):
            return service, "connect_instagram", "high", "connect_instagram"
        if has("utworz strone", "create page"):
            return service, "create_page", "medium", "create_page"
        if has("developers.facebook.com", "waba", "meta for developers", "app id"):
            return service, "meta_developer_configuration", "medium", "meta_developer"
    if service == "google_analytics_gtm":
        if read_only:
            return service, "audit_analytics_gtm", "high", "readonly_ga4_gtm"
        if has("ga4", "analytics", "tag manager", "gtm", "tagmanager"):
            return service, "configure_analytics_gtm", "medium", "ga4_gtm_workflow"
    if service == "google_drive_docs" and read_only:
        return service, "read_drive_docs", "high", "readonly_drive"
    return service, "unknown", "low", "no_evidence"

def aggregate(data):
    rows = {}
    totals = collections.Counter()
    changed = []
    for run_id, item in sorted(data["runs"].items()):
        old_service = base.service_of(item)
        old_intent, _ = base.classify(item, old_service)
        service, intent, confidence, rule = classify(item)
        if old_intent != "unknown":
            assert intent == old_intent, (run_id, old_intent, intent)
        elif intent != "unknown":
            changed.append(run_id)
        key = (service, intent)
        if key not in rows:
            rows[key] = dict(service=service, intent=intent, tasks=0, calls=0,
                             uncached_tokens=0, cost_logged_usd=0.0, get_page=0,
                             run_js=0, act=0, confidence=collections.Counter(),
                             rules=collections.Counter(), examples=[])
        r = rows[key]
        r["tasks"] += 1
        m = base.metric(item)
        for k in ("calls", "uncached_tokens", "cost_logged_usd", "get_page", "run_js", "act"):
            r[k] += m[k]
        r["confidence"][confidence] += 1
        r["rules"][rule] += 1
        if len(r["examples"]) < 5:
            r["examples"].append(run_id)
        totals["runs"] += 1
        totals["calls"] += m["calls"]
        if old_intent == "unknown":
            totals["baseline_unknown_tasks"] += 1
            totals["baseline_unknown_calls"] += m["calls"]
        if intent == "unknown":
            totals["remaining_unknown_tasks"] += 1
            totals["remaining_unknown_calls"] += m["calls"]
    assert totals["runs"] == len(data["runs"])
    assert totals["calls"] == sum(int(x.get("calls") or 0) for x in data["runs"].values())
    assert totals["baseline_unknown_tasks"] - totals["remaining_unknown_tasks"] == len(changed)
    output = sorted(rows.values(), key=lambda r: (-r["calls"], -r["tasks"], r["service"], r["intent"]))
    for r in output:
        r["confidence"] = dict(r["confidence"])
        r["rules"] = dict(r["rules"])
        r["cost_logged_usd"] = round(r["cost_logged_usd"], 5)
        r["calls_per_task"] = round(r["calls"] / r["tasks"], 1)
    eligible = [r for r in output if r["service"] not in ("other", "google_other")
                and r["intent"] not in ("unknown", "scheduled_short_followup")
                and r["tasks"] >= 3]
    def candidate(r):
        return dict(service=r["service"], intent=r["intent"], tasks=r["tasks"],
                    calls=r["calls"], calls_per_task=r["calls_per_task"],
                    cost_logged_usd=r["cost_logged_usd"], confidence=r["confidence"],
                    examples=r["examples"])
    candidates = [candidate(r) for r in eligible if "high" in r["confidence"]]
    review_candidates = [candidate(r) for r in eligible if "high" not in r["confidence"]]
    remaining_unknown = sorted((dict(service=r["service"], tasks=r["tasks"], calls=r["calls"])
                                for r in output if r["intent"] == "unknown"),
                               key=lambda r: -r["calls"])
    return dict(source=SOURCE.name, base="analyze_intents_refined_20261101.py",
                method="v2 preserved; only unknown intents reclassified from task preview and observed domains",
                limitations=[
                    "Task text is truncated and sometimes contains replacement characters.",
                    "Intent is a heuristic; logged outcome is not proof of an external effect.",
                    "Calls measure historical model usage, not verified savings.",
                    "Reel editor and scheduled follow-up labels do not assert publication.",
                    "Candidate ranking is by historical model calls, not ROI or business priority."],
                totals=dict(totals), reclassified_examples=changed[:20],
                groups=output, candidates=candidates, review_candidates=review_candidates,
                remaining_unknown=remaining_unknown)

def selftest():
    cases = [
        ({"site":"Google","domains":{"studio.youtube.com":1},"task_preview":"wgraj i ZAPLANUJ publikacje Shorta"}, "youtube_studio", "upload_schedule_short"),
        ({"site":"Google","domains":{"studio.youtube.com":1},"task_preview":"TYLKO ODCZYT, nic nie zmieniaj, nie publikuj, nie wgrywaj. YouTube kanal"}, "youtube_studio", "audit_channel_readonly"),
        ({"site":"Google","domains":{"studio.youtube.com":1},"task_preview":"DOKONCZ planowanie Shorta"}, "youtube_studio", "schedule_existing_short"),
        ({"site":"Google","domains":{"studio.youtube.com":1},"task_preview":"film Short jest zaplanowany"}, "youtube_studio", "scheduled_short_followup"),
        ({"site":"Facebook/Meta","domains":{"business.facebook.com":1},"task_preview":"Otworz https://www.facebook.com/reels/create, tam zalaczanie pliku dziala"}, "meta_business", "reel_editor_workflow"),
        ({"site":"Facebook/Meta","domains":{"business.facebook.com":1},"task_preview":"Zaplanuj DWA posty strony"}, "meta_business", "schedule_page_posts"),
        ({"site":"Facebook/Meta","domains":{"business.facebook.com":1},"task_preview":"TYLKO ODCZYT, nie publikuj reels/create"}, "meta_business", "audit_page_readonly"),
        ({"site":"Facebook/Meta","domains":{"business.facebook.com":1},"task_preview":"Podlacz konto Instagram"}, "meta_business", "connect_instagram"),
        ({"site":"Facebook/Meta","domains":{"developers.facebook.com":1,"business.facebook.com":1},"task_preview":"Meta for Developers App ID"}, "meta_business", "meta_developer_configuration"),
        ({"site":"Google","domains":{"tagmanager.google.com":1},"task_preview":"GTM container and GA4 property setup"}, "google_analytics_gtm", "configure_analytics_gtm"),
        ({"site":"Google","domains":{"analytics.google.com":1},"task_preview":"TYLKO ODCZYT Google Analytics GA4"}, "google_analytics_gtm", "audit_analytics_gtm"),
        ({"site":"Google","domains":{"drive.google.com":1},"task_preview":"TYLKO ODCZYT Google Drive Docs"}, "google_drive_docs", "read_drive_docs"),
        ({"site":"LinkedIn","task_preview":"Send a message to contacts"}, "linkedin", "send_message"),
        ({"site":"Trello","task_preview":"Unclear request"}, "other", "unknown"),
    ]
    for item, expected_service, expected_intent in cases:
        actual_service, actual_intent, _, _ = classify(item)
        assert (actual_service, actual_intent) == (expected_service, expected_intent), (item, actual_service, actual_intent)
    real = json.loads(SOURCE.read_text(encoding="utf-8"))["runs"]
    real_cases = {
        "20261029_141730_f136f5": ("youtube_studio", "upload_schedule_short"),
        "20261027_114437_35beda": ("youtube_studio", "audit_channel_readonly"),
        "20261027_040813_c2029b": ("meta_business", "reel_editor_workflow"),
        "20261011_180222_999e4d": ("google_analytics_gtm", "configure_analytics_gtm"),
    }
    for run_id, expected in real_cases.items():
        assert classify(real[run_id])[:2] == expected, (run_id, classify(real[run_id]))
    print("E01_ACTIONABLE_SELFTEST_PASS", len(cases), "REAL_SAMPLES_PASS", len(real_cases))

def main():
    if "--selftest" in sys.argv:
        selftest()
        return
    data = json.loads(SOURCE.read_text(encoding="utf-8"))
    result = aggregate(data)
    OUT.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    t = result["totals"]
    lines = ["# OF-IMP E01: uzupełnienie klasyfikacji nieznanych intencji",
             "", f"Zadania: {t['runs']}, model calls: {t['calls']}.",
             f"Nieznane przed: {t['baseline_unknown_tasks']} zadań, {t['baseline_unknown_calls']} calls.",
             f"Nieznane po: {t['remaining_unknown_tasks']} zadań, {t['remaining_unknown_calls']} calls.",
             f"Rozpoznano dodatkowo: {t['baseline_unknown_tasks'] - t['remaining_unknown_tasks']} zadań.",
             "", "## Kandydaci do procedur (ranking według historycznych calls, nie ROI)",
             "", "| Serwis | Intencja | Zadania | Calls | Calls/zadanie | Koszt logowany USD |",
             "| --- | --- | ---: | ---: | ---: | ---: |"]
    for r in result["candidates"]:
        lines.append(f"| {r['service']} | {r['intent']} | {r['tasks']} | {r['calls']} | {r['calls_per_task']} | {r['cost_logged_usd']:.4f} |")
    lines.extend(["", "## Do weryfikacji: klasyfikacja średniej pewności", "",
                  "| Serwis | Intencja | Zadania | Calls |",
                  "| --- | --- | ---: | ---: |"])
    for r in result["review_candidates"]:
        lines.append(f"| {r['service']} | {r['intent']} | {r['tasks']} | {r['calls']} |")
    lines.extend(["", "## Pozostałe unknown według serwisu", "",
                  "| Serwis | Zadania | Calls |", "| --- | ---: | ---: |"])
    for r in result["remaining_unknown"]:
        lines.append(f"| {r['service']} | {r['tasks']} | {r['calls']} |")
    lines.extend(["", "## Ograniczenia", *("- " + s for s in result["limitations"]),
                  "- Wiele run może być próbami tej samej czynności biznesowej, nie liczbą niezależnych publikacji.",
                  "", "Pełne grupy, confidence, reguły i przykładowe run IDs są w JSON."])
    REPORT.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(json.dumps({"status":"E01_ACTIONABLE_PASS","totals":t,"groups":len(result["groups"]),
                      "top_candidates":result["candidates"][:8]}, ensure_ascii=False))

if __name__ == "__main__":
    main()
