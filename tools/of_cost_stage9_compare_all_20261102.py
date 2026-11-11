"""Final comparison: frozen stage2 + frozen stage8 assumptions versus stage6 observed ledger."""
import collections,csv,json,pathlib
P=pathlib.Path(__file__).resolve().parents[1]/'docs'
observed=json.loads((P/'OPENFILL_COST_STAGE6_FULL_COVERAGE_20261102.json').read_text(encoding='utf8'))
additional=json.loads((P/'OPENFILL_COST_STAGE8_FROZEN_SUPPLEMENTAL_NORMS_20261102.json').read_text(encoding='utf8'))
assert additional['stage']==8 and not additional['uses_actual_usage']
oldbase=json.loads((P/'OPENFILL_COST_STAGE2_REASONABLE_BASELINE_20261102.json').read_text(encoding='utf8'))['runs']
stage7=json.loads((P/'OPENFILL_COST_STAGE7_UNKNOWN_ACTIVITIES_20261102.json').read_text(encoding='utf8'))['task_inventory']
by7={a['operation_key']:a for a in stage7}
rows=[]
for obs in observed['operations']:
    out=dict(obs)
    supplement=additional['results'].get(obs['operation'])
    if obs['baseline_calls'] is not None:
        out['baseline_origin']='stage2_original'
        meta=oldbase.get(obs['run_ids'][0],{})
        out['confidence']='original_intent_heuristic'
        out['baseline_min_calls']=meta.get('reasonable_calls_min')
        out['baseline_max_calls']=meta.get('reasonable_calls_max')
        out['activity']=obs['intent']
        out['normative_dom_reads']=meta.get('baseline_dom_reads')
        out['normative_uncached_input']=meta.get('baseline_uncached_tokens')
        out['normative_output_tokens']=meta.get('baseline_output_tokens')
    elif supplement and supplement['status']=='normative_assumption':
        out['baseline_calls']=supplement['baseline_calls']
        out['call_difference']=obs['actual_calls']-supplement['baseline_calls']
        out['baseline_min_calls']=supplement['low_calls']
        out['baseline_max_calls']=supplement['high_calls']
        out['activity']=supplement['intent']
        out['confidence']=supplement['classification_confidence']
        out['baseline_origin']='stage8_supplemental'
        out['normative_dom_reads']=supplement['reference_dom_reads']
        out['normative_uncached_input']=supplement['baseline_uncached_input_tokens']
        out['normative_output_tokens']=supplement['baseline_output_tokens']
    else:
        out['baseline_calls']=None
        out['call_difference']=None
        out['baseline_min_calls']=None
        out['baseline_max_calls']=None
        out['normative_dom_reads']=None
        out['normative_uncached_input']=None
        out['normative_output_tokens']=None
        out['activity']=by7.get(obs['operation'],{}).get('new_activity') or obs['intent']
        out['confidence']='unresolved'
        out['baseline_origin']='none'
    out['uncached_input_tokens']=max(0,obs['input_tokens']-obs['cached_tokens'])
    rows.append(out)
comparables=[x for x in rows if x['baseline_calls'] is not None]
group=collections.defaultdict(list)
for r in rows:group[(r['service'],r['activity'],r['baseline_origin'])].append(r)
byclass=[]
for (site,activity,origin),rr in group.items():
    cc=[r for r in rr if r['baseline_calls'] is not None]
    byclass.append({'service':site,'activity':activity,'baseline_origin':origin,
     'count':len(rr),'compared':len(cc),'actual_calls':sum(r['actual_calls'] for r in rr),
     'actual_cost_usd':round(sum(r['actual_cost_usd'] for r in rr),6),
     'reference_calls':sum(r['baseline_calls'] for r in cc),
     'comparable_actual_calls':sum(r['actual_calls'] for r in cc),
     'excess_calls_comparable':sum(r['call_difference'] for r in cc),
     'actual_uncached_input_tokens':sum(r['uncached_input_tokens'] for r in rr),
     'actual_output_tokens':sum(r['output_tokens'] for r in rr)})
