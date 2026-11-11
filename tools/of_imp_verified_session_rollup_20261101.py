"""Aggregate REAL OpenFill continuation chains from taskKey/continuedFrom usage rows.

Do not infer continuity from time gaps, domain names or similar task previews.
Unmatched parent IDs remain explicitly unresolved. Python stdlib only.
"""
import argparse
import json
from collections import defaultdict
from pathlib import Path


def load(root):
    rows = {}
    missing_id = 0
    malformed = 0
    duplicates = 0
    files = sorted(root.glob("usage_*.ndjson"))
    for file in files:
        with file.open("r", encoding="utf-8-sig", errors="replace") as inp:
            for line in inp:
                try:
                    row = json.loads(line)
                except (ValueError, TypeError):
                    malformed += 1
                    continue
                if row.get("kind") != "task":
                    continue
                key = row.get("taskKey")
                if not key:
                    missing_id += 1
                    continue
                if key in rows:
                    duplicates += 1
                    if str(row.get("ts", "")) <= str(rows[key].get("ts", "")):
                        continue
                rows[key] = row
    return rows, dict(files_scanned=len(files), without_task_id=missing_id,
                      malformed_lines=malformed, duplicate_task_ids=duplicates)


def chain(key, rows):
    visited = set()
    root = key
    reason = "root"
    depth = 0
    scope = rows[key].get("requesterChat") or ""
    while root in rows:
        if root in visited:
            return key, "cycle", depth
        visited.add(root)
        parent = rows[root].get("continuedFrom")
        if not parent:
            return root, reason, depth
        if parent not in rows:
            return root, "parent_not_in_usage", depth
        parent_scope = rows[parent].get("requesterChat") or ""
        if scope and parent_scope and scope != parent_scope:
            return root, "requester_chat_mismatch", depth
        root = parent
        depth += 1
        reason = "verified_continuation"
    return key, "missing_record", depth


def report(root):
    rows, coverage = load(root)
    groups = defaultdict(list)
    reasons = defaultdict(int)
    verified_edges = 0
    for key in rows:
        root_id, reason, depth = chain(key, rows)
        groups[(root_id, rows[key].get("requesterChat") or "")].append(key)
        reasons[reason] += 1
        if depth:
            verified_edges += 1
    result = []
    for (root_id, caller), keys in groups.items():
        entries = [rows[k] for k in keys]
        def summed(field):
            return sum(float(x.get(field) or 0) for x in entries)
        unresolved = any(rows[k].get("continuedFrom") and
                         rows[k]["continuedFrom"] not in rows for k in keys)
        result.append({
            "root_task_id": root_id,
            "requester_chat": caller,
            "task_ids": sorted(keys),
            "task_count": len(keys),
            "calls": int(summed("calls")),
            "input_tokens": int(summed("inputTokens")),
            "cached_tokens": int(summed("cachedTokens")),
            "uncached_tokens": int(summed("inputTokens")-summed("cachedTokens")),
            "output_tokens": int(summed("outputTokens")),
            "cost_usd": round(summed("costUsd"), 8),
            "unresolved_ancestor": unresolved,
        })
    result.sort(key=lambda x: (-x["calls"], -x["cost_usd"], x["root_task_id"]))
    return {
        "method": "taskKey/continuedFrom graph only; no chronological grouping",
        "source": str(root),
        "coverage": {**coverage, "identified_tasks": len(rows),
                     "verified_parent_links": verified_edges,
                     "chain_statuses": dict(reasons)},
        "sessions": result,
    }


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("usage_dir", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    payload = report(args.usage_dir)
    output = json.dumps(payload, indent=2, ensure_ascii=False)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(output + "\n", encoding="utf-8")
    print(json.dumps({"coverage": payload["coverage"],
                      "top_sessions": payload["sessions"][:5]}, ensure_ascii=False))
