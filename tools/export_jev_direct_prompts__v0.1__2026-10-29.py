# Metadata: OpenFill JEV direct prompts v0.1, 2026-10-29
from pathlib import Path
import json,re
root=Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill")
docs=root/"docs"; out=root/"src"/"OpenFill.Core"/"Assets"
pairs=[
("OpenFill_Jev_filtr_polecen_dla_czatu_deweloperskiego__v1.0__2026-10-29_1300.md","jev_inbound__v1.1__2026-10-29.json","client_meddling"),
("OpenFill_Jev_oznaczanie_wstrzykniec_dla_czatu_deweloperskiego__v1.0__2026-10-29_1315.md","jev_outbound__v1.0__2026-10-29.json","ai_directed_instructions")
]
for name,target,key in pairs:
    text=(docs/name).read_text(encoding="utf-8-sig")
    block=re.search(r"```json\s*(\{[\s\S]*?\})\s*```",text)
    if not block:raise RuntimeError("JSON block missing "+name)
    data=json.loads(block.group(1))
    assert data["model"]=="typesafe/jev-1.13"
    assert list(data["questions"])==[key]
    assert data["questions"][key]["type"]=="choice"
    export={"model":data["model"],"questions":data["questions"]}
    dest=out/target
    if dest.exists():raise RuntimeError("Destination exists "+str(dest))
    dest.write_text(json.dumps(export,ensure_ascii=False,indent=2),encoding="utf-8")
    print("PROMPT_SAVED="+target+" LENGTH="+str(dest.stat().st_size)+" QUESTION="+key)
