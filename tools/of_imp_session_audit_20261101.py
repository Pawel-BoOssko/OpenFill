"""Read-only historical session audit: group consecutive tasks by service and idle gap.
Heuristic sessions are explicitly candidates, never asserted to be the same conversation."""
import json, re, collections, datetime, pathlib
ROOT=pathlib.Path(r"C:\Users\pboru\OpenFill\data\stable\logs")
SOURCE=pathlib.Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill")
runs={}
for path in sorted(ROOT.glob("usage_*.ndjson")):
    with path.open(encoding="utf-8",errors="replace") as f:
        for line in f:
            try: x=json.loads(line)
            except (ValueError,UnicodeDecodeError): continue
            rid=x.get("runId")
            if not rid: continue
            r=runs.setdefault(rid,dict(id=rid,ts=None,task="",calls=0,inputs=0,cached=0,outputs=0,cost=0,model_ms=0,tools=collections.Counter(),has_total=False))
            if x.get("kind")=="call":
                if r["ts"] is None: r["ts"]=x.get("ts")
                r["task"]=r["task"] or x.get("task","")
                for t in x.get("tools",[]): r["tools"][t]+=1
                if not r["has_total"]:
                    r["calls"]+=1;r["inputs"]+=x.get("inputTokens",0);r["cached"]+=x.get("cachedTokens",0)
                    r["outputs"]+=x.get("outputTokens",0);r["cost"]+=x.get("costUsd",0);r["model_ms"]+=x.get("latencyMs",0)
            elif x.get("kind")=="task":
                r["task"]=r["task"] or x.get("task","")
                r["has_total"]=True
                r["calls"]=x.get("calls",r["calls"]);r["inputs"]=x.get("inputTokens",r["inputs"])
                r["cached"]=x.get("cachedTokens",r["cached"]);r["outputs"]=x.get("outputTokens",r["outputs"])
                r["cost"]=x.get("costUsd",r["cost"]);r["model_ms"]=x.get("modelMs",r["model_ms"])
def provider(r):
    q=(r["task"] or "").lower()
    if re.search(r"linkedin|linked.in",q):return "LinkedIn"
    if re.search(r"whatsapp|wa.me|jid",q):return "WhatsApp"
    return None
filtered=[]
for r in runs.values():
    r["provider"]=provider(r)
    if r["provider"] and r["ts"]:
        r["time"]=datetime.datetime.fromisoformat(r["ts"].replace("Z","+00:00"))
        filtered.append(r)
filtered.sort(key=lambda x:x["time"])
clusters=[]
for r in filtered:
    if clusters and clusters[-1]["provider"]==r["provider"] and (r["time"]-clusters[-1]["end"]).total_seconds()<5400:
        c=clusters[-1];c["tasks"].append(r);c["end"]=r["time"]
    else:clusters.append(dict(provider=r["provider"],start=r["time"],end=r["time"],tasks=[r]))
def aggregate(c):
    ss=c["tasks"]; tools=sum((r["tools"] for r in ss),collections.Counter())
    return dict(provider=c["provider"],start=str(c["start"]),end=str(c["end"]),run_count=len(ss),
       calls=sum(r["calls"] for r in ss),input_tokens=sum(r["inputs"] for r in ss),
       cached_tokens=sum(r["cached"] for r in ss),uncached_tokens=sum(r["inputs"]-r["cached"] for r in ss),
       output_tokens=sum(r["outputs"] for r in ss),model_ms=sum(r["model_ms"] for r in ss),
       cost_usd=round(sum(r["cost"] for r in ss),6),tools=dict(tools.most_common(12)),
       runs=[dict(id=r["id"],calls=r["calls"],cost_usd=round(r["cost"],6),task=r["task"][:180]) for r in ss])
ranked=sorted(map(aggregate,clusters),key=lambda c:(c["calls"],c["cost_usd"]),reverse=True)
out=SOURCE/"docs"/"OPENFILL_REAL_SESSION_BASELINE_20261101.json"
out.write_text(json.dumps(dict(method="Consecutive tasks mentioning same provider within 90 minutes; heuristic candidate session, not validated same chat identity",total_runs=len(runs),matched_runs=len(filtered),sessions=ranked),ensure_ascii=False,indent=2),encoding="utf-8")
print("TOTAL_RUNS",len(runs),"MATCHED_RUNS",len(filtered),"CANDIDATE_SESSIONS",len(clusters))
for c in ranked[:12]:
    print(c["provider"],c["start"][:16],"runs",c["run_count"],"calls",c["calls"],"input",c["input_tokens"],"cached",c["cached_tokens"],"usd",c["cost_usd"],"tools",list(c["tools"].items())[:6])
print("REPORT",out)
