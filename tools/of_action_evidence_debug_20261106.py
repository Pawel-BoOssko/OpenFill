import json,pathlib
from of_action_evidence_20261106 import reported_units
f=pathlib.Path(__file__).resolve().parents[1]/'docs'/'of_action_accounting_20261106'/'all_business_goals.jsonl'
for ln in f.open(encoding='utf-8'):
 r=json.loads(ln)
 if r['category']=='invitation' and r['requested_units']==5 and r['any_success'] and r['calls']==89:
  s=r['summary']
  print('NEWLINES',chr(10) in s,'LITERAL',chr(92)+'n' in s,'LINES',len(s.splitlines()),'SENT',s.count('WYSŁANO'))
  print('PARSER',reported_units('invitation',5,s,'success'))
  print('HEAD',repr(s[80:240])); break
