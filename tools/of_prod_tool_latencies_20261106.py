import pathlib,json,collections,sys
file=pathlib.Path(r"C:\Users\pboru\OpenFill\data\stable\logs\app_20261105.ndjson")
run=sys.argv[1] if len(sys.argv)>1 else "20261105_075235_393302"
d=collections.defaultdict(list)
with file.open(encoding="utf-8",errors="replace") as f:
 for line in f:
  try:x=json.loads(line)
  except:continue
  if x.get("runId")!=run or x.get("source")!="tool" or x.get("eventType")!="result":continue
  obj=x.get("data") or {}
  name=obj.get("name","?")
  d[name].append((obj.get("elapsedMs") or 0,x.get("status")))
for k,v in sorted(d.items(),key=lambda z:sum(a for a,b in z[1]),reverse=True):
 print(k,"n",len(v),"sum_s",round(sum(a for a,b in v)/1000,2),"max_s",round(max(a for a,b in v)/1000,2),"errors",sum(b=="error" for a,b in v))
