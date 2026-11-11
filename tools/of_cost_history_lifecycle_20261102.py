"""Read-only lifecycle audit, no actual token/cost reads."""
from collections import Counter, defaultdict
import json, pathlib, re, hashlib, datetime
root=pathlib.Path(r'C:\Users\pboru\OpenFill\data\stable')
source=root/'history'/'tasks.ndjson'
out=pathlib.Path(__file__).resolve().parents[1]/'docs'/'OPENFILL_HISTORY_LIFECYCLE_STAGE1_20261102.json'
records=Counter(); latest={}; transitions=defaultdict(list); runs=defaultdict(set); malformed=0
for line in source.open(encoding='utf8',errors='replace'):
    try: d=json.loads(line)
    except ValueError: malformed+=1;continue
    if not isinstance(d,dict) or not d.get('id'): malformed+=1;continue
    task_id=d['id'];status=d.get('status','missing')
    records[status]+=1
    if not transitions[task_id] or transitions[task_id][-1]!=status:transitions[task_id].append(status)
    latest[task_id]=d
    if d.get('runId'):runs[task_id].add(d['runId'])
last=Counter(d.get('status','missing') for d in latest.values())
running={i:d for i,d in latest.items() if d.get('status')=='running'}
# Confirm presence of usage-independent session end evidence for the stale running run IDs.
end=defaultdict(list)
app_logs=root/'logs'
waiting=set(d.get('runId') for d in running.values() if d.get('runId'))
for path in sorted(app_logs.glob('app_*.ndjson')):
    for line in path.open(encoding='utf8',errors='replace'):
        try:e=json.loads(line)
        except ValueError:continue
        if e.get('runId') in waiting and e.get('source')=='session' and e.get('eventType')=='end':
            end[e['runId']].append({'status':e.get('status'),'timestamp':e.get('ts') or e.get('tsUtc')})
textgroups=defaultdict(list)
for i,d in latest.items():
    t=re.sub(r'\s+',' ',d.get('task','')).strip().lower()
    if t:textgroups[hashlib.sha256(t.encode('utf8')).hexdigest()[:18]].append(i)
dup=[ids for ids in textgroups.values() if len(ids)>1]
chains=[]
for ids in dup:
    ordered=sorted(ids,key=lambda i:latest[i].get('startedUtc') or '')
    chains.append({'ids':ordered[:30],'count':len(ids),'allSameTaskText':True,'confirmedSameBusinessTask':False})
chains.sort(key=lambda x:-x['count'])
details=[]
for i,d in running.items():
    run=d.get('runId')
    details.append({'id':i,'runId':run,'startedUtc':d.get('startedUtc'),
    'ageDaysFromStart':round((datetime.datetime.now(datetime.timezone.utc)-datetime.datetime.fromisoformat(d['startedUtc'].replace('Z','+00:00'))).total_seconds()/86400,1) if d.get('startedUtc') else None,
    'matchedSessionEndEvents':end.get(run,[]),
    'statusChain':transitions[i],
    'sameTextOtherTasks':next((len(ids)-1 for ids in dup if i in ids),0)})
report={'stage':1,'path':str(source),'rawRecords':sum(records.values()),'rawStatusCounts':dict(records),
'uniqueTaskIds':len(latest),'latestStatusCounts':dict(last),'malformed':malformed,
'countWithMultipleSnapshots':sum(len(v)>1 for v in transitions.values()),
'countWithMultipleRunIds':sum(len(v)>1 for v in runs.values()),
'countWithSingleRunId':sum(len(v)==1 for v in runs.values()),
'nonterminalRunning':details,'runningWithEndEvent':sum(bool(d['matchedSessionEndEvents']) for d in details),
'sameExactTaskTextCandidateGroups':len(chains),'candidateRepeatedTaskGroups':chains[:150],
'interpretation':'HistoryStore is append-only, its Load() projects inactive running as interrupted without persisting; exact same task text is NOT proof of same business operation. Do not change state or automatically join costs.',
'next':'Investigate 26 stale running and correlate runId to continuation/task instructions before grouping.'}
out.write_text(json.dumps(report,ensure_ascii=False,indent=1),encoding='utf8')
print('HISTORY_AUDIT_OK',sum(records.values()),len(latest),'LATEST_RUNNING',len(running),'WITH_SESSION_END',report['runningWithEndEvent'],'IDENTICAL_TEXT_GROUPS',len(chains),'MULTIRUN_ID',report['countWithMultipleRunIds'])
for row in details:print(row['id'],row['ageDaysFromStart'],len(row['matchedSessionEndEvents']),row['sameTextOtherTasks'])
