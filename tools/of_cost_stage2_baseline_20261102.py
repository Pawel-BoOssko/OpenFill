"""STAGE 2 normative cost estimates, based ONLY on stage 1 activity/scope. Do not read usage/cost logs."""
import json, pathlib, math, collections
root=pathlib.Path(__file__).resolve().parents[1]/'docs'
source=root/'OPENFILL_COST_STAGE1_ACTIVITIES_20261102.json'
output=root/'OPENFILL_COST_STAGE2_REASONABLE_BASELINE_20261102.json'
data=json.loads(source.read_text(encoding='utf8'))
# Reference is local WebView2 + one file-first view + deterministic filters, not a model call per click.
# Counts are estimates for one logically distinct request, NOT measured OpenFill performance.
base={
 'read_thread_or_data':(2,5),'search_resolve':(3,5),
 'send_message':(3,10),'send_invitation':(3,10),
 'edit_record_settings':(4,5),'publish_comment':(3,10),
 'export_extract':(3,5),'upload_schedule_short':(6,1),
 'reel_editor_workflow':(5,1),'schedule_existing_short':(3,2),
 'scheduled_short_followup':(2,10),'schedule_page_posts':(4,5),
 'schedule_posts':(4,5),'configure_analytics_gtm':(7,2),
 'audit_analytics_gtm':(3,3),'audit_channel_readonly':(3,3),
 'audit_page_readonly':(3,5),'configure_channel':(5,2),
 'diagnose_test':(4,3),'meta_developer_configuration':(6,2),
 'read_drive_docs':(2,5),'create_page':(6,1)
}
runs={}; groups=collections.defaultdict(list)
for rid,row in data['runs'].items():
    intent=row['intent']
    qty=row.get('scope_items') or 1
    if intent=='unknown' or intent not in base:
        runs[rid]={'service':row['service'],'intent':intent,'status':'not_estimated_unknown_intent'}
        continue
    turns,unit=base[intent]
    # A model turn coordinates a local batch; UI actions should not cause extra turns per target.
    extra=max(0,math.ceil(qty/unit)-1)
    expected=turns+extra*(2 if intent in ('upload_schedule_short','reel_editor_workflow') else 1)
    expected=min(expected,40)
    low=max(1,math.ceil(expected*0.75))
    high=max(low,math.ceil(expected*1.5))
    # Assumed reference token budget, NOT measured pricing. Price is consciously not fitted from real logs.
    input_tokens=7000+max(0,expected-1)*2400
    uncached_tokens=4000+max(0,expected-1)*1100
    output_tokens=expected*260
    dom_reads=2+max(0,math.ceil(qty/10)-1)
    tool_actions=2+qty
    wall_seconds=expected*5+dom_reads*1.5+tool_actions*1.5
    # Explicit assumed budget: $0.001 / model call, deliberately not derived from logged actual.
    cost_usd=expected*0.001
    runs[rid]={'service':row['service'],'intent':intent,'status':'estimated',
        'scope_items':qty,'scope_source':'task_extraction' if row.get('scope_from_task') else 'default_one',
        'baseline_calls':expected,'reasonable_calls_min':low,'reasonable_calls_max':high,
        'baseline_model_input_tokens':input_tokens,'baseline_uncached_tokens':uncached_tokens,
        'baseline_output_tokens':output_tokens,'baseline_dom_reads':dom_reads,
        'baseline_ui_actions':tool_actions,'baseline_wall_seconds':round(wall_seconds,1),
        'baseline_cost_usd':round(cost_usd,6)}
    groups[(row['service'],intent)].append(rid)
summary=[{'service':s,'intent':i,'tasks':len(ids),
    'normative_calls_total':sum(runs[id]['baseline_calls'] for id in ids),
    'normative_cost_usd_total':round(sum(runs[id]['baseline_cost_usd'] for id in ids),5)}
    for (s,i),ids in sorted(groups.items())]
report={'stage':2,'source':source.name,'method':'Normative effort estimated BEFORE reading real costs. One model coordinator per logical phase, browser actions batched locally, limited DOM captures. No measured usage inputs.',
'policy':{'reference_cost_per_model_call_usd':0.001,'first_input_tokens':7000,
    'uncached_first_tokens':4000,'incremental_input_tokens':2400,
    'uncached_incremental_tokens':1100,'output_tokens_per_call':260,
    'seconds_per_model_call':5,'seconds_per_dom_read':1.5,'seconds_per_action':1.5,
    'note':'Dollar assumptions are illustrative and not verified tariff; call counts are primary comparison. Unknown intents receive no baseline. Multi-run business tasks must be linked before interpreting savings.'},
'base_calls_per_intent':{k:{'base_calls':v[0],'items_per_additional_call':v[1]} for k,v in base.items()},
'estimated_runs':sum(v['status']=='estimated' for v in runs.values()),
'unestimated_runs':sum(v['status']!='estimated' for v in runs.values()),
'groups':summary,'runs':runs}
output.write_text(json.dumps(report,ensure_ascii=False,indent=1),encoding='utf8')
print('STAGE2_BASELINE_FROZEN',report['estimated_runs'],'UNESTIMATED',report['unestimated_runs'],
    'GROUPS',len(summary),'BASELINE_TOTAL_CALLS',sum(x['normative_calls_total'] for x in summary))
