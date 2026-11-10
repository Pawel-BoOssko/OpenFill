"""STAGE 1: identify activity and scope from logs. DO NOT read actual usage/cost fields."""
import collections, importlib.util, json, pathlib, re, sys
from datetime import datetime, timezone
ROOT=pathlib.Path(__file__).resolve().parents[1]
OLD=ROOT/'docs'/'OPENFILL_TOKEN_AUDIT_DATA_20261031.json'
OUT=ROOT/'docs'/'OPENFILL_COST_STAGE1_ACTIVITIES_20261102.json'
LOG=pathlib.Path(r'C:\Users\pboru\OpenFill\data\stable\logs')
spec=importlib.util.spec_from_file_location('activityclass',ROOT/'tools'/'analyze_intents_actionable_20261101.py')
module=importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)
runs={}
def register(rid,item,source):
    if not rid or rid in runs: return
    task=str(item.get('task') or item.get('task_preview') or '')
    try: service,intent,confidence,rule=module.classify(item)
    except Exception as e:
        service,intent,confidence,rule='unclassified','unknown','low',type(e).__name__
    tools=item.get('tool_counts') or {}
    if not isinstance(tools,dict): tools={}
    nums=re.findall(r'(?<!\\d)(\\d{1,3})\\s*(?:wiadomości|messages|zaproszeń|invitations|kontaktów|contacts|osób|profiles|threads|numerów|numbers|plików|files|postów|posts|shorts)',task,re.I)
    qty=max((int(x) for x in nums if 1<=int(x)<=100),default=1)
    runs[rid]={'service':service,'intent':intent,'confidence':confidence,'rule':rule,
        'source':source,'date':str(item.get('date') or item.get('ts') or '')[:10],
        'scope_items':qty,'scope_from_task':bool(nums),
        'task_excerpt':task[:550],'outcome':str(item.get('outcome') or ''),
        'tool_counts':{str(k):int(v) for k,v in tools.items() if isinstance(v,(int,float))},
        'domains':list((item.get('domains') or {}).keys())[:12]}
old=json.loads(OLD.read_text(encoding='utf8'))
for rid,item in old['runs'].items(): register(rid,item,'oct_existing_log_audit')
# Append November sessions without using usage ledgers (usage belongs to stage 3).
nov={}
for path in sorted(LOG.glob('app_202611*.ndjson')):
    for raw in path.open(encoding='utf8',errors='replace'):
        try:x=json.loads(raw)
        except (ValueError,UnicodeError):continue
        rid=x.get('runId')
        if not rid:continue
        row=nov.setdefault(rid,{'tool_counts':collections.Counter(),'domains':{},'date':path.name[4:12]})
        if x.get('source')=='session' and x.get('eventType')=='start':
            row['task_preview']=(x.get('data') or {}).get('preview','')
        if x.get('source')=='session' and x.get('eventType')=='end':
            row['outcome']=x.get('status','')
        if x.get('source')=='tool' and x.get('eventType')=='call':
            data=x.get('data') or {}
            name=str(data.get('name') or str(x.get('message') or '').split(' ',1)[0])
            if name:row['tool_counts'][name]+=1
for rid,item in nov.items():
    if rid in runs:continue
    item['tool_counts']=dict(item['tool_counts'])
    register(rid,item,'nov_app_event_log')
groups=collections.defaultdict(list)
for rid,row in runs.items():groups[(row['service'],row['intent'])].append(rid)
data={'stage':1,'method':'actual activity inventory ONLY; no actual usage/cost analyzed',
      'sources':['prior October app/usage-derived indexed activity metadata (cost fields ignored)',
          'November app session/tool event logs (usage files unopened)'],
      'runs_count':len(runs),'activity_groups':len(groups),
      'groups':[{'service':s,'intent':i,'tasks':len(ids),
        'high_confidence':sum(runs[r]['confidence']=='high' for r in ids),
        'medium_confidence':sum(runs[r]['confidence']=='medium' for r in ids),
        'examples':ids[:4]} for (s,i),ids in sorted(groups.items())],
      'runs':runs}
OUT.write_text(json.dumps(data,ensure_ascii=False,indent=1),encoding='utf8')
print('STAGE1_DONE runs=',len(runs),'groups=',len(groups),
      'nov=',sum(r['source']=='nov_app_event_log' for r in runs.values()))
for r in sorted(data['groups'],key=lambda q:-q['tasks'])[:24]:
    print(r['service'],r['intent'],r['tasks'], 'high',r['high_confidence'])
