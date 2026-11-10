import json, pathlib, collections
root=pathlib.Path(r"C:\Users\pboru\OpenFill\data\stable\logs")
for name in ("usage_202610.ndjson","usage_202611.ndjson"):
    counts=collections.Counter(); runkeys=set(); has_task_key=0; examples=[]
    for raw in (root/name).open(encoding="utf-8",errors="replace"):
        try: x=json.loads(raw)
        except: continue
        counts[x.get("kind")]+=1
        if x.get("runId"):runkeys.add(x["runId"])
        if x.get("kind")=="task":
            has_task_key+=bool(x.get("taskKey"))
            if len(examples)<12:examples.append((x.get("taskKey"),x.get("continuedFrom"),x.get("outcome"),x.get("task","")[:90]))
    print(name,dict(counts),"runs",len(runkeys),"taskKey",has_task_key)
    for e in examples: print("EX",e)
