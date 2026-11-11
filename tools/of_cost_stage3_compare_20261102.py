"""STAGE 3: actual cost comparison. Stage 2 baseline MUST already exist and is never recalibrated here."""
import json, pathlib, collections, math
root=pathlib.Path(__file__).resolve().parents[1]/'docs'
A=json.loads((root/'OPENFILL_COST_STAGE1_ACTIVITIES_20261102.json').read_text(encoding='utf8'))
B=json.loads((root/'OPENFILL_COST_STAGE2_REASONABLE_BASELINE_20261102.json').read_text(encoding='utf8'))
assert A['runs_count']==len(A['runs']) and B['stage']==2 and B['estimated_runs']>0
O=json.loads((root/'OPENFILL_TOKEN_AUDIT_DATA_20261031.json').read_text(encoding='utf8'))['runs']
log=pathlib.Path(r'C:\Users\pboru\OpenFill\data\stable\logs')/'usage_202611.ndjson'
nov=collections.defaultdict(lambda:{'calls':0,'inputTokens':0,'cachedTokens':0,'outputTokens':0,'reasoningTokens':0,'costUsd':0.0,'modelMs':0,'kind':'calls_only'})
# The final kind=task totals supersede call sums, not added twice.
tasks={}
for line in log.open(encoding='utf8',errors='replace'):
    try:x=json.loads(line)
    except ValueError:continue
    rid=x.get('runId')
    if not rid:continue
    if x.get('kind')=='task':
        tasks[rid]=x
    elif x.get('kind')=='call':
        v=nov[rid]
        v['calls']+=1
        for k in ('inputTokens','cachedTokens','outputTokens','reasoningTokens'):v[k]+=int(x.get(k) or 0)
        v['costUsd']+=float(x.get('costUsd') or 0)
        v['modelMs']+=int(x.get('latencyMs') or 0)
for rid,item in tasks.items():
    nov[rid]={k:item.get(k) or 0 for k in ('calls','inputTokens','cachedTokens','outputTokens','reasoningTokens','costUsd','modelMs')}
    nov[rid]['kind']='task_total'
actual={}
for rid,meta in A['runs'].items():
    src=O.get(rid) or nov.get(rid)
    if src is None or int(src.get('calls') or 0)<=0:continue
    v={k:src.get(k) or 0 for k in ('calls','inputTokens','cachedTokens','outputTokens','reasoningTokens','costUsd','modelMs')}
    v['modelMs']=v.get('modelMs',0) or 0
    v['source']='October indexed audit' if rid in O else 'November usage ledger'
    v['completeness']='task_total_or_indexed' if rid in O or rid in tasks else 'call_sum_missing_task_total'
    v['dom_read_operations']=sum(int(meta.get('tool_counts',{}).get(k,0)) for k in ('get_page','run_js','read_text','read_view','view_digest','inspect_view'))
    v['tool_calls']=sum(int(x) for x in meta.get('tool_counts',{}).values())
    actual[rid]=v
rows=[];groups=collections.defaultdict(list);all_runs=[]
for rid,norm in B['runs'].items():
    act=actual.get(rid)
    if not act:
        continue
    baseline=norm.get('baseline_calls') if norm.get('status')=='estimated' else None
    record={'runId':rid,'service':A['runs'][rid]['service'],'intent':A['runs'][rid]['intent'],
            'classification':A['runs'][rid]['confidence'],'source':act['source'],
            'actual_calls':int(act['calls']),'actual_input_tokens':int(act['inputTokens']),
            'actual_cached_tokens':int(act['cachedTokens']),'actual_uncached_tokens':max(0,int(act['inputTokens'])-int(act['cachedTokens'])),
            'actual_output_tokens':int(act['outputTokens']),'actual_cost_usd':round(float(act['costUsd']),8),
            'actual_model_seconds':round(float(act.get('modelMs') or 0)/1000,1),
            'dom_read_operations':act['dom_read_operations'],'tool_calls':act['tool_calls'],
            'completeness':act['completeness'],'baseline_calls':baseline}
    if baseline is not None:
        record.update({'excess_calls':record['actual_calls']-baseline,
                'excess_calls_pct':round(100*(record['actual_calls']-baseline)/max(1,baseline),1),
                'reference_cost_usd':norm['baseline_cost_usd'],
                'potential_cost_difference_usd':round(record['actual_cost_usd']-norm['baseline_cost_usd'],8),
                'baseline_uncached_tokens':norm['baseline_uncached_tokens'],
                'baseline_dom_reads':norm['baseline_dom_reads']})
        rows.append(record);groups[(record['service'],record['intent'])].append(record)
    all_runs.append(record)
summary=[]
for (site,intent),rr in groups.items():
    ac=sum(x['actual_calls'] for x in rr); bc=sum(x['baseline_calls'] for x in rr)
    c=sum(x['actual_cost_usd'] for x in rr);ref=sum(x['reference_cost_usd'] for x in rr)
    summary.append({'service':site,'intent':intent,'runs':len(rr),
        'actual_calls':ac,'baseline_calls':bc,'excess_calls':ac-bc,
        'calls_reduction_potential_pct':round(100*(ac-bc)/ac,1) if ac else 0,
        'actual_cost_usd':round(c,6),'reference_cost_usd':round(ref,6),
        'cost_difference_usd':round(c-ref,6),
        'actual_uncached_tokens':sum(x['actual_uncached_tokens'] for x in rr),
        'reference_uncached_tokens':sum(x['baseline_uncached_tokens'] for x in rr),
        'actual_dom_read_operations':sum(x['dom_read_operations'] for x in rr),
        'reference_dom_reads':sum(x['baseline_dom_reads'] for x in rr),
        'examples':sorted(rr,key=lambda x:x['excess_calls'],reverse=True)[:3]})
