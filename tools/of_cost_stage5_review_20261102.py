"""Stage5: reviewed business continuations; no browsing, mutations or usage price fitting."""
import collections, csv, datetime, json, pathlib
P=pathlib.Path(__file__).resolve().parents[1]/"docs"
R=json.loads((P/"OPENFILL_COST_BUSINESS_RECONCILED_20261102.json").read_text(encoding="utf8"))
A=json.loads((P/"OPENFILL_CONTINUATION_LINKS_20261102.json").read_text(encoding="utf8"))
H=pathlib.Path(r"C:\Users\pboru\OpenFill\data\stable\history\tasks.ndjson")
hist={}
for line in H.open(encoding="utf8",errors="replace"):
    try: r=json.loads(line)
    except ValueError: continue
    if isinstance(r,dict) and r.get("id"): hist[r["id"]]=r
# Reviewed against the complete original instructions on 2026-11-02.
decisions=[
 ("H20261011-195844-968a","H20261011-195049-7b26","merge",
  "Bing Webmaster Tools, same verified domain, same GSC import and sitemap; child explicitly identifies prior attempt"),
 ("H20261011-201315-8ab2","H20261011-153302-d9f0","merge",
  "Facebook Business Suite, child explicitly continues unfinished scheduling of post 1, then post 2, from parent's four-post batch; scope narrowed"),
 ("H20261011-202826-4919","H20261011-200222-bb22","merge",
  "GA4/GTM for identical company domain; child corrects account selection after previous unsuccessful configuration attempt; scope revised"),
 ("H20261011-223539-2622","H20261011-223411-64bf","merge",
  "LinkedIn company-page read-only audit, identical company and inspection fields, child clarifies permitted login identity"),
 ("H20261019-204249-5aef","H20261019-204000-cc32","merge",
  "WhatsApp list read-only; child explicitly continues after first 18 items already gathered by parent; alternative scrolling technique"),
 ("H20261012-202137-5e03","H20261012-201938-8511","separate",
  "Facebook missing-post incident is a new diagnostic goal, not prior status check of published post from a different date"),
 ("H20261022-141844-4018","H20261022-140141-579a","separate",
  "LinkedIn Featured and Skills editing is distinct from earlier About editing and inbox review")
]
expected={v["child"] for v in A["uncertainCandidateLinks"]} - {x["child"] for x in R["confirmed_exact_repeat_edges"]}
assert len(decisions)==len(expected)==7 and {d[0] for d in decisions}==expected
for child,par,decision,_ in decisions:
    assert child in hist and par in hist, (child,par)
    assert hist[child]["startedUtc"]>hist[par]["startedUtc"], (child,par)
# Map existing stage4 operational units and link reviewed tasks to those units.
ops={r["operation_key"]:r for r in R["operations"]}
parent={k:k for k in ops}
rid2key={rid:key for key,op in ops.items() for rid in op["run_ids"]}
id2key={tid:key for key,op in ops.items() for tid in op["linked_task_ids"]}
def key_for_task(tid):
    rid=hist[tid].get("runId")
    return id2key.get(tid) or rid2key.get(rid)
def root(k):
    while parent[k]!=k:
        parent[k]=parent[parent[k]]
        k=parent[k]
    return k
merged_edges=[];excluded=[]
for child,par,verdict,evidence in decisions:
    ck=key_for_task(child);pk=key_for_task(par)
    row={"child":child,"parent":par,"decision":verdict,"evidence":evidence,
        "child_run_id":hist[child].get("runId"),"parent_run_id":hist[par].get("runId"),
        "parent_key":pk,"child_key":ck}
    if verdict=="merge" and ck and pk and ck!=pk:
        parent[root(ck)]=root(pk);merged_edges.append(row)
    elif verdict=="merge":
        row["review_required"]="Missing run in measured inventory, cannot silently attach"
        excluded.append(row)
    else:excluded.append(row)