byclass.sort(key=lambda x:-x['actual_calls'])
totals={'measured_business_units':len(rows),'measured_runs':observed['summary']['measured_non_airbnb_runs'],
 'covered_units':len(comparables),'uncovered_units':len(rows)-len(comparables),
 'actual_calls_all':sum(r['actual_calls'] for r in rows),
 'actual_usd_all':round(sum(r['actual_cost_usd'] for r in rows),6),
 'actual_calls_in_comparable_units':sum(r['actual_calls'] for r in comparables),
 'reference_calls_in_comparable_units':sum(r['baseline_calls'] for r in comparables),
 'reference_min_calls':sum(r['baseline_min_calls'] or 0 for r in comparables),
 'reference_max_calls':sum(r['baseline_max_calls'] or 0 for r in comparables),
 'actual_cost_usd_in_comparable_units':round(sum(r['actual_cost_usd'] for r in comparables),6),
 'actual_uncached_input_tokens':sum(r['uncached_input_tokens'] for r in rows),
 'actual_output_tokens':sum(r['output_tokens'] for r in rows),
 'confirmed_continuation_chains':observed['summary']['confirmed_continuation_chains'],
 'supplemental_units':sum(r['baseline_origin']=='stage8_supplemental' for r in comparables)}
totals['potential_call_reduction_pct']=round(100*(totals['actual_calls_in_comparable_units']-totals['reference_calls_in_comparable_units'])/totals['actual_calls_in_comparable_units'],1)
assert totals['measured_business_units']==1566
assert totals['actual_calls_all']==76566
assert totals['covered_units']+totals['uncovered_units']==1566
report={'stage':9,'method':'Locked norm tables stage2 + stage8 joined with actual metrics stage6; no norm re-fitting. Values are hypothetical optimization potential, NOT historical savings. All 8 verified continuation groups without whole-task baseline remain uncovered.',
'totals':totals,'groups':byclass,'operations':rows,
'limitations':['Stage8 norms are retrospective and cannot be considered cost-blind despite separate computation from usage.',
 'The 54 uncovered business units require manual task-level effort budgets, including 8 verified continuation chains.',
 'The 96 original service/intent clusters have heuristic error; stage7 classifications carry low/medium confidence.',
 'Actual USD reflects model ledger; no independent tariff simulation and no verified future USD savings.',
 'Time wall-clock of browser tasks is not reliably available for all runs, so it is not synthesized.',
 'The eight confirmed chains do not guarantee all continuation attempts in history were identified.']}
(P/'OPENFILL_COST_STAGE9_FINAL_COMPARISON_20261102.json').write_text(json.dumps(report,ensure_ascii=False,indent=1),encoding='utf8')
with (P/'OPENFILL_COST_STAGE9_FINAL_COMPARISON_20261102.csv').open('w',encoding='utf-8-sig',newline='') as f:
    columns=['operation','service','activity','run_ids','task_ids','run_count','actual_calls','actual_cost_usd','input_tokens','cached_tokens','uncached_input_tokens','output_tokens','baseline_origin','confidence','baseline_calls','baseline_min_calls','baseline_max_calls','call_difference','comparison_status']
    w=csv.DictWriter(f,fieldnames=columns);w.writeheader()
    for row in rows:w.writerow({k:';'.join(map(str,row[k])) if isinstance(row.get(k),list) else row.get(k) for k in columns})
md=['# OpenFill: porównanie modelowej liczby wywołań według czynności, 2026-11-02','',
 'To nie są osiągnięte oszczędności. Referencje stanowią hipotezę o wydajnej obsłudze tych samych celów.',
 '','## Wszystkie dostępne przebiegi poza Airbnb','']
md+=['- '+k+': '+str(v) for k,v in totals.items()]
md+=['','## Rodzaje czynności','',
 '| Serwis | Rodzaj czynności | Normy | Jednostki | Calls realne | Calls porównywalne | Calls wzorcowe | Koszt realny USD |',
 '|---|---|---|---:|---:|---:|---:|---:|']
for x in byclass:
    md.append(f"| {x['service']} | {x['activity']} | {x['baseline_origin']} | {x['count']} | {x['actual_calls']} | {x['comparable_actual_calls']} | {x['reference_calls']} | {x['actual_cost_usd']:.4f} |")
md+=['','## Ograniczenia']+['- '+x for x in report['limitations']]
(P/'OPENFILL_COST_STAGE9_FINAL_COMPARISON_20261102.md').write_text('\n'.join(md)+'\n',encoding='utf8')
print('STAGE9_FINAL',json.dumps(totals,ensure_ascii=False))

