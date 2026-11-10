import json,pathlib,sys,collections
logs=pathlib.Path(r"C:\Users\pboru\OpenFill\data\stable\logs")
runs=sys.argv[1:] or ["20261105_075235_393302","20261105_125414_a51675","20261106_115046_bdb6f6"]
states={r:{"calls":collections.Counter(),"errors":[],"timeline":[],"compacts":0} for r in runs}
for file in ["app_20261105.ndjson","app_20261106.ndjson"]:
    with (logs/file).open(encoding="utf-8",errors="replace") as f:
        for line in f:
            try:x=json.loads(line)
            except:continue
            run=x.get("runId")
            if run not in states:continue
            s=states[run]
            if x.get("source")=="tool":
                data=x.get("data") or {}
                if x.get("eventType")=="call":
                    name=data.get("name") or str(x.get("message","")).split(" ")[0]
                    s["calls"][name]+=1
                    s["timeline"].append(("call",x.get("seq"),name,str(data.get("args",""))[:160]))
                if x.get("eventType")=="result":
                    if x.get("status")=="error":
                        s["errors"].append((x.get("seq"),data.get("name"),str(data.get("preview",""))[:500],next((e[3] for e in reversed(s["timeline"]) if e[0]=="call"),"")))
                    s["timeline"].append(("result",x.get("seq"),data.get("name"),str(data.get("preview",""))[:160]))
            if x.get("source")=="model" and x.get("eventType")=="compact":
                s["compacts"]+=1
for run,s in states.items():
    print("\nRUN",run,"TOOLS",s["calls"].most_common(20),"COMPACTIONS",s["compacts"])
    print("ERRORS")
    for e in s["errors"]:print(e)
    print("LAST_18_EVENTS")
    for e in s["timeline"][-18:]:print(e)
