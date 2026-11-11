import json,pathlib,collections
path=pathlib.Path(__file__).resolve().parents[1]/"docs"/"of_action_accounting_20261106"/"all_business_goals.jsonl"
rows=[json.loads(s) for s in path.open(encoding="utf-8")]
cats=collections.defaultdict(list)
for r in rows:cats[r["category"]].append(r)
for cat,items in cats.items():
 print("\nCATEGORY",cat,"N",len(items))
 ranked=sorted(items,key=lambda x:x["input_tokens"],reverse=True)[:8]
 for r in ranked:
  print(r["status"],"R",r["runs"],"CALLS",r["calls"],"REQ",r["requested_units"],"DONE",r["verified_completed_units"],repr(r["goal"][:150]))
