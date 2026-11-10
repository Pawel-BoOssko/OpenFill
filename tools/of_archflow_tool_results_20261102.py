import collections
import json
import pathlib

root = pathlib.Path(__file__).resolve().parents[1]
stage = json.loads((root / "docs" / "OPENFILL_COST_STAGE14_RECONCILED_20261102.json").read_text(encoding="utf-8"))
ids = {rid for op in stage["operations"] for rid in op["run_ids"]}
runs = pathlib.Path(r"C:\Users\pboru\OpenFill\data\stable\logs\runs")
counts = collections.Counter()
errors = collections.Counter()
error_classes = collections.Counter()
missing = 0
for rid in ids:
    path = runs / ("run_" + rid + ".ndjson")
    if not path.exists():
        missing += 1
        continue
    with path.open(encoding="utf-8") as f:
        for line in f:
            try:
                event = json.loads(line)
            except json.JSONDecodeError:
                continue
            if event.get("source") != "tool" or event.get("eventType") != "result":
                continue
            data = event.get("data") or {}
            name = data.get("name")
            if name not in ("act_many", "act_locator", "view_digest", "verify_view"):
                continue
            status = event.get("status", "unknown")
            counts[(name, status)] += 1
            if status != "ok":
                preview = str(data.get("preview") or "")
                errors[(name, preview[:100])] += 1
                lower = preview.lower()
                reason = ("unknown_action" if "unknown_action" in lower else
                          "illegal_invocation" if "illegal invocation" in lower else
                          "not_found" if "not_found" in lower else
                          "canceled" if "cancel" in lower else
                          "semantic_batch_stopped" if '"status":"stopped"' in lower else
                          "other")
                error_classes[(name, reason)] += 1
report = {
    "run_ids": len(ids),
    "missing_run_log_files": missing,
    "counts": [{"tool": n, "status": s, "count": v}
               for (n, s), v in sorted(counts.items())],
    "error_classes": [{"tool": n, "category": c, "count": v}
                      for (n, c), v in error_classes.most_common()],
    "errors": [{"tool": n, "preview_first_100": p, "count": v}
               for (n, p), v in errors.most_common(20)],
    "note": "Historical tool result status is not proof of business effect; tool error previews may be truncated."
}
(root / "docs" / "OPENFILL_ARCHFLOW_TOOL_RESULTS_20261102.json").write_text(
    json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print("ARCHFLOW_TOOL_RESULTS_OK", len(ids), "missing", missing, "counts", dict(counts),
      "error_classes", dict(error_classes))
