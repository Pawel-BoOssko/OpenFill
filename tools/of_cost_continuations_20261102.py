"""Evidence-led continuation linking. Do NOT use cost fields for identity matching."""
import json,pathlib,collections,re,datetime,difflib
root=pathlib.Path(__file__).resolve().parents[1]/'docs'
history=pathlib.Path(r'C:\Users\pboru\OpenFill\data\stable\history\tasks.ndjson')
state={}
for raw in history.open(encoding='utf8',errors='replace'):
    try:r=json.loads(raw)
    except ValueError:continue
    if isinstance(r,dict) and r.get('id'):state[r['id']]=r
items=sorted(state.values(),key=lambda x:x.get('startedUtc',''))
by_id={x['id']:x for x in items}
id_index={x['id']:i for i,x in enumerate(items)}
by_run={x.get('runId'):x['id'] for x in items if x.get('runId')}
def norm(s):
    return re.sub(r'\s+',' ',str(s or '').lower()).strip()
high=[]; candidate=[]; task_re=re.compile(r'(?:Earlier task\s*:\s*|Previous task\s*:\s*)(.{100,})',re.I|re.S)
for idx,current in enumerate(items):
    task=str(current.get('task') or '')
    n=norm(task)
    parents=[]
    for found in re.findall(r'H20\d{6}-\d{6}-[a-z0-9]{4}|20\d{6}_\d{6}_[a-f0-9]{6}',task):
        parent=found if found in by_id else by_run.get(found)
        if parent and parent!=current['id'] and id_index[parent]<idx:parents.append((parent,'explicit_task_or_run_id'))
    match=task_re.search(task)
    if match:
        # Strip the outer quote but preserve at least first 100 normalized chars.
        earlier=norm(match.group(1).strip(' \n\r\t\"'))
        earlier=earlier.split(' newer task:',1)[0]
        if len(earlier)>=100:
            prefix=earlier[:min(180,len(earlier))]
            options=[older for older in items[:idx] if norm(older.get('task','')).startswith(prefix)]
            if len(options)==1:parents.append((options[0]['id'],'unique_earlier_task_excerpt'))
            elif len(options)>1:
                candidate.append({'child':current['id'],'possible_parents':[x['id'] for x in options[-6:]],'kind':'ambiguous_earlier_excerpt'})
    for parent,kind in set(parents):
        high.append({'child':current['id'],'parent':parent,'kind':kind,
          'childRunId':current.get('runId'),'parentRunId':by_id[parent].get('runId')})
    if not parents and re.search(r'\b(resume|continue|kontynuuj|wzn[oó]w|wcześniejsz|poprzedni)\b',n):
        recent=items[max(0,idx-120):idx]
        similar=[x for x in recent if len(norm(x.get('task','')))>60 and
                 difflib.SequenceMatcher(None,n[:200],norm(x.get('task',''))[:200]).ratio()>.75]
        if similar:
            candidate.append({'child':current['id'],'possible_parents':[x['id'] for x in similar[-6:]],'kind':'similar_continuation_text'})
# Do not merge ambiguous relations.
parent={id:id for id in by_id}
def find(x):
    while parent[x]!=x:
        parent[x]=parent[parent[x]];x=parent[x]
    return x
for edge in high:
    a,b=find(edge['child']),find(edge['parent'])
    if a!=b:parent[a]=b
components=collections.defaultdict(list)
for id in by_id:components[find(id)].append(id)
chains=[{'taskIds':v,'runIds':[by_id[x].get('runId') for x in v if by_id[x].get('runId')],
         'statuses':[by_id[x].get('status') for x in v]} for v in components.values() if len(v)>1]
report={'stage':'1c','source':'history file only','uniqueTasks':len(by_id),
'linkedEvidenceEdges':len(high),'definiteLinkGroups':len(chains),'highConfidenceLinks':high,
'uncertainCandidateLinks':candidate,'candidateCount':len(candidate),'confirmedChains':chains,
'meaning':'Only explicit historic run/task IDs and uniquely matched earlier-task text linked. Similar text is candidate only; no fees or savings computed from uncertain links.'}
out=root/'OPENFILL_CONTINUATION_LINKS_20261102.json'
out.write_text(json.dumps(report,ensure_ascii=False,indent=1),encoding='utf8')
print('LINK_AUDIT_DONE unique',len(by_id),'confirmed_edges',len(high),'confirmed_chains',len(chains),'uncertain_candidates',len(candidate))
for x in chains[:12]:print('CHAIN',len(x['taskIds']),','.join(x['statuses']),'IDS',','.join(x['taskIds'][:3]))
