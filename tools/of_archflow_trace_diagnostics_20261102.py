# OpenFill: offline diagnosis of actual Responses API tool sequences.
# Scope: Stage14 reviewed non-Airbnb run IDs. No mutation of usage or history.
import collections
import json
import pathlib
import statistics

ROOT = pathlib.Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs"
LOGS = pathlib.Path(r"C:\Users\pboru\OpenFill\data\stable\logs")
stage = json.loads((DOCS / "OPENFILL_COST_STAGE14_RECONCILED_20261102.json").read_text(encoding="utf-8"))
run_to_op = {}
for op in stage["operations"]:
    for rid in op["run_ids"]:
        if rid in run_to_op:
            raise AssertionError(f"run appears in multiple operations: {rid}")
        run_to_op[rid] = op["operation"]

by_run = collections.defaultdict(list)
for path in [LOGS / "usage_202610.ndjson", LOGS / "usage_202611.ndjson"]:
    with path.open(encoding="utf-8") as f:
        for line in f:
            if not line.strip():
                continue
            item = json.loads(line)
            if item.get("kind") != "call":
                continue
            rid = item.get("runId")
            if rid in run_to_op:
                by_run[rid].append(item)

all_calls = sum(map(len, by_run.values()))
assert len(run_to_op) == stage["summary"]["current_units"] + sum(
    len(op["run_ids"]) - 1 for op in stage["operations"])
assert all_calls == stage["summary"]["total_actual_calls"], (
    all_calls, stage["summary"]["total_actual_calls"])
assert set(by_run) == set(run_to_op), ("missing", len(set(run_to_op) - set(by_run)))

tools = collections.Counter()
monthly_tools = collections.defaultdict(collections.Counter)
transition = collections.Counter()
read_after_act = 0
action_calls_with_successor = 0
multi_tool_calls = 0
first_tools = collections.Counter()
per_op_calls = collections.Counter()
by_service = collections.defaultdict(lambda: collections.Counter())
service_for_op = {op["operation"]: op["service"] for op in stage["operations"]}
for rid, rows in by_run.items():
    rows.sort(key=lambda r: (r.get("step", 0), r.get("ts", "")))
    opid = run_to_op[rid]
    per_op_calls[opid] += len(rows)
    by_service[service_for_op[opid]]["calls"] += len(rows)
    if rows:
        first_tools[tuple(rows[0].get("tools") or [])] += 1
    for row in rows:
        names = row.get("tools") or []
        tools.update(names)
        monthly_tools[str(row.get("ts", ""))[:7]].update(names)
        multi_tool_calls += len(names) > 1
    for a, b in zip(rows, rows[1:]):
        a_names, b_names = a.get("tools") or [], b.get("tools") or []
        if len(a_names) == len(b_names) == 1:
            transition[(a_names[0], b_names[0])] += 1
        if a_names == ["act"]:
            action_calls_with_successor += 1
            read_after_act += b_names == ["get_page"]

def quantile(items, p):
    ordered = sorted(items)
    return ordered[int((len(ordered) - 1) * p)] if ordered else None

payloads = [r for rows in by_run.values() for r in rows]
ops_values = list(per_op_calls.values())
out = {
    "source": "usage_202610.ndjson + usage_202611.ndjson, filtered by stage14 reviewed run IDs",
    "scope": "non-Airbnb, includes all measured retries/continuations joined in stage14",
    "runs": len(by_run),
    "business_operations": len(per_op_calls),
    "model_calls": all_calls,
    "tool_invocations": sum(tools.values()),
    "model_calls_with_multiple_function_calls": multi_tool_calls,
    "tools": dict(tools.most_common()),
    "monthly_tools": {k: dict(v.most_common()) for k, v in sorted(monthly_tools.items())},
    "single_tool_transitions": [{"from": a, "to": b, "count": n}
                                for (a, b), n in transition.most_common(35)],
    "action_calls_with_next_model_call": action_calls_with_successor,
    "act_then_get_page": read_after_act,
    "act_then_get_page_pct_of_act_with_successor": round(
        100 * read_after_act / action_calls_with_successor, 2) if action_calls_with_successor else None,
    "first_tool_sequences": [{"tools": list(k), "runs": v}
                             for k, v in first_tools.most_common(20)],
    "calls_per_operation": {"p50": quantile(ops_values, .5), "p90": quantile(ops_values, .9),
                            "p95": quantile(ops_values, .95), "max": max(ops_values)},
    "request_chars": {"p50": quantile([x.get("requestChars", 0) for x in payloads], .5),
                      "p95": quantile([x.get("requestChars", 0) for x in payloads], .95)},
    "input_tokens": {"p50": quantile([x.get("inputTokens", 0) for x in payloads], .5),
                     "p95": quantile([x.get("inputTokens", 0) for x in payloads], .95)},
    "by_service": {k: dict(v) for k, v in sorted(by_service.items())},
    "limits": [
        "One function_call is not automatically one model call; transitions count only single-tool responses.",
        "A repeated get_page may reflect a changed page, not necessarily redundant work.",
        "Data spans historical versions of OpenFill and cannot measure current LIVE after code changes.",
        "No business outcomes or delivery confirmations inferred from usage records."
    ]
}
(DOCS / "OPENFILL_ARCHFLOW_TRACE_DIAGNOSTICS_20261102.json").write_text(
    json.dumps(out, ensure_ascii=False, indent=2), encoding="utf-8")
lines = [
    "# OpenFill: architektura, ślady rzeczywistych wywołań, 2026-11-02",
    "",
    f"- Zakres: {out['business_operations']} operacji biznesowych, {out['runs']} runId, "
    f"{all_calls} model calls, bez Airbnb.",
    f"- Funkcje: {out['tool_invocations']} wywołań narzędzi w odpowiedziach modelu; "
    f"{multi_tool_calls} odpowiedzi z więcej niż jednym function_call.",
    f"- act -> get_page: {read_after_act} z {action_calls_with_successor} wywołań act "
    f"mających następną odpowiedź modelu ({out['act_then_get_page_pct_of_act_with_successor']}%). "
    "To nie jest dowód zbędnego odczytu.",
    f"- Model calls na operację: mediana {out['calls_per_operation']['p50']}, "
    f"p90 {out['calls_per_operation']['p90']}, p95 {out['calls_per_operation']['p95']}.",
    f"- requestChars p50 {out['request_chars']['p50']}, p95 {out['request_chars']['p95']}.",
    "",
    "## Najczęstsze wywołania narzędzi",
    "",
    "| Narzędzie | Liczba |",
    "|---|---:|",
]
lines += [f"| {k} | {v} |" for k, v in tools.most_common(16)]
lines += ["", "## Najczęstsze przejścia między kolejnymi wywołaniami modelu",
          "", "| Poprzednie | Następne | Liczba |", "|---|---|---:|"]
lines += [f"| {x['from']} | {x['to']} | {x['count']} |" for x in out["single_tool_transitions"][:18]]
lines += ["", "## Granice interpretacji", ""]
lines += [f"- {x}" for x in out["limits"]]
(DOCS / "OPENFILL_ARCHFLOW_TRACE_DIAGNOSTICS_20261102.md").write_text(
    "\n".join(lines) + "\n", encoding="utf-8")
print("ARCHFLOW_TRACE_OK", json.dumps({
    k: out[k] for k in ["business_operations", "runs", "model_calls",
                       "tool_invocations", "act_then_get_page",
                       "act_then_get_page_pct_of_act_with_successor", "calls_per_operation"]
}, ensure_ascii=False))
