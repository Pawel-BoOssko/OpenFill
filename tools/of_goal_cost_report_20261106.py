"""Aggregate OpenFill tokens by end-user business goal and verified action.
Reads all monthly usage_*.ndjson files. No model calls, no web calls.
For old runs, use historical retrospective separately. This report is for
the new business_goal_* event protocol and its stable goalId.
"""
import argparse, csv, json, pathlib, collections, datetime

DEFAULT_LOGS=pathlib.Path(r"C:\Users\pboru\OpenFill\data\stable\logs")
DEFAULT_OUTPUT=pathlib.Path(__file__).resolve().parents[1]/"docs"/"OPENFILL_GOAL_COST_CURRENT.json"

def records(log_dir):
    for file in sorted(log_dir.glob("usage_*.ndjson")):
        with file.open("r",encoding="utf-8",errors="replace") as handle:
            for line in handle:
                try: yield json.loads(line)
                except json.JSONDecodeError: continue

def aggregate(log_dir):
    goal_by_run={}
    starts={}
    finishes=collections.defaultdict(dict)
    effects=collections.defaultdict(dict)
    calls=collections.defaultdict(dict)
    for row in records(log_dir):
        run=row.get("runId")
        kind=row.get("kind")
        if kind=="business_goal_start" and run:
            goal=row.get("goalId")
            if goal:
                goal_by_run[run]=goal
                starts[run]=row
        elif kind=="business_goal_finish" and run:
            goal=row.get("goalId")
            if goal:
                goal_by_run[run]=goal
                finishes[goal][run]=row
        elif kind=="business_goal_effect" and run:
            goal=row.get("goalId")
            key=row.get("actionKey")
            if goal and key:
                effects[goal][key]=row
        elif kind=="call" and run:
            calls[run][row.get("step")]=row
    by_goal=collections.defaultdict(list)
    for run,goal in goal_by_run.items():by_goal[goal].append(run)
    result=[]
    for goal,runs in by_goal.items():
        runs=sorted(set(runs))
        starts_for=[starts[r] for r in runs if r in starts]
        finish_for=[finishes[goal][r] for r in runs if r in finishes[goal]]
        category=next((r.get("goalType") for r in starts_for if r.get("goalType")), "other")
        units=next((r.get("requestedUnits") for r in starts_for if r.get("requestedUnits") is not None),None)
        label=next((r.get("unitName") for r in starts_for if r.get("unitName")),"result")
        title=next((r.get("goalText") for r in starts_for if r.get("goalText")),"")
        input_tokens=cache_tokens=output_tokens=reasoning_tokens=0
        usd=0.0; model_calls=0
        for run in runs:
            f=finishes[goal].get(run)
            if f:
                input_tokens+=f.get("inputTokens") or 0
                cache_tokens+=f.get("cachedTokens") or 0
                output_tokens+=f.get("outputTokens") or 0
                reasoning_tokens+=f.get("reasoningTokens") or 0
                usd+=f.get("costUsd") or 0
                model_calls+=f.get("calls") or 0
            else:
                for c in calls.get(run,{}).values():
                    input_tokens+=c.get("inputTokens") or 0
                    cache_tokens+=c.get("cachedTokens") or 0
                    output_tokens+=c.get("outputTokens") or 0
                    reasoning_tokens+=c.get("reasoningTokens") or 0
                    usd+=c.get("costUsd") or 0
                    model_calls+=1
        unique_effects=list(effects.get(goal,{}).values())
        batch_units=sum((e.get("units") or 0) for e in unique_effects if e.get("evidence")=="provider_batch_ui_verified")
        recipient_units=sum((e.get("units") or 0) for e in unique_effects if e.get("evidence")=="recipient_verified_in_current_ui")
        # Batch and recipient evidence may refer to the same sends.
        provider_units=max(batch_units,recipient_units)
        # A continuation can repeat the summary of earlier attempts. Never sum claims.
        claimed=max((f.get("agentReportedUnits") or 0 for f in finish_for),default=0)
        read_units=max((f.get("readCompletedUnits") or 0 for f in finish_for),default=0)
        confirmed=provider_units if provider_units>0 else read_units if read_units>0 else claimed
        evidence="provider_ui" if provider_units else "success_and_live_read" if read_units else "agent_summary" if claimed else "none"
        total=input_tokens+output_tokens
        last=sorted(finish_for,key=lambda f:f.get("ts",""))[-1] if finish_for else {}
        result.append({
            "goal_id":goal,"goal_type":category,"unit_name":label,
            "goal_text":title,"requested_units":units,
            "runs":len(runs),"run_ids":runs,"status":last.get("outcome","running_or_interrupted"),
            "model_calls":model_calls,"input_tokens":input_tokens,
            "cached_tokens":cache_tokens,"uncached_input_tokens":input_tokens-cache_tokens,
            "output_tokens":output_tokens,"reasoning_tokens_in_output":reasoning_tokens,
            "total_tokens":total,"cost_usd":round(usd,8),
            "provider_verified_units":provider_units,
            "read_completed_units":read_units,
            "agent_reported_units":claimed,"supported_units":confirmed,
            "effect_evidence":evidence,
            "tokens_per_supported_unit":round(total/confirmed,2) if confirmed else None,
            "tokens_per_provider_verified_unit":round(total/provider_units,2) if provider_units else None,
            "tokens_per_agent_reported_unit":round(total/claimed,2) if claimed else None,
            "tokens_per_requested_unit_nominal":round(total/units,2) if units else None,
            "cost_per_supported_unit_usd":round(usd/confirmed,8) if confirmed else None,
            "finished_runs":len(finish_for),
            "unclosed_runs":len(runs)-len(finish_for)
        })
    return sorted(result,key=lambda r:(r["cost_usd"],r["total_tokens"]),reverse=True)

