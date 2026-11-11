"""Final scenario per operation: all reference budgets frozen before this step."""
import csv,json,pathlib,collections
P=pathlib.Path(__file__).resolve().parents[1]/'docs'
measured=json.loads((P/'OPENFILL_COST_STAGE9_FINAL_COMPARISON_20261102.json').read_text(encoding='utf8'))
locked=json.loads((P/'OPENFILL_COST_STAGE10_MANUAL_NORMS_20261102.json').read_text(encoding='utf8'))
assert locked['locked_reference_before_stage11_usage_comparison']
rows=[]
for row in measured['operations']:
    out=dict(row)
    r=locked['manual_task_norms'].get(row['operation']) or locked['whole_operation_norms'].get(row['operation'])
    if out['baseline_calls'] is None and r is not None and r['calls'] is not None:
        out['baseline_calls']=r['calls'];out['baseline_min_calls']=r['min_calls'];out['baseline_max_calls']=r['max_calls']
        out['baseline_origin']='stage10_manual_full_scope' if row['task_ids'] else 'stage10_manual_task_scope'
        out['activity']=r['category'];out['confidence']='manually_reviewed_hypothesis'
        out['normative_rationale']=r['rationale']
        out['call_difference']=out['actual_calls']-r['calls']
    elif out['baseline_calls'] is None:
        out['normative_rationale']='Insufficient objective or incomplete measured chain'
    is_complete=not bool(out['missing_history_attempts']) and out['baseline_calls'] is not None
    if row['task_ids'] and r is not None and not r.get('measured_completeness',True):
        is_complete=False
    if is_complete:
        cost_per_actual_call=out['actual_cost_usd']/max(1,out['actual_calls'])
        out['scenario_reference_usd']=round(cost_per_actual_call*out['baseline_calls'],8)
        out['scenario_reference_usd_low_calls']=round(cost_per_actual_call*(out['baseline_min_calls'] or out['baseline_calls']),8)
        out['scenario_reference_usd_high_calls']=round(cost_per_actual_call*(out['baseline_max_calls'] or out['baseline_calls']),8)
        out['scenario_potential_usd']=round(out['actual_cost_usd']-out['scenario_reference_usd'],8)
        out['savings_usd_is_measured']=False
        out['comparable_final']=True
        out['baseline_comparison']='hypothetical'
    else:
        out['scenario_reference_usd']=None
        out['scenario_reference_usd_low_calls']=None
        out['scenario_reference_usd_high_calls']=None
        out['scenario_potential_usd']=None
        out['savings_usd_is_measured']=False
        out['comparable_final']=False
        out['baseline_comparison']='excluded_no_scope_or_incomplete_usage'
    rows.append(out)
cc=[x for x in rows if x['comparable_final']]
summary={
 'measured_units':len(rows),
 'fully_comparable_units':len(cc),
 'excluded_units':len(rows)-len(cc),
 'history_confirmed_chains':sum(bool(x['task_ids']) for x in rows),
 'measured_runs':measured['totals']['measured_runs'],
 'total_actual_calls':sum(x['actual_calls'] for x in rows),
 'total_actual_usd':round(sum(x['actual_cost_usd'] for x in rows),6),
 'comparable_actual_calls':sum(x['actual_calls'] for x in cc),
 'reference_calls':sum(x['baseline_calls'] for x in cc),
 'baseline_low_calls':sum((x['baseline_min_calls'] or x['baseline_calls']) for x in cc),
 'baseline_high_calls':sum((x['baseline_max_calls'] or x['baseline_calls']) for x in cc),
 'comparable_actual_usd':round(sum(x['actual_cost_usd'] for x in cc),6),
 'reference_usd_fixed_per_run_call_mix':round(sum(x['scenario_reference_usd'] for x in cc),6),
 'reference_usd_low_calls_scenario':round(sum(x['scenario_reference_usd_low_calls'] for x in cc),6),
 'reference_usd_high_calls_scenario':round(sum(x['scenario_reference_usd_high_calls'] for x in cc),6),
 'potential_usd_scenario_not_realized':round(sum(x['scenario_potential_usd'] for x in cc),6),
 'unestimated_actual_usd':round(sum(x['actual_cost_usd'] for x in rows if not x['comparable_final']),6),
 'new_manual_norms':sum(x['baseline_origin'].startswith('stage10_manual') for x in cc),
}
summary['calls_reduction_hypothesis_pct']=round(100*(summary['comparable_actual_calls']-summary['reference_calls'])/summary['comparable_actual_calls'],1)
assert len(rows)==1566
assert summary['total_actual_calls']==76566
assert abs(summary['total_actual_usd']-41.459336)<.000001
assert summary['fully_comparable_units']+summary['excluded_units']==len(rows)
assert summary['history_confirmed_chains']==8
assert sum(x['baseline_origin'].startswith('stage10_manual') for x in cc)==49
groups=collections.defaultdict(list)
for x in rows:groups[(x['service'],x['activity'])].append(x)
activity=[]
for (site,intent),r in groups.items():
    comp=[x for x in r if x['comparable_final']]
    activity.append({'service':site,'activity':intent,'units':len(r),'compared':len(comp),
        'actual_calls':sum(x['actual_calls'] for x in r),
        'actual_usd':round(sum(x['actual_cost_usd'] for x in r),6),
        'compared_actual_calls':sum(x['actual_calls'] for x in comp),
        'reference_calls':sum(x['baseline_calls'] for x in comp),
        'scenario_reference_usd':round(sum(x['scenario_reference_usd'] for x in comp),6)})
