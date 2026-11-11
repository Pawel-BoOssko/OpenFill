"""Reconcile the 16 newly reviewed edges. Stage13 norms were frozen before this cost sum."""
import collections,csv,json,pathlib
P=pathlib.Path(__file__).resolve().parents[1]/'docs'
old=json.loads((P/'OPENFILL_COST_STAGE11_SCENARIOS_20261102.json').read_text(encoding='utf8'))
frozen=json.loads((P/'OPENFILL_COST_STAGE13_REVIEWED_LINKS_NORMS_20261102.json').read_text(encoding='utf8'))
assert frozen['normative_budgets_frozen_before_reading_stage11_usage'] and len(frozen['edges'])==16
hist={}
for line in pathlib.Path(r'C:\Users\pboru\OpenFill\data\stable\history\tasks.ndjson').open(encoding='utf8',errors='replace'):
    try:r=json.loads(line)
    except ValueError:continue
    if r.get('id'):hist[r['id']]=r
original={r['operation']:r for r in old['operations']}
root={k:k for k in original}
ridkey={rid:k for k,op in original.items() for rid in op['run_ids']}
tidkey={tid:k for k,op in original.items() for tid in op['task_ids']}
def key(tid):
    return tidkey.get(tid) or ridkey.get(hist.get(tid,{}).get('runId'))
def find(k):
    while root[k]!=k:root[k]=root[root[k]];k=root[k]
    return k
connections=[];missing_links=[]
for edge in frozen['edges']:
    p=key(edge['parent']); c=key(edge['child'])
    if p is None or c is None:
        missing_links.append(dict(edge,reason='history_task_missing_from_usage',parent_key=p,child_key=c))
        continue
    if find(p)!=find(c):
        root[find(c)]=find(p)
        connections.append(dict(edge,parent_key=p,child_key=c))
groups=collections.defaultdict(list)
for k,r in original.items():groups[find(k)].append(r)
rows=[]
for key0,parts in groups.items():
    taskids=set(t for p in parts for t in p['task_ids'])
    runids=sorted(set(t for p in parts for t in p['run_ids']))
    for e in frozen['edges']:
        p=key(e['parent']); c=key(e['child'])
        if p is not None and find(p)==key0:taskids|={e['parent'],e['child']}
        if c is not None and find(c)==key0:taskids|={e['parent'],e['child']}
    misses=set(t for p in parts for t in p.get('missing_history_attempts',[]))
    for tid in taskids:
        rid=hist.get(tid,{}).get('runId')
        if not rid or rid not in runids:misses.add(tid)
    calls=sum(r['actual_calls'] for r in parts)
    cost=round(sum(r['actual_cost_usd'] for r in parts),8)
    ref_keys=[k for k in frozen['whole_operation_reference'] if k in taskids]
    if len(parts)>1 and len(ref_keys)==1:
        ref=frozen['whole_operation_reference'][ref_keys[0]]
        activity=ref['activity'];origin='stage13_whole_operation'
        base=ref['calls']; low=ref['low_calls']; high=ref['high_calls']
        rationale=ref['rationale']
    elif len(parts)>1:
        ref=None;activity='mixed_continuation';origin='no_norm_for_new_chain'
        base=low=high=None;rationale='Cannot sum per-run references for a merged business action'
    else:
        orig=parts[0];ref=None;activity=orig['activity'];origin=orig['baseline_origin']
        base=orig['baseline_calls'];low=orig['baseline_min_calls'];high=orig['baseline_max_calls']
        rationale=orig.get('normative_rationale')
    if misses:base=low=high=None;origin='incomplete_measured_chain'
    if base is not None:
        costper=cost/max(1,calls)
        reference_usd=round(costper*base,8)
        diffusd=round(cost-reference_usd,8)
    else:
        reference_usd=diffusd=None
    sites={p['service'] for p in parts}
    rows.append({'operation':key0,'service':next(iter(sites)) if len(sites)==1 else 'mixed',
       'activity':activity,'included_units':len(parts),'run_ids':runids,'task_ids':sorted(taskids),
       'actual_calls':calls,'actual_cost_usd':cost,'reference_calls':base,'reference_low_calls':low,
       'reference_high_calls':high,'baseline_origin':origin,'normative_rationale':rationale,
       'reference_cost_usd_scenario':reference_usd,'potential_scenario_difference_usd':diffusd,
       'missing_history_attempts':sorted(misses),
       'comparable':base is not None})
