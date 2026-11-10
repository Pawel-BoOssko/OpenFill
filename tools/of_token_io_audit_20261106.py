import json,pathlib,collections,sys
p=pathlib.Path(r"C:\Users\pboru\OpenFill\data\stable\logs\app_20261105.ndjson")
runs=set(sys.argv[1:] or ["20261106_125447_c5e307","20261106_131116_bd27db","20261105_075235_393302"])
data=collections.defaultdict(lambda:collections.defaultdict(list))
with p.open(encoding="utf-8",errors="replace") as f:
 for s in f:
  try:x=json.loads(s)
  except:continue
  run=x.get("runId")
  if run not in runs or x.get("source")!="tool" or x.get("eventType")!="result":continue
  o=x.get("data") or {}
  data[run][o.get("name","?")].append((o.get("outputBytes") or 0,o.get("preview") or ""))
for run,tools in data.items():
 print("RUN",run)
 for name,items in sorted(tools.items(),key=lambda p:sum(i[0] for i in p[1]),reverse=True):
  lengths=[i[0] for i in items]
  print(name,"n",len(items),"output_bytes",sum(lengths),"mean",round(sum(lengths)/len(items),1),"max",max(lengths))