def render(rows,out):
    out.parent.mkdir(parents=True,exist_ok=True)
    by_type=collections.defaultdict(list)
    for row in rows:by_type[row["goal_type"]].append(row)
    by_type_data={}
    for category,items in by_type.items():
        prov=sum(x["provider_verified_units"] for x in items)
        supported=sum(x["supported_units"] for x in items)
        by_type_data[category]={
            "goals":len(items),
            "total_tokens":sum(x["total_tokens"] for x in items),
            "cost_usd":round(sum(x["cost_usd"] for x in items),8),
            "provider_verified_units":prov,"supported_units":supported,
            "note":"Do not divide all category tokens by units from only some goals."}
    result={"created_utc":datetime.datetime.now(datetime.timezone.utc).isoformat(),
            "goal_count":len(rows),"categories":by_type_data,"goals":rows}
    out.write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding="utf-8")
    with out.with_suffix(".csv").open("w",newline="",encoding="utf-8-sig") as f:
        fields=["goal_type","goal_id","status","requested_units","provider_verified_units",
                "read_completed_units","agent_reported_units","supported_units","effect_evidence",
                "tokens_per_supported_unit","total_tokens","input_tokens","cached_tokens",
                "uncached_input_tokens","output_tokens","model_calls","runs","cost_usd",
                "tokens_per_provider_verified_unit","tokens_per_agent_reported_unit",
                "tokens_per_requested_unit_nominal","goal_text"]
        writer=csv.DictWriter(f,fieldnames=fields,extrasaction="ignore")
        writer.writeheader()
        for row in rows:writer.writerow(row)
    return result

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--logs",type=pathlib.Path,default=DEFAULT_LOGS)
    parser.add_argument("--out",type=pathlib.Path,default=DEFAULT_OUTPUT)
    args=parser.parse_args()
    data=render(aggregate(args.logs),args.out)
    print("GOAL_COST_REPORT_OK",data["goal_count"],"goals")
    for cat,v in sorted(data["categories"].items()):
        print(cat,"goals",v["goals"],"tokens",v["total_tokens"],"provider_units",v["provider_verified_units"])
if __name__=="__main__":main()
