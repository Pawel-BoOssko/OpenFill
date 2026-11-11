"""Read-only scan of continuation claims, no usage or cost access."""
import collections,datetime,difflib,json,pathlib,re
P=pathlib.Path(__file__).resolve().parents[1]/'docs'
H=pathlib.Path(r'C:\Users\pboru\OpenFill\data\stable\history\tasks.ndjson')
rows={}
for line in H.open(encoding='utf8',errors='replace'):
    try:r=json.loads(line)
    except ValueError:continue
    if r.get('id'):rows[r['id']]=r
tasks=sorted(rows.values(),key=lambda r:r.get('startedUtc',''))
def normalize(x):
    return re.sub(r'\s+',' ',str(x or '')).lower().strip()
def dt(x):
    return datetime.datetime.fromisoformat(x.replace('Z','+00:00'))
by_id={r['id']:r for r in tasks}
by_run={r['runId']:r for r in tasks if r.get('runId')}
recognized=json.loads((P/'OPENFILL_COST_STAGE6_FULL_COVERAGE_20261102.json').read_text(encoding='utf8'))['continuation_chains']
already={tuple(sorted((chain[0],x))) for chain in recognized for x in chain[1:]}
trigger=re.compile(r'\b(continue|continuation|resum[eia]|earlier task|previous task|poprzednie zadanie|wzn[oó]w|kontynuuj|ciąg dalszy|przerwane zadanie)\b',re.I)
read=[]
for i,child in enumerate(tasks):
    desc=child.get('task') or ''
    if not trigger.search(desc):continue
    parentoptions=[]
    # Explicit exact internal references that match a prior task/run.
    for ref in set(re.findall(r'H20\d{6}-\d{6}-[a-z0-9]{4}|20\d{6}_\d{6}_[a-f0-9]{6}',desc)):
        p=by_id.get(ref) or by_run.get(ref)
        if p and p['id']!=child['id'] and p.get('startedUtc','')<child.get('startedUtc',''):
            parentoptions.append((p,'exact_history_id','high'))
    # OpenFill external job id includes date-hour-minute, use it ONLY to propose matching time candidates.
    jobs=set(re.findall(r'OF-(20\d{6})-(\d{4})-[a-z0-9]{4,}',desc,re.I))
    for date,time in jobs:
        target=f'{date}-{time}'
        matches=[p for p in tasks[:i] if p['id'].startswith('H'+date+'-'+time)]
        if len(matches)==1:parentoptions.append((matches[0],'external_job_minute_unique','medium'))
    # Earlier task embeds original user message. Read first block, and find prefix in older tasks.
    for match in re.finditer(r'(?:earlier task|previous task)\s*:\s*["“](.{80,1200}?)(?:["”]\s*(?:[\\n\\r]|$)|["”]\s*[.;])',desc,re.I|re.S):
        embed=normalize(match.group(1))
        pfx=embed[:min(120,len(embed))]
        cands=[p for p in tasks[:i] if normalize(p.get('task','')).startswith(pfx)]
        if len(cands)==1:parentoptions.append((cands[0],'embedded_exact_task_prefix','high'))
    # Exact same instruction in earlier record, within 60m: candidate, not automatic link.
    same=[p for p in tasks[max(0,i-160):i] if normalize(p.get('task'))==normalize(desc) and
          (dt(child['startedUtc'])-dt(p['startedUtc'])).total_seconds()<=3600]
    for p in same[-3:]:parentoptions.append((p,'same_full_instruction_within_60m','medium'))
    unique={}
    for p,reason,conf in parentoptions:
        unique[(p['id'],reason)]=(p,conf)
    linked=[]
    for (pid,reason),(parent,conf) in unique.items():
        key=tuple(sorted([pid,child['id']]))
        linked.append({'parent':pid,'parent_status':parent.get('status'),
            'child':child['id'],'child_status':child.get('status'),
            'method':reason,'evidence_strength':conf,'already_recognized':key in already,
            'minutes_between':round((dt(child['startedUtc'])-dt(parent['startedUtc'])).total_seconds()/60,1),
            'parent_task_preview':str(parent.get('task',''))[:300],
            'child_task_preview':desc[:300]})
    read.extend(linked)
undone=[x for x in read if not x['already_recognized']]
report={'stage':12,'tasks':len(tasks),'with_explicit_continuation_keyword':sum(bool(trigger.search(x.get('task',''))) for x in tasks),
  'link_candidates':len(read),'new_candidates':len(undone),'strong_new_candidates':sum(x['evidence_strength']=='high' for x in undone),
  'note':'No automatic merging. Links referencing same external job minute may refer to internal stages. The original task goal and outcome must be inspected. No cost or token data used.',
  'new_candidates_sorted':sorted(undone,key=lambda r:({'high':0,'medium':1}[r['evidence_strength']],r['minutes_between'])),
  'already_recognized':len(read)-len(undone)}
(P/'OPENFILL_COST_STAGE12_CONTINUATION_CANDIDATES_20261102.json').write_text(json.dumps(report,ensure_ascii=False,indent=1),encoding='utf8')
print('STAGE12_SCAN',json.dumps({k:v for k,v in report.items() if k not in ('new_candidates_sorted','note')},ensure_ascii=False))
for x in report['new_candidates_sorted'][:30]:
    print('CANDIDATE',x['evidence_strength'],x['method'],x['parent'],x['child'],'gap_min',x['minutes_between'])

