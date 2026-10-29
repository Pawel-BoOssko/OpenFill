# OpenFill JEV prompts: copy approved public prompt JSON from owner's docs
import json,re
from pathlib import Path
root=Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill")
for kind,filename in [
 ("inbound","OpenFill_Jev_filtr_polecen_dla_czatu_deweloperskiego__v1.0__2026-10-29_1300.md"),
 ("outbound","OpenFill_Jev_oznaczanie_wstrzykniec_dla_czatu_deweloperskiego__v1.0__2026-10-29_1315.md")
]:
  doc=(root/"docs"/filename).read_text(encoding="utf-8-sig")
  found=[]
  for m in re.finditer(r"```(?:json)?\s*\n([\s\S]*?)\n```",doc,flags=re.I):
    try: payload=json.loads(m.group(1))
    except json.JSONDecodeError: continue
    if isinstance(payload,dict) and payload.get("model")=="typesafe/jev-1.13" and isinstance(payload.get("questions"),dict):
      found.append(payload)
  if len(found)!=1:raise ValueError(f"{kind}: expected exactly one canonical payload, got {len(found)}")
  payload=found[0]
  name="OpenFill.Jev."+kind+".json"
  path=root/"src"/"OpenFill.Core"/"Assets"/("jev_"+kind+"__v1.0__2026-10-29.json")
  path.write_text(json.dumps(payload,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
  print("PROMPT="+kind+" KEYS="+",".join(payload["questions"].keys())+" STATE_KEYS="+",".join(payload["state"].keys())+" FILE="+str(path))
