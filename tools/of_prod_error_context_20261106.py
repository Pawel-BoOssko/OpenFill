import json,pathlib,sys
fn=pathlib.Path(r"C:\Users\pboru\OpenFill\data\stable\logs\app_20261105.ndjson")
target=int(sys.argv[1]) if len(sys.argv)>1 else 1619
with fn.open(encoding="utf-8",errors="replace") as f:
    for line in f:
        try:x=json.loads(line)
        except:continue
        if x.get("runId")!="20261105_075235_393302" or abs(x.get("seq",0)-target)>3:continue
        d=x.get("data") or {}
        print("SEQ",x.get("seq"),"SOURCE",x.get("source"),"EVENT",x.get("eventType"),"STATUS",x.get("status"))
        print("MESSAGE",str(x.get("message",""))[:160])
        print("NAME",d.get("name"),"ARGS",str(d.get("args",""))[:600])
        print("PREVIEW",str(d.get("preview",""))[:900])
