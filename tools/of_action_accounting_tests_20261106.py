from of_action_evidence_20261106 import reported_units
import json,pathlib,math
cases=[("publication",3,"Published all three posts",3),
       ("publication",3,"Opublikowano i zweryfikowano trzy osobne posty",3),
       ("invitation",5,"1. A — WYSŁANO\n2. B — POMINIĘTO\n3. C — WYSŁANO",2),
       ("invitation",5,"1. A — NIE WYSŁANO",0),
       ("registration",1,"Rejestracja powiodła się. Profil jest już aktywny.",1),
       ("registration",1,"Submitted interest form.",0)]
for cat,n,s,expect in cases:
 got,_=reported_units(cat,n,s,"success")
 assert got==expect,(cat,expect,got)
f=pathlib.Path(__file__).resolve().parents[1]/"docs"/"of_action_accounting_20261106"/"all_business_goals.jsonl"
n=0
for line in f.open(encoding="utf-8"):
 row=json.loads(line);n+=1
 assert 0<=row["cached_tokens"]<=row["input_tokens"]
 assert 0<=row["reasoning_tokens_in_output"]<=row["output_tokens"]
 assert row["total_tokens"]==row["input_tokens"]+row["output_tokens"]
 if row["requested_units"] is not None:
  assert row["supported_completed_units"]<=row["requested_units"]
 if row["supported_completed_units"]:
  assert math.isclose(row["total_tokens"]/row["supported_completed_units"],
                      row["tokens_per_supported_total"],abs_tol=0.02)
print("SEMANTIC_ACTION_ACCOUNTING_CHECKS_OK",len(cases),"samples",n,"groups")