groups=collections.defaultdict(list)
for key,op in ops.items():groups[root(key)].append(op)
merged_rows=[];compare=[]
for key,parts in groups.items():
    allrun=sorted(set(r for op in parts for r in op["run_ids"]))
    alltask=sorted(set(t for op in parts for t in op["linked_task_ids"]))
    for edge in merged_edges:
        if root(edge["child_key"])==key:
            alltask=sorted(set(alltask+[edge["child"],edge["parent"]]))
    statuses=sorted(set(s for op in parts for s in [op["status"]]))
    is_joined=len(parts)>1
    cost=round(sum(op["actual_usd"] for op in parts),8)
    calls=sum(op["actual_calls"] for op in parts)
    missing=sorted(set(r for op in parts for r in op["missing_usage_run_ids"]))
    missing_history=sorted(set(t for op in parts for t in op["missing_history_task_ids"]))
    # In cases with newly linked historical task but absent usage, retain missing marker.
    for tid in alltask:
        rid=hist.get(tid,{}).get("runId")
        if not rid or rid not in allrun: missing_history.append(tid)
    missing_history=sorted(set(missing_history))
    original_ok=(len(parts)==1 and parts[0]["eligible_for_reference"])
    ok=original_ok and not is_joined and not missing and not missing_history
    ref=parts[0]["reference_calls"] if ok else None
    site_set={op["service"] for op in parts};intent_set={op["intent"] for op in parts}
    merged_rows.append({"operation_key":key,"service":next(iter(site_set)) if len(site_set)==1 else "mixed",
      "intent":next(iter(intent_set)) if len(intent_set)==1 else "mixed",
      "source_units":len(parts),"run_ids":allrun,"task_ids":alltask,
      "actual_calls":calls,"actual_cost_usd":cost,
      "reference_calls":ref,"excess_calls":calls-ref if ref is not None else None,
      "missing_usage":missing,"missing_history":missing_history,
      "comparison_status":"comparable" if ok else ("reconciled_no_frozen_whole_task_baseline" if is_joined else "not_comparable"),
      "proven_continuation":is_joined or bool(alltask),"baseline_source":"unchanged_frozen_stage2" if ok else None})
comp=[r for r in merged_rows if r["comparison_status"]=="comparable"]
summary={"original_stage4_units":len(ops),"new_stage5_units":len(merged_rows),
         "prior_exact_chains":len(R["confirmed_exact_repeat_chains"]),
         "new_vetted_continuations":len(merged_edges),"explicitly_separate":sum(d[2]=="separate" for d in decisions),
         "unmerged_for_missing_evidence":sum(bool(r.get("review_required")) for r in excluded),
         "actual_calls":sum(r["actual_calls"] for r in merged_rows),
         "actual_cost_usd":round(sum(r["actual_cost_usd"] for r in merged_rows),6),
         "comparable_units":len(comp),"comparable_actual_calls":sum(r["actual_calls"] for r in comp),
         "comparable_reference_calls":sum(r["reference_calls"] for r in comp)}
assert summary["actual_calls"]==R["measured_calls_all_non_airbnb"]
assert abs(summary["actual_cost_usd"]-R["measured_cost_usd_all_non_airbnb"])<0.000001
result={"stage":5,"updated_on":"2026-11-02","summary":summary,"reviewed":decisions,
        "merged_edges":merged_edges,"separate_or_unresolved":excluded,
        "operations":merged_rows,
        "limitations":["Reviewed only 7 previously flagged ambiguous continuation candidates; this does not establish all possible continuations in the full history.",
         "Newly joined operations are excluded from quantitative savings until a whole-business-operation reference budget is independently estimated and prior usage is complete.",
         "Count and USD are actual measured OpenFill usage for the indexed runs, not proof of completed external work.",
         "No Airbnb or LIVE changes."]}
(P/"OPENFILL_COST_STAGE5_REVIEWED_OPERATIONS_20261102.json").write_text(json.dumps(result,ensure_ascii=False,indent=1),encoding="utf8")
with (P/"OPENFILL_COST_STAGE5_REVIEWED_OPERATIONS_20261102.csv").open("w",encoding="utf-8-sig",newline="") as f:
    fields=("operation_key","service","intent","source_units","actual_calls","actual_cost_usd","reference_calls","excess_calls","comparison_status","run_ids","task_ids","missing_usage","missing_history")
    w=csv.DictWriter(f,fieldnames=fields);w.writeheader()
    for op in merged_rows:
        w.writerow({k:";".join(str(x) for x in op[k]) if isinstance(op.get(k),list) else op.get(k) for k in fields})
md=["# OpenFill: etap 5, zweryfikowane kontynuacje, 2026-11-02","",
    "Wykaz 7 przypadków ręcznie zweryfikowano na podstawie pełnych poleceń. Wykluczono Airbnb.",
    "", "## Rozliczenie po scaleniu", ""]
for k,v in summary.items():md.append(f"- {k}: {v}")
md.extend(["","## Potwierdzone kontynuacje",""])
for r in merged_edges:md.append(f"- {r['parent']} -> {r['child']}: {r['evidence']}")
md.extend(["","## Oddzielone przypadki",""])
for r in excluded:md.append(f"- {r['parent']} / {r['child']}: {r['evidence']}")
md.extend(["","## Ograniczenia",""])
md.extend("- "+x for x in result["limitations"])
(P/"OPENFILL_COST_STAGE5_REVIEWED_OPERATIONS_20261102.md").write_text("\n".join(md)+"\n",encoding="utf8")
print("STAGE5_OK",json.dumps(summary,ensure_ascii=False))

