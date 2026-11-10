"""Read-only final coverage: every recorded OpenFill model run, with confirmed restart chains."""
import collections,csv,json,pathlib
P=pathlib.Path(__file__).resolve().parents[1]/'docs'
LOG=pathlib.Path(r'C:\Users\pboru\OpenFill\data\stable\logs')
hist={}
for line in (LOG.parent/'history'/'tasks.ndjson').open(encoding='utf8',errors='replace'):
    try:r=json.loads(line)
    except ValueError:continue
    if r.get('id'):hist[r['id']]=r
history_by_run={r.get('runId'):r for r in hist.values() if r.get('runId')}
idx=json.loads((P/'OPENFILL_COST_STAGE1_ACTIVITIES_20261102.json').read_text(encoding='utf8'))['runs']
baseline=json.loads((P/'OPENFILL_COST_STAGE2_REASONABLE_BASELINE_20261102.json').read_text(encoding='utf8'))['runs']
prior=json.loads((P/'OPENFILL_COST_STAGE5_REVIEWED_OPERATIONS_20261102.json').read_text(encoding='utf8'))
october=json.loads((P/'OPENFILL_TOKEN_AUDIT_DATA_20261031.json').read_text(encoding='utf8'))['runs']
# Task totals supersede call summaries; some interrupted attempts lack a task-total record.
task={}; call=collections.defaultdict(lambda:dict(calls=0,costUsd=0.,inputTokens=0,cachedTokens=0,outputTokens=0))
for name in ('usage_202610.ndjson','usage_202611.ndjson'):
    for line in (LOG/name).open(encoding='utf8',errors='replace'):
        try:r=json.loads(line)
        except ValueError:continue
        rid=r.get('runId')
        if not rid:continue
        if r.get('kind')=='task':task[rid]=r
        elif r.get('kind')=='call':
            c=call[rid];c['calls']+=1
            for k in ('costUsd','inputTokens','cachedTokens','outputTokens'):
                c[k]+=float(r.get(k) or 0)
usage={}
for rid in set(task)|set(call)|set(october):
    if rid in task: d=task[rid];evidence='task_total'
    elif rid in call:d=call[rid];evidence='call_sum_only'
    else:d=october[rid];evidence='october_index_only'
    usage[rid]={'calls':int(d.get('calls') or 0),'cost':float(d.get('costUsd') or 0),
        'input':int(d.get('inputTokens') or 0),'cached':int(d.get('cachedTokens') or 0),
        'output':int(d.get('outputTokens') or 0),'evidence':evidence}
# Keep all unindexed usage, but never infer missing cost=0 and never fabricate normative baselines.
def inferred_service(rid):
    if rid in idx:return idx[rid]['service'],idx[rid]['intent']
    history=history_by_run.get(rid,{})
    task_text=str(history.get('task') or task.get(rid,{}).get('task') or '').casefold()
    if 'airbnb' in task_text:return 'airbnb','unindexed'
    for text,svc in (('whatsapp','whatsapp'),('linkedin','linkedin'),
                     ('facebook','meta_other'),('business.facebook','meta_business'),
                     ('instagram','meta_other'),('youtube','youtube_other'),
                     ('bing.com/webmasters','bing_webmaster'),('analytics.google','google_analytics_gtm'),
                     ('tagmanager.google','google_analytics_gtm'),('reddit','reddit'),
                     ('tiktok','tiktok'),('n8n.cloud','n8n')):
        if text in task_text:return svc,'unindexed'
    return 'other','unindexed'
skipped=[]
for rid in list(usage):
    site,_=inferred_service(rid)
    if site=='airbnb':skipped.append(rid);del usage[rid]
# Reconstruct all 8 confirmed/reviewed continuation chains from history links.
stage4=json.loads((P/'OPENFILL_COST_BUSINESS_RECONCILED_20261102.json').read_text(encoding='utf8'))
chains=[list(x) for x in stage4['confirmed_exact_repeat_chains']]
for e in prior['merged_edges']:chains.append([e['parent'],e['child']])
# The fifth manually verified Facebook link had an unindexed parent with call-only ledger.
fb=[x for x in prior['separate_or_unresolved'] if x['decision']=='merge']
assert len(fb)==1 and fb[0]['child']=='H20261011-201315-8ab2'
chains.append([fb[0]['parent'],fb[0]['child']])
links={k:k for k in hist}
def find(k):
    while links[k]!=k:links[k]=links[links[k]];k=links[k]
    return k
for group in chains:
    for child in group[1:]:links[find(child)]=find(group[0])
components=collections.defaultdict(list)
for tid in hist:components[find(tid)].append(tid)
taskgroups=[tids for tids in components.values() if len(tids)>1]
assert len(taskgroups)==8,[(len(x),x) for x in taskgroups]
run2op={}
for tids in taskgroups:
    key=min(tids)
    for tid in tids:
        rid=hist[tid].get('runId')
        if rid:run2op[rid]=key
