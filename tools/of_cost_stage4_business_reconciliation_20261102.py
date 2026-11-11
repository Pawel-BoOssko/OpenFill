"""OpenFill stage 4: conservative business-operation reconciliation, read-only."""
import json, pathlib, collections, datetime, hashlib, re
P=pathlib.Path(__file__).resolve().parents[1]/"docs"
H=pathlib.Path(r"C:\Users\pboru\OpenFill\data\stable\history\tasks.ndjson")
LOG=pathlib.Path(r"C:\Users\pboru\OpenFill\data\stable\logs")
stage1=json.loads((P/"OPENFILL_COST_STAGE1_ACTIVITIES_20261102.json").read_text(encoding="utf8"))
stage2=json.loads((P/"OPENFILL_COST_STAGE2_REASONABLE_BASELINE_20261102.json").read_text(encoding="utf8"))
old=json.loads((P/"OPENFILL_TOKEN_AUDIT_DATA_20261031.json").read_text(encoding="utf8"))["runs"]
audit=json.loads((P/"OPENFILL_CONTINUATION_LINKS_20261102.json").read_text(encoding="utf8"))
history={}
for line in H.open(encoding="utf8",errors="replace"):
    try: r=json.loads(line)
    except ValueError: continue
    if isinstance(r,dict) and r.get("id"): history[r["id"]]=r
# Read actual usage only AFTER frozen stage2. Choose task totals; do not sum call and task records.
ledger={}
for path in (LOG/"usage_202610.ndjson",LOG/"usage_202611.ndjson"):
    for line in path.open(encoding="utf8",errors="replace"):
        try: r=json.loads(line)
        except ValueError: continue
        if r.get("kind")=="task" and r.get("runId"): ledger[r["runId"]]=r
actual={}
for rid,act in stage1["runs"].items():
    if act.get("service")=="airbnb": continue
    obs=old.get(rid) or ledger.get(rid)
    if obs is not None and int(obs.get("calls") or 0)>0:
        actual[rid]={"calls":int(obs["calls"]),"usd":float(obs.get("costUsd") or 0),
            "input":int(obs.get("inputTokens") or 0),"cached":int(obs.get("cachedTokens") or 0),
            "source":"indexed_october" if rid in old else "usage_task"}
# Confirm only exact-text repeats previously surfaced for review, prior not successful, within 20 minutes.
edges=[]
def dt(s):return datetime.datetime.fromisoformat(s.replace("Z","+00:00"))
def normal(s):return re.sub(r"\s+"," ",str(s or "")).strip().casefold()
for item in audit["uncertainCandidateLinks"]:
    child=history.get(item["child"])
    if child is None: continue
    for pid in item["possible_parents"]:
        par=history.get(pid)
        if par is None or par.get("status") not in ("running","partial","stopped"): continue
        gap=(dt(child["startedUtc"])-dt(par["startedUtc"])).total_seconds()/60
        if 0<gap<=20 and normal(child.get("task"))==normal(par.get("task")):
            edges.append({"parent":pid,"child":child["id"],"gap_minutes":round(gap,1),
                          "evidence":"exact_full_task_text+prior_non_success+within_20m"})
parent={k:k for k in history}
def find(x):
    while parent[x]!=x:
        parent[x]=parent[parent[x]]
        x=parent[x]
    return x
for e in edges:
    parent[find(e["child"])]=find(e["parent"])
groups=collections.defaultdict(list)
for tid in history: groups[find(tid)].append(tid)
chains=[tids for tids in groups.values() if len(tids)>1]
run_to_chain={}
for tids in chains:
    canonical=sorted(tids)[0]
    for tid in tids:
        rid=history[tid].get("runId")
        if rid:run_to_chain[rid]=canonical
members=collections.defaultdict(list)
for rid,row in stage1["runs"].items():
    if row.get("service")!="airbnb":members[run_to_chain.get(rid,rid)].append(rid)
rows=[]
for key,rids in members.items():
    measured=[actual[r] for r in rids if r in actual]
    norms=[stage2["runs"].get(r,{}) for r in rids]
    missing=[r for r in rids if r not in actual]
    history_ids=next((t for t in chains if sorted(t)[0]==key),[])
    missing_history=[tid for tid in history_ids if not history[tid].get("runId") or history[tid].get("runId") not in actual]
    intents={(stage1["runs"][r]["service"],stage1["runs"][r]["intent"]) for r in rids}
    comparable=(not missing and not missing_history and len(intents)==1
       and all(n.get("status")=="estimated" for n in norms))
    ref=max(n["baseline_calls"] for n in norms) if comparable else None
    site,intent=sorted(intents)[0] if len(intents)==1 else ("mixed","mixed")
    calls=sum(m["calls"] for m in measured); usd=sum(m["usd"] for m in measured)
    rows.append({"operation_key":key,"service":site,"intent":intent,"run_ids":rids,
      "linked_task_ids":history_ids,"run_count":len(rids),"status":"confirmed_exact_repeat" if len(rids)>1 or history_ids else "unlinked_run",
      "actual_calls":calls,"actual_usd":round(usd,8),"reference_calls":ref,
      "excess_calls":calls-ref if ref is not None else None,
      "missing_usage_run_ids":missing,"missing_history_task_ids":missing_history,
      "measured":len(measured),"eligible_for_reference":comparable})
