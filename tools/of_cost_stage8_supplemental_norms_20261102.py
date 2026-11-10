"""Fixed-scheme task reference budget for stage7 activity taxonomy. DO NOT read actual usage/cost."""
import collections,datetime,json,math,pathlib,re
P=pathlib.Path(__file__).resolve().parents[1]/'docs'
activity=json.loads((P/'OPENFILL_COST_STAGE7_UNKNOWN_ACTIVITIES_20261102.json').read_text(encoding='utf8'))
# Calls for one complex business task, not one click. Token assumptions are fixed.
# Nominal + lower/upper uncertainty, not measured values or promises.
base={
 'read_only_inspection':(3,10),'list_audit':(3,10),'navigation_search':(2,8),
 'account_registration':(6,2),'account_application':(7,2),'account_recovery':(5,2),
 'connection_invitation':(3,10),'send_messages':(3,10),'message_read':(2,15),
 'chat_cleanup':(4,10),'content_schedule':(5,8),'content_publishing':(4,8),
 'channel_configuration':(6,3),'profile_edit':(5,5),'website_edit':(7,3),
 'analytics_configuration':(8,2),'automation_workflow':(7,3),
 'booking_service_configuration':(7,3),'domain_dns_hosting':(5,3),
 'email_forwarding':(5,3),'company_directory_listing':(5,3),
 'content_import':(4,5),'export_data':(3,10),'video_upload':(6,4),
 'insurance_price_comparison':(4,3),'service_price_comparison':(4,3)
}
pattern=re.compile(r'(?<!\d)(\d{1,3})\s*(?:wiadomości|wiadomosci|messages|zaprosze[nń]|invitations|postów|posty|posts|filmów|films|shorts|osób|persons|people|profiles|kontaktów|contacts|konwersacj|rozmów|chatów|items|plików|files|domen|domains)',re.I)
results={};group=collections.defaultdict(list)
for r in activity['task_inventory']:
    label=r['new_activity'];root=r['operation_key']
    # Stage7 rows cover the previously unpriced cases only, including entire linked chains.
    if label not in base or r['linked_task_ids']:
        results[root]={'status':'requires_manual_whole_task_review' if r['linked_task_ids'] else 'no_reliable_intent',
            'service':r['service'],'intent':label,'baseline_calls':None}
        continue
    t=r.get('task_text_preview') or ''
    counts=[int(x) for x in pattern.findall(t) if 1<=int(x)<=120]
    n=max(counts) if counts else 1
    per,threshold=base[label]
    extra=math.ceil(max(0,n-threshold)/threshold)
    # Complexity allowance from long multi-step task instructions, not amount of actual model traffic.
    complex_step=1 if len(t)>=600 and label not in ('read_only_inspection','list_audit','navigation_search') else 0
    calls=max(1,min(28,per+extra+complex_step))
    low=max(1,math.ceil(calls*.65))
    high=max(calls,math.ceil(calls*1.65))
    results[root]={'status':'normative_assumption','service':r['service'],
        'intent':label,'classification_confidence':r['confidence'],
        'items_assumed':n,'item_count_from_text':bool(counts),
        'baseline_calls':calls,'low_calls':low,'high_calls':high,
        'reference_dom_reads':max(1,2+math.ceil(n/12)),
        'baseline_input_tokens':7000+max(0,calls-1)*2400,
        'baseline_output_tokens':calls*260,
        'baseline_uncached_input_tokens':4000+max(0,calls-1)*1100}
    group[label].append(root)
report={'stage':8,'source':'OPENFILL_COST_STAGE7_UNKNOWN_ACTIVITIES_20261102.json',
'uses_actual_usage':False,
'method':'Precommitted activity class rule table, additional batched model coordination per target count and complexity. No actual costs or token fields read. Not a blind prospective study because prior high-level historical totals have already been reviewed.',
'reference_rulebook':{k:{'initial_calls':v[0],'items_per_extra_call':v[1]} for k,v in base.items()},
'number_of_entries':len(results),
'estimated_entries':sum(v['status']=='normative_assumption' for v in results.values()),
'unestimated_entries':sum(v['status']!='normative_assumption' for v in results.values()),
'group_counts':{k:len(v) for k,v in group.items()},
'results':results}
(P/'OPENFILL_COST_STAGE8_FROZEN_SUPPLEMENTAL_NORMS_20261102.json').write_text(json.dumps(report,ensure_ascii=False,indent=1),encoding='utf8')
print('STAGE8_NORMS_FROZEN',report['estimated_entries'],'UNESTIMATED',report['unestimated_entries'],
 'REFERENCE_CALLS',sum(x.get('baseline_calls') or 0 for x in results.values()))