operations=collections.defaultdict(list)
for rid in usage:operations[run2op.get(rid,rid)].append(rid)
rows=[]
for op,rids in operations.items():
    gids=next((x for x in taskgroups if min(x)==op),[])
    observed=[usage[rid] for rid in rids]
    sources={inferred_service(rid) for rid in rids}
    svc,intent=next(iter(sources)) if len(sources)==1 else ('mixed','mixed')
    missing=[tid for tid in gids if not hist[tid].get('runId') or hist[tid]['runId'] not in usage]
    comparable=(len(rids)==1 and not gids and rids[0] in baseline and
        baseline[rids[0]].get('status')=='estimated')
    ref=baseline[rids[0]]['baseline_calls'] if comparable else None
    costs=sum(d['cost'] for d in observed)
    calls=sum(d['calls'] for d in observed)
    rows.append({'operation':op,'service':svc,'intent':intent,'run_ids':sorted(rids),
       'task_ids':gids,'run_count':len(rids),'actual_calls':calls,'actual_cost_usd':round(costs,8),
       'input_tokens':sum(x['input'] for x in observed),'cached_tokens':sum(x['cached'] for x in observed),
       'output_tokens':sum(x['output'] for x in observed),'baseline_calls':ref,
       'call_difference':calls-ref if ref is not None else None,
       'missing_history_attempts':missing,
       'measurement_types':sorted({x['evidence'] for x in observed}),
       'comparison_status':'comparable' if comparable else ('linked_whole_task_no_baseline' if gids else 'unknown_or_not_estimated')})
rows.sort(key=lambda x:-x['actual_calls'])
c=[r for r in rows if r['comparison_status']=='comparable']
summary={'all_usage_runs':len(set(task)|set(call)|set(october)),
         'measured_non_airbnb_runs':len(usage),'excluded_airbnb_runs':len(skipped),
         'measured_non_airbnb_calls':sum(r['actual_calls'] for r in rows),
         'measured_non_airbnb_cost_usd':round(sum(r['actual_cost_usd'] for r in rows),6),
         'confirmed_continuation_chains':len(taskgroups),
         'comparable_operations':len(c),
         'comparable_actual_calls':sum(x['actual_calls'] for x in c),
         'comparable_baseline_calls':sum(x['baseline_calls'] for x in c),
         'extra_runs_missing_from_stage1':sum(rid not in idx for rid in usage),
         'extra_runs_call_only':sum(v['evidence']=='call_sum_only' and rid not in idx for rid,v in usage.items()),
         'potential_excess_calls_comparable_only':sum(x['actual_calls']-x['baseline_calls'] for x in c)}
assert sum(row['actual_calls'] for row in rows)==sum(x['calls'] for x in usage.values())
report={'stage':6,'method':'All task-level and call-level usage ledgers. No duplication between task totals and component calls. 8 verified continuation groups, including a history-only indexed gap. Frozen stage2 reference only where valid.',
        'summary':summary,'continuation_chains':taskgroups,'operations':rows,
        'limitations':['Missing task totals are reconstructed from recorded individual call events only, not assumed zero.',
            'Historical non-airbnb runs missing both task and call ledger records cannot be priced.',
            'Only the flagged 7 ambiguities were manually reviewed; other repeated work may remain unidentified.',
            'Frozen reference per run is not a whole-task reference for changed-scope continuation chains; comparison omitted.',
            'This computes hypothetical call differences, not achieved USD savings.']}
(P/'OPENFILL_COST_STAGE6_FULL_COVERAGE_20261102.json').write_text(json.dumps(report,ensure_ascii=False,indent=1),encoding='utf8')
with (P/'OPENFILL_COST_STAGE6_FULL_COVERAGE_20261102.csv').open('w',encoding='utf-8-sig',newline='') as f:
    fields=['operation','service','intent','run_ids','task_ids','run_count','actual_calls','actual_cost_usd','input_tokens','cached_tokens','output_tokens','baseline_calls','call_difference','missing_history_attempts','measurement_types','comparison_status']
    w=csv.DictWriter(f,fieldnames=fields);w.writeheader()
    for row in rows:w.writerow({k:';'.join(str(t) for t in row[k]) if isinstance(row.get(k),list) else row.get(k) for k in fields})
md=['# OpenFill: pełny odczyt usage, rozliczenie przebiegów i 8 potwierdzonych łańcuchów','',
    'Ustalenia:']
md+=['- '+k+': '+str(v) for k,v in summary.items()]
md+=['','## Ograniczenia']+['- '+x for x in report['limitations']]
md+=['','## 8 łańcuchów wznowień']+['- '+', '.join(x) for x in taskgroups]
(P/'OPENFILL_COST_STAGE6_FULL_COVERAGE_20261102.md').write_text('\n'.join(md)+'\n',encoding='utf8')
print('STAGE6_DONE',json.dumps(summary,ensure_ascii=False))
for row in rows:
    if row['task_ids']:
        print('CHAIN',row['operation'],row['actual_calls'],round(row['actual_cost_usd'],5), 'missing',len(row['missing_history_attempts']))



