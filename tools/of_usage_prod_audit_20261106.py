import json, collections, datetime, pathlib
BASE=pathlib.Path(r"C:\Users\pboru\OpenFill\data\stable\logs")
NOW=datetime.datetime.now(datetime.timezone.utc)
SINCE=NOW-datetime.timedelta(hours=30)
def read(path):
    with path.open(encoding="utf-8",errors="replace") as stream:
        for raw in stream:
            try: yield json.loads(raw)
            except json.JSONDecodeError: continue
def recent(x):
    try: return datetime.datetime.fromisoformat((x.get("ts") or x.get("tsUtc") or "").replace("Z","+00:00"))>=SINCE
    except ValueError: return False
calls=collections.defaultdict(list); tasks=[]; status=collections.Counter()
usage=BASE/"usage_202611.ndjson"
for x in read(usage):
    if not recent(x): continue
    if x.get("kind")=="call": calls[x.get("runId")].append(x)
    if x.get("kind")=="task": tasks.append(x); status[x.get("outcome")]+=1
rows=[]
for run,items in calls.items():
    rows.append(dict(run=run,calls=len(items),cost=sum(x.get("costUsd",0) or 0 for x in items),
        tokens=sum(x.get("inputTokens",0) or 0 for x in items),
        cache=sum(x.get("cachedTokens",0) or 0 for x in items),
        lat=sum(x.get("latencyMs",0) or 0 for x in items)/1000,
        max_input=max((x.get("inputTokens",0) or 0 for x in items),default=0),
        tools=collections.Counter(t for x in items for t in (x.get("tools") or [])),
        task=str(items[0].get("task",""))[:110]))
bycost=sorted(rows,key=lambda x:x["cost"],reverse=True)
taskbycost=sorted(tasks,key=lambda x:x.get("costUsd",0) or 0,reverse=True)
print("AUDIT_NOW_UTC",NOW.isoformat())
print("WINDOW_UTC",SINCE.isoformat())
print("CLOSED_TASKS",len(tasks),"OUTCOMES",dict(status))
print("STOP_REASONS",dict(collections.Counter(x.get("stopReason") for x in tasks)))
print("USER_STOPS_MONTH",[(x.get("taskKey"),x.get("runId")) for x in read(usage) if x.get("kind")=="task" and x.get("stopReason")=="user_stop"][-12:])
print("ACTIVE_RUNS_WITH_CALLS",len(rows))
print("CALLS",sum(x["calls"] for x in rows),"COST_USD",round(sum(x["cost"] for x in rows),6))
print("INPUT_TOKENS",sum(x["tokens"] for x in rows),"CACHED_TOKENS",sum(x["cache"] for x in rows))
print("OUTPUT_TOKENS",sum((c.get("outputTokens") or 0) for items in calls.values() for c in items),"REASONING_TOKENS_INCLUDED",sum((c.get("reasoningTokens") or 0) for items in calls.values() for c in items))
print("TOOL_SCHEMA_CHARS_PER_CALL",round(sum((c.get("toolSchemaChars") or 0) for items in calls.values() for c in items)/max(1,sum(len(items) for items in calls.values()))))
print("TOTAL_TOOL_SCHEMA_CHARS_SENT",sum((c.get("toolSchemaChars") or 0) for items in calls.values() for c in items))
print("TOP_RUNNING_AND_CLOSED_RUNS_BY_COST")
for x in bycost[:14]:
    print(x["run"],"calls",x["calls"],"usd",round(x["cost"],5),
          "max_input",x["max_input"],"tools",str(x["tools"].most_common(5))[:145],"task",x["task"][:65])
print("TOP_CLOSED_TASKS_BY_COST")
for x in taskbycost[:9]:
    print(x.get("taskKey"),x.get("outcome"),"usd",round(x.get("costUsd",0) or 0,5),"calls",x.get("calls"),
        "toolErrors",x.get("toolErrors"),"read",x.get("pageReadRequests"),"repeat",x.get("repeatedPageReadRequests"),
        "run",x.get("runId"))
for fname in ["app_20261105.ndjson","app_20261104.ndjson"]:
    f=BASE/fname
    if not f.exists():continue
    events=collections.Counter(); causes=collections.Counter(); perrun=collections.defaultdict(collections.Counter)
    for x in read(f):
        if not recent(x):continue
        typ=(str(x.get("source","")),str(x.get("eventType","")),str(x.get("status","")))
        events[typ]+=1
        if typ[0]=="tool":
            msg=str(x.get("message",""))
            if typ[1]=="result" and (typ[2]=="error" or msg.endswith(" -> error")):
                causes[msg[:110]]+=1
                perrun[x.get("runId")][msg[:75]]+=1
    print("APP_LOG",fname,"EVENTS",events.most_common(10))
    print("TOP_TOOL_ERRORS",causes.most_common(16))
    print("ERROR_RUNS",sorted(((run,sum(c.values()),c.most_common(3)) for run,c in perrun.items()),
        key=lambda k:k[1],reverse=True)[:10])