activity.sort(key=lambda x:-x['actual_calls'])
result={'stage':11,'summary':summary,'operations':rows,'activity_groups':activity,
'method':'After stage2, stage8 and stage10 normative rules were saved, actual stage6 usage was joined. USD reference is a scenario at the SAME observed USD per model call within each historical operation, NOT a verified price for an optimized new execution. Costs with missing attempts are not compared.',
'limits':['Normative call counts are retrospective estimates, not experimentally observed optimized task completion.',
'Assumption of unchanged average USD/call despite fewer calls is not verified and can fail when prompt size or cache hit ratio changes.',
'Cost scenarios are not real realized savings. The two figures should never be conflated.',
'Continuation coverage is not exhaustive: only previously identified chains were checked.',
'For runs without a task total, individual usage calls were summed and may still omit unlogged activity.']}
(P/'OPENFILL_COST_STAGE11_SCENARIOS_20261102.json').write_text(json.dumps(result,ensure_ascii=False,indent=1),encoding='utf8')
with (P/'OPENFILL_COST_STAGE11_SCENARIOS_20261102.csv').open('w',encoding='utf-8-sig',newline='') as f:
    fields=['operation','service','activity','run_ids','task_ids','actual_calls','actual_cost_usd','baseline_origin','baseline_calls','baseline_min_calls','baseline_max_calls','scenario_reference_usd','scenario_reference_usd_low_calls','scenario_reference_usd_high_calls','scenario_potential_usd','comparable_final','normative_rationale']
    w=csv.DictWriter(f,fieldnames=fields);w.writeheader()
    for r in rows:w.writerow({k:';'.join(str(x) for x in r[k]) if isinstance(r.get(k),list) else r.get(k) for k in fields})
md=['# OpenFill: etap 11, kalkulacja po niezależnym zapisaniu referencji', '', 
 '**Uwaga: USD referencyjne są scenariuszem opartym o średni koszt jednego wywołania Z TEJ SAMEJ historycznej operacji. Nie są oszczędnościami już uzyskanymi ani dowodem wydajnego wykonania.**','']
md+=['- '+key+': '+str(val) for key,val in summary.items()]
md+=['','## Czynności', '',
 '| Serwis | Czynność | Jednostki | Wywołania rzeczywiste | Wywołania referencyjne (porównywalne) | USD rzeczywiste | USD referencyjne (scenariusz) |',
 '|---|---|---:|---:|---:|---:|---:|']
for r in activity:md.append(f"| {r['service']} | {r['activity']} | {r['units']} | {r['actual_calls']} | {r['reference_calls']} | {r['actual_usd']:.4f} | {r['scenario_reference_usd']:.4f} |")
md+=['','## Ograniczenia', '']+['- '+v for v in result['limits']]
(P/'OPENFILL_COST_STAGE11_SCENARIOS_20261102.md').write_text('\n'.join(md)+'\n',encoding='utf8')
print('STAGE11_DONE',json.dumps(summary,ensure_ascii=False))

