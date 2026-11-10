import pathlib,json
f=pathlib.Path(__file__).resolve().parents[1]/"docs"/"of_action_accounting_20261106"/"all_business_goals.jsonl"
rows=[json.loads(s) for s in f.open(encoding="utf-8")]
for cat in ("publication","registration","single_page_read","invitation","message"):
 print("\n",cat)
 group=[r for r in rows if r["category"]==cat]
 group.sort(key=lambda r:(r["requested_units"]==5,r["any_success"],r["total_tokens"]),reverse=True)
 for x in group[:8]:
  print("req",x["requested_units"],"strict",x["verified_completed_units"],"reported",x["agent_reported_units"],"supported",x["supported_completed_units"],"calls",x["calls"],"total",x["total_tokens"],"per",x["tokens_per_supported_total"],"status",x["status"])
  print("TASK",repr(x["goal"][:155])); print("SUMMARY",repr(x["summary"][:230]))