types=collections.defaultdict(list)
for r in rows:types[(r["service"],r["intent"])].append(r)
type_rows=[]
for (site,intent),entries in types.items():
    a=sum(x["actual_calls"] for x in entries);usd=sum(x["actual_usd"] for x in entries)
    refs=[x for x in entries if x["eligible_for_reference"]]
    ca=sum(x["actual_calls"] for x in refs); cb=sum(x["reference_calls"] for x in refs)
    type_rows.append({"service":site,"intent":intent,"operations_or_unlinked_runs":len(entries),
       "measured_calls":a,"measured_usd":round(usd,6),
       "comparable_operations":len(refs),"comparable_actual_calls":ca,
       "comparable_reference_calls":cb,"comparable_excess_calls":ca-cb})
type_rows.sort(key=lambda x:-x["comparable_excess_calls"])
comparable=[x for x in rows if x["eligible_for_reference"]]
all_measured=sum(r["actual_calls"] for r in rows)
comp_calls=sum(r["actual_calls"] for r in comparable)
ref_calls=sum(r["reference_calls"] for r in comparable)
report={"stage":4,"source_sequence":"stage1 activities -> stage2 frozen baseline -> stage3 logged costs -> stage4 operation joining",
 "history_records":len(history),"raw_candidate_links":len(audit["uncertainCandidateLinks"]),
 "confirmed_exact_repeat_edges":edges,"confirmed_exact_repeat_chains":chains,
 "unresolved_continuation_candidates":len(audit["uncertainCandidateLinks"])-len({e["child"] for e in edges}),
 "total_measured_runs":len(actual),"measured_calls_all_non_airbnb":all_measured,
 "measured_cost_usd_all_non_airbnb":round(sum(r["actual_usd"] for r in rows),6),
 "compared_operations_or_unlinked_runs":len(comparable),
 "comparable_actual_calls":comp_calls,"comparable_reference_calls":ref_calls,
 "hypothetical_call_reduction_pct":round(100*(comp_calls-ref_calls)/comp_calls,1) if comp_calls else None,
 "unknown_or_incomplete_operations":sum(not r["eligible_for_reference"] for r in rows),
 "type_rows":type_rows,"operations":rows,
 "limits":["Repeated prompts only joined for reviewed candidates with exact entire task text and <=20m interval.",
 "A repeated business request can still have changed scope; review before declaring final economic savings.",
 "No normative baseline where original attempt lacks observed usage or is unknown-classified.",
 "Dollar budgets are assumptions, not provider tariff.","No deletion/mutation of original histories or billing logs.",
 "No Airbnb development or evaluation included."]}
out=P/"OPENFILL_COST_BUSINESS_RECONCILED_20261102.json"
out.write_text(json.dumps(report,ensure_ascii=False,indent=1),encoding="utf8")
lines=["# OpenFill: rozliczenie prób względem całych czynności, 2026-11-02","",
 f"- Potwierdzone łańcuchy powtórzeń: {len(chains)}; krawędzie: {len(edges)}.",
 f"- Nierozstrzygnięte kandydatury na wznowienie: {report['unresolved_continuation_candidates']}.",
 f"- Rzeczywiste wywołania (wszystkie rozpoznane przebiegi poza Airbnb): {all_measured}.",
 f"- Rzeczywisty koszt tych przebiegów: {report['measured_cost_usd_all_non_airbnb']:.4f} USD.",
 f"- Wywołania porównywalne: {comp_calls}; referencyjne: {ref_calls}.","",
 "**Wyniki są szacowaniem potencjału, nie osiągniętym wynikiem finansowym.**","",
 "| Serwis | Czynność | Przebiegi/operacje | Calls realne | Calls porównywalne | Wzorzec | USD realne |",
 "|---|---|---:|---:|---:|---:|---:|"]
for r in type_rows:
    lines.append(f"| {r['service']} | {r['intent']} | {r['operations_or_unlinked_runs']} | {r['measured_calls']} | {r['comparable_actual_calls']} | {r['comparable_reference_calls']} | {r['measured_usd']:.4f} |")
lines+=["","## Potwierdzone próby",""]
for g in chains:
    lines.append("- "+", ".join(g))
lines+=["","## Ograniczenia",""]+["- "+x for x in report["limits"]]
(P/"OPENFILL_COST_BUSINESS_RECONCILED_20261102.md").write_text("\n".join(lines)+"\n",encoding="utf8")
print("STAGE4_DONE",json.dumps({k:report[k] for k in ("confirmed_exact_repeat_chains","unresolved_continuation_candidates","total_measured_runs","measured_calls_all_non_airbnb","measured_cost_usd_all_non_airbnb","compared_operations_or_unlinked_runs","comparable_actual_calls","comparable_reference_calls")},ensure_ascii=False))