rows.sort(key=lambda r:-r['actual_calls'])
c=[r for r in rows if r['comparable']]
tot={
 'previous_units':len(original),'current_units':len(rows),'new_confirmed_links':len(connections),
 'reviewed_edges_without_usage':len(missing_links),
 'total_actual_calls':sum(r['actual_calls'] for r in rows),
 'total_actual_usd':round(sum(r['actual_cost_usd'] for r in rows),6),
 'compared_units':len(c),'uncompared_units':len(rows)-len(c),
 'compared_actual_calls':sum(r['actual_calls'] for r in c),
 'compared_reference_calls':sum(r['reference_calls'] for r in c),
 'reference_low_calls':sum(r['reference_low_calls'] or r['reference_calls'] for r in c),
 'reference_high_calls':sum(r['reference_high_calls'] or r['reference_calls'] for r in c),
 'compared_actual_usd':round(sum(r['actual_cost_usd'] for r in c),6),
 'reference_cost_scenario_usd':round(sum(r['reference_cost_usd_scenario'] for r in c),6),
 'hypothetical_difference_usd':round(sum(r['potential_scenario_difference_usd'] for r in c),6),
 'chains_with_multiple_runs':sum(r['included_units']>1 or len(r['task_ids'])>1 for r in rows),
 'excluded_cost_due_no_norm':round(sum(r['actual_cost_usd'] for r in rows if not r['comparable']),6)}
tot['call_reduction_scenario_pct']=round(100*(tot['compared_actual_calls']-tot['compared_reference_calls'])/max(1,tot['compared_actual_calls']),1)
assert tot['total_actual_calls']==old['summary']['total_actual_calls']==76566
assert abs(tot['total_actual_usd']-old['summary']['total_actual_usd'])<0.000001
assert tot['previous_units']-tot['current_units']==tot['new_confirmed_links']
report={'stage':14,'summary':tot,'operations':rows,'merged_edges':connections,'unmergeable_edges':missing_links,
 'note':'Reference USD is a scenario at same ACTUAL average USD/model call, never realized savings.',
 'limits':['Only 19 explicit candidate links from stage12 were reviewed; three disjoint deliverables were NOT merged.',
  'Per-task reference for a newly merged chain is NOT summed from earlier per-run estimates.',
  'Completion evidence remains separate from the reported success/partial task statuses.',
  'The live application and original usage/history ledgers are unmodified.']}
(P/'OPENFILL_COST_STAGE14_RECONCILED_20261102.json').write_text(json.dumps(report,ensure_ascii=False,indent=1),encoding='utf8')
with (P/'OPENFILL_COST_STAGE14_RECONCILED_20261102.csv').open('w',encoding='utf-8-sig',newline='') as f:
    columns=['operation','service','activity','run_ids','task_ids','included_units','actual_calls','actual_cost_usd','reference_calls','reference_low_calls','reference_high_calls','baseline_origin','reference_cost_usd_scenario','potential_scenario_difference_usd','missing_history_attempts','comparable']
    w=csv.DictWriter(f,fieldnames=columns);w.writeheader()
    for r in rows:w.writerow({k:';'.join(map(str,r[k])) if isinstance(r.get(k),list) else r.get(k) for k in columns})
md=['# OpenFill etap 14: zadania po weryfikacji dodatkowych kontynuacji','',
 'Koszt modelu z logów jest zmierzony; koszt modelowy referencyjny jest wyłącznie hipotetycznym scenariuszem, a nie faktyczną oszczędnością.',
 '']
md+=['- '+k+': '+str(v) for k,v in tot.items()]
md+=['','## Łańcuchy połączone', '']
for r in rows:
    if r['included_units']>1:
        md.append(f"- {r['operation']}: {r['activity']}, {len(r['run_ids'])} run, {r['actual_calls']} calls, {r['actual_cost_usd']:.6f} USD, wzorzec {r['reference_calls']} calls")
if missing_links:
    md+=['','## Powiązania bez danych wejściowych', '']
    for m in missing_links:md.append('- '+m['parent']+' -> '+m['child']+': '+m['reason'])
md+=['','## Ograniczenia','']+['- '+x for x in report['limits']]
(P/'OPENFILL_COST_STAGE14_RECONCILED_20261102.md').write_text('\n'.join(md)+'\n',encoding='utf8')
print('STAGE14_DONE',json.dumps(tot,ensure_ascii=False))
for r in rows:
    if r['included_units']>1:print('CHAIN',r['activity'],r['actual_calls'],r['reference_calls'],r['actual_cost_usd'])