summary.sort(key=lambda x:-x['excess_calls'])
tot=lambda name:sum(x[name] for x in rows)
report={'stage':3,'stage2_preexisting':True,'stage1_inventory':A['runs_count'],
'actual_runs_matched':len(actual),'classified_runs_compared':len(rows),
'actual_runs_without_reference':len(all_runs)-len(rows),'no_matching_usage':len(A['runs'])-len(actual),
'method':'Measured historical usage vs PRE-RECORDED normative assumptions. One runId != one business operation: repeated/continued runs require evidence-based linkage; do not treat potential as already realized. No re-fitting.',
'assumptions':B['policy'],'totals':{'actual_calls':tot('actual_calls'),'baseline_calls':tot('baseline_calls'),
        'excess_calls':tot('actual_calls')-tot('baseline_calls'),
        'potential_call_reduction_pct':round(100*(tot('actual_calls')-tot('baseline_calls'))/max(1,tot('actual_calls')),1),
        'actual_cost_usd':round(tot('actual_cost_usd'),6),
        'reference_cost_usd':round(tot('reference_cost_usd'),6),
        'potential_cost_difference_usd':round(tot('actual_cost_usd')-tot('reference_cost_usd'),6),
        'actual_uncached_tokens':tot('actual_uncached_tokens'),
        'baseline_uncached_tokens':tot('baseline_uncached_tokens'),
        'actual_dom_read_operations':tot('dom_read_operations'),
        'baseline_dom_reads':tot('baseline_dom_reads')},
'groups':summary,'runs':rows,
'excluded_unknown_samples':sorted([r for r in all_runs if r.get('baseline_calls') is None],key=lambda x:-x['actual_calls'])[:30],
'limitations':['Runtime ledger is not an external action acceptance proof.',
    'No confirmed mapping of continuation runs to a single business result; logical-operation savings cannot be claimed definitively until linked.',
    'Normative USD 0.001 per model call is an explicit assumption, not validated tariff.',
    'DOM read proxy includes some non-DOM run_js calls.','Incomplete task-level November usage marked calls_only.']}
path=root/'OPENFILL_COST_STAGE3_MEASURED_COMPARISON_20261102.json'
path.write_text(json.dumps(report,ensure_ascii=False,indent=1),encoding='utf8')
md=['# OpenFill: historyczne zużycie vs oszacowany wzorzec, 2026-11-02',
'','Etap 1: wykaz czynności z logów. Etap 2: zamrożony budżet normatywny. Etap 3: dopiero teraz koszty rzeczywiste.',
'','**Wynik to potencjał optymalizacji, nie zaoszczędzone już pieniądze.**',
'','## Pokrycie','',
f"- Sklasyfikowanych i porównanych przebiegów: {len(rows)}, rozpoznanych z rzeczywistym zużyciem: {len(actual)}.",
f"- Bez rozpoznanej intencji, lecz z logiem zużycia: {len(all_runs)-len(rows)}.",
f"- Bez pomiaru użycia: {len(A['runs'])-len(actual)}.",
'','## Łączny wynik z oszacowaniem','',
'| Metryka | Rzeczywiste | Referencyjne | Różnica |','|---|---:|---:|---:|']
for key,display in [('calls','Wywołania modelu'),('cost_usd','Koszt USD'),('uncached_tokens','Tokeny input uncached'),('dom_reads','Odczyty widoku, przybliżenie')]:
    ak={'calls':'actual_calls','cost_usd':'actual_cost_usd','uncached_tokens':'actual_uncached_tokens','dom_reads':'actual_dom_read_operations'}[key]
    bk={'calls':'baseline_calls','cost_usd':'reference_cost_usd','uncached_tokens':'baseline_uncached_tokens','dom_reads':'baseline_dom_reads'}[key]
    x=report['totals'][ak]; y=report['totals'][bk]
    md.append(f'| {display} | {x:,.3f} | {y:,.3f} | {x-y:,.3f} |')
md.extend(['','## Grupy czynności','',
    '| Serwis | Intencja | Runs | Calls realne | Calls wzorzec | Nadwyżka | USD realne | USD wzorzec |',
    '|---|---|---:|---:|---:|---:|---:|---:|'])
for g in summary:
    md.append(f"| {g['service']} | {g['intent']} | {g['runs']} | {g['actual_calls']} | {g['baseline_calls']} | {g['excess_calls']} | {g['actual_cost_usd']:.4f} | {g['reference_cost_usd']:.4f} |")
md+=['','## Ograniczenia','']+['- '+s for s in report['limitations']]
(root/'OPENFILL_COST_STAGE3_MEASURED_COMPARISON_20261102.md').write_text('\n'.join(md)+'\n',encoding='utf8')
print('STAGE3_COMPARISON_DONE',len(rows),'measured',len(actual),'groups',len(summary),'actual_calls',report['totals']['actual_calls'],'normative_calls',report['totals']['baseline_calls'],'actual_usd',report['totals']['actual_cost_usd'])
for x in summary[:12]:print(x['service'],x['intent'],'runs',x['runs'],'calls',x['actual_calls'],'base',x['baseline_calls'],'usd',x['actual_cost_usd'])
