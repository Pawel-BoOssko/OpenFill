"""Metrics per *whole verified task chain*, normalized to explicitly requested effects.

No time-gap guessing, no claims of delivery based on successful tool calls.
Input: OpenFill usage_*.ndjson and app_*.ndjson. Python standard library.
"""
import argparse
import json
import re
from collections import Counter, defaultdict
from pathlib import Path
from of_imp_verified_session_rollup_20261101 import report as session_report, load as load_usage

# These expressions only classify explicitly quantified requests, not website rules.
UNITS = {
    "messages": r"wiadomo(?:s|ś)ci|wiadomo(?:s|ś)(?:c|ć)|messages?",
    "invitations": r"zaprosze(?:n|ń)|zaproszenia|invitations?|connection requests?",
    "records": r"rekord(?:y|ów)|records?|entries",
}
COUNT_PATTERNS = [
    re.compile(r"\b(?:wy(?:s|ś)lij|wys(?:l|ł)a(?:c|ć)|prze(?:s|ś)lij|send|"
               r"nast(?:e|ę)pnych|kolejnych|next)\s+(\d{1,3})\s+"
               r"(?P<unit>" + "|".join(UNITS.values()) + r")\b", re.I),
    re.compile(r"\b(\d{1,3})\s+(?P<unit>" + "|".join(UNITS.values()) +
               r")\b\s*(?:do\s+wys(?:l|ł)ania|to\s+send)", re.I),
]

def requested_units(prompt):
    """Conservative; no implicit count if a number doesn't qualify a unit."""
    prompt = prompt or ""
    hits = []
    for expression in COUNT_PATTERNS:
        for match in expression.finditer(prompt):
            kind = "unknown"
            token = match.group("unit")
            for label, pattern in UNITS.items():
                if re.fullmatch(pattern, token, flags=re.I):
                    kind = label
                    break
            quantity = int(match.group(1))
            if 0 < quantity <= 500:
                hits.append((match.start(), match.end(), kind, quantity))
    unique = {}
    for start, end, kind, quantity in hits:
        unique[(start, end)] = (kind, quantity)
    # Multiple numeric counts in a single request can be genuinely additive,
    # but unless explicitly sequential, report ambiguity rather than sum.
    values = list(unique.values())
    if len(values) == 1:
        return {"unit": values[0][0], "count": values[0][1], "basis": "explicit_task_text"}
    if len(values) > 1:
        return {"unit": None, "count": None, "basis": "multiple_counts_ambiguous"}
    return {"unit": None, "count": None, "basis": "not_explicit"}

def read_tool_events(log_dir, wanted_runs):
    totals = defaultdict(Counter)
    for path in sorted(log_dir.glob("app_*.ndjson")):
        with path.open(encoding="utf-8-sig", errors="replace") as inp:
            for line in inp:
                if not any(r in line for r in wanted_runs):
                    continue
                try: row = json.loads(line)
                except ValueError: continue
                run = row.get("runId")
                if run not in wanted_runs: continue
                if row.get("source") == "tool" and row.get("eventType") == "call":
                    name = (row.get("data") or {}).get("name")
                    if name: totals[run][name] += 1
                if row.get("source") == "view_inspection":
                    evt = row.get("eventType")
                    if evt in ("navigate_wait_for_view","view_digest","initial_generic_view"):
                        totals[run]["inspection_"+evt] += 1
    return totals

def safe_rate(value, n):
    return round(value / n, 7) if n and n > 0 else None

def build(usage_dir, app_log_dir):
    chains = session_report(usage_dir)
    indexed, _ = load_usage(usage_dir)
    wanted = {r.get("runId") for r in indexed.values() if r.get("runId")}
    counts = read_tool_events(app_log_dir, wanted)
    results = []
    by_id = {k: v for k,v in indexed.items()}
    for session in chains["sessions"]:
        tasks = [by_id[k] for k in session["task_ids"] if k in by_id]
        observations = [requested_units(t.get("task","")) for t in tasks]
        known = [o for o in observations if o["count"] is not None]
        unidentified = [o for o in observations if o["count"] is None]
        same_unit = len({o["unit"] for o in known}) == 1 if known else False
        # Requested denominators are only complete if every task in chain
        # has an explicit count of the SAME effect type.
        denominator = sum(o["count"] for o in known) if same_unit and not unidentified else None
        tool_count = Counter()
        for task in tasks:
            tool_count.update(counts[task["runId"]])
        calls = session["calls"]
        cost = session["cost_usd"]
        results.append({
            "root_task_id": session["root_task_id"],
            "task_count": session["task_count"],
            "task_ids": session["task_ids"],
            "requested_effect_type": known[0]["unit"] if same_unit else None,
            "requested_effect_count": denominator,
            "requested_count_status": ("explicit_all_tasks" if denominator is not None
                                       else "unknown_or_ambiguous"),
            "verified_effect_count": None,
            "verified_effect_source": "not_available_in_usage_or_tool_success",
            "model_calls": calls,
            "total_input_tokens": session["input_tokens"],
            "cached_input_tokens": session["cached_tokens"],
            "uncached_input_tokens": session["uncached_tokens"],
            "output_tokens": session["output_tokens"],
            "cost_usd": cost,
            "tool_calls": sum(n for k,n in tool_count.items() if not k.startswith("inspection_")),
            "dom_reads": {k: tool_count[k] for k in ("get_page","read_view","view_digest","page_capture","inspect_view") if tool_count[k]},
            "navigation_calls": tool_count["navigate"],
            "action_calls": {k: tool_count[k] for k in ("act","act_many","act_locator") if tool_count[k]},
            "model_calls_per_requested_effect": safe_rate(calls,denominator),
            "cost_usd_per_requested_effect": safe_rate(cost,denominator),
            "uncached_tokens_per_requested_effect": safe_rate(session["uncached_tokens"],denominator),
            "model_calls_per_verified_effect": None,
            "cost_usd_per_verified_effect": None,
            "notes": "Requested is not verified; no verified effect count without operation evidence"
        })
    results.sort(key=lambda x: (-x["model_calls"], x["root_task_id"]))
    return {"method":"verified continuation chains + conservative explicit requested effects; no delivery inference",
            "coverage":chains["coverage"],
            "sessions":results}

def main():
    p=argparse.ArgumentParser()
    p.add_argument("usage_dir",type=Path)
    p.add_argument("app_log_dir",type=Path)
    p.add_argument("--output",type=Path)
    args=p.parse_args()
    result=build(args.usage_dir,args.app_log_dir)
    if args.output:
        args.output.parent.mkdir(parents=True,exist_ok=True)
        args.output.write_text(json.dumps(result,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
    print(json.dumps({"coverage":result["coverage"],
                      "top_sessions":result["sessions"][:5]},ensure_ascii=False))

if __name__=="__main__": main()
