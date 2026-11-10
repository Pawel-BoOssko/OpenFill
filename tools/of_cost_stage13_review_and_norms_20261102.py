"""Manual review of explicit continuations: freeze reference budgets before summing actual costs."""
import collections,json,pathlib,re
P=pathlib.Path(__file__).resolve().parents[1]/'docs'
H=pathlib.Path(r'C:\Users\pboru\OpenFill\data\stable\history\tasks.ndjson')
history={}
for line in H.open(encoding='utf8',errors='replace'):
    try:r=json.loads(line)
    except ValueError:continue
    if r.get('id'):history[r['id']]=r
candidates=json.loads((P/'OPENFILL_COST_STAGE12_CONTINUATION_CANDIDATES_20261102.json').read_text(encoding='utf8'))['new_candidates_sorted']
assert len(candidates)==19
# Original stage12 candidate ordering is checked by explicit IDs, never rely on fuzzy score alone.
merge_indices={0,3,4,6,7,9,10,11,12,13,14,15,16,17,18}
exclude_indices={1,5,8}
already_indices={2}
assert set(range(len(candidates)))==merge_indices|exclude_indices|already_indices
details={
 0:"Child retries a failed hotel-chat cleanup from seven-chat WhatsApp cleanup batch, same target inside batch",
 3:"XING company page creation, same company, child resumes incomplete contact-details form",
 4:"Meta WhatsApp Business test message under same developer app, child resumes after logout",
 6:"Apple Business company verification, child explicitly resumes the same login/organization flow",
 7:"YouTube 21-reel posting workflow, child schedules an already uploaded item from the same approved batch",
 9:"LinkedIn one approved post cleanup and associated profile work interrupted by upgrade, child checks/resumes",
 10:"Kenya eTA single form completion, child explicitly resumes same unsubmitted application",
 11:"Bing Places Lupus Consulting same company listing, child retries prior portal lock",
 12:"Kenya eTA same form preparation, child resumes the earlier stopped application",
 13:"OpenStreetMap company account registration, child performs email activation for the account created earlier",
 14:"LinkedIn stale pending-invitation withdrawal, exact same old-invitations cleanup target",
 15:"PanoramaFirm Skakanka listing, child uses missing taxpayer ID to continue interrupted company registration",
 16:"PKT Skakanka listing, child resumes same incomplete business listing",
 17:"Bing Places Skakanka listing, child resumes prior popup-blocked session",
 18:"Apple Business Skakanka same company verification, continued after account login"
}
excluded_reason={
 1:"WhatsApp read-only inventory of 30 chats completed, subsequent seven-chat archiving is a different deliverable",
 5:"Z2U purchase preparation milestone completed, then separate checkout-only form reopened: separate deliverable",
 8:"Child explicitly refers to a PREVIOUS TASK AS DOMAIN LOCK HOLDER, not as work to resume; completely different task: Meta developer configuration versus WhatsApp voucher sending"
}
edges=[]
for i in sorted(merge_indices):
    c=candidates[i]
    assert c['parent'] in history and c['child'] in history
    edges.append({'parent':c['parent'],'child':c['child'],'evidence':details[i],
      'confidence':'manually_reviewed_same_goal'})
# Review also Apple 17:38 -> 18:38 transition, explicit job-id from history parent of #6.
bridge={'parent':'H20261012-173859-b44b','child':'H20261012-183841-53f2',
 'evidence':'18:38 Apple Business verification instruction explicitly states prior workflow OF-20261012-1738-c6b4, same company, and previous child has that start minute',
 'confidence':'manually_reviewed_same_goal'}
assert bridge['parent'] in history and bridge['child'] in history
assert 'OF-20261012-1738-' in history[bridge['child']]['task']
edges.append(bridge)
exclusions=[{'parent':candidates[i]['parent'],'child':candidates[i]['child'],'why':excluded_reason[i]}
  for i in sorted(exclude_indices)]
# A budget is for the complete objective, never for each retry. Saved BEFORE opening measured Stage11.
norms={
 'H20261019-173941-dec2':('whatsapp_cleanup_seven_rejected_threads',7,4,13,'Read 7 selected threads, inspect pending state, archive valid targets, verify'),
 'H20261014-040357-af7d':('xing_company_page_registration',7,4,12,'Reuse existing sign-in, finish company-page wizard, verify resulting page'),
 'H20261022-161118-63fa':('meta_whatsapp_business_test_message',8,5,14,'Select correct developer app, authenticate, identify recipient, issue one authorized test, verify outcome'),
 'H20261012-122923-bc71':('apple_business_company_verification',10,6,17,'Verify Apple account, finish company/location verification including interrupted login gates'),
 'H20261022-140632-0846':('youtube_batch_shorts_scheduling',22,14,36,'Check active account, schedule batch of up to 21 prepared Shorts with local browser batching, verify'),
 'H20261018-143006-2247':('linkedin_profile_post_cleanup_and_followup',7,4,13,'Verify and delete specific authorized post, perform remaining approved work, reload and verify'),
 'H20261018-212426-bede':('kenya_eta_draft_preparation',11,7,20,'Reuse an existing draft, fill verified fields and uploaded documents, stop before submission/payment'),
 'H20261012-172851-3b0a':('bing_places_lupus_listing',7,4,13,'Find existing firm, continue listing through portal and verification blockers'),
 'H20261012-171507-5ae9':('openstreetmap_company_account_registration_activation',6,4,12,'Finish registration with verified account, fetch activation link, confirm activation'),
 'H20261018-124915-84b7':('linkedin_old_invitations_withdrawal',6,4,12,'Filter old pending invites, batch withdrawals, verify reduced pending list'),
 'H20261012-121720-6972':('panoramafirm_skakanka_listing_registration',7,4,13,'Check existing company entry, fill mandatory forms including later supplied NIP, confirm'),
 'H20261012-121914-dff5':('pkt_skakanka_listing_registration',7,4,13,'Check existing company entry, finish login and listing form, confirm'),
 'H20261012-123132-5a42':('bing_places_skakanka_listing_registration',8,5,15,'Restore account login and finish/sketch listing, verify portal state')
}
for k in norms:assert k in history
report={'stage':13,'source':'complete history tasks plus 19 medium-evidence stage12 candidates',
 'source_confirmed_edges':len(edges),'explicitly_separate':len(exclusions),
 'already_reviewed_stage12_candidates':len(already_indices),
 'edges':edges,'excluded':exclusions,
 'whole_operation_reference':{k:{'activity':v[0],'calls':v[1],'low_calls':v[2],
             'high_calls':v[3],'rationale':v[4]} for k,v in norms.items()},
 'normative_budgets_frozen_before_reading_stage11_usage':True,
 'limitations':['Reference is retrospective reasonable-workflow estimate, not measured successful alternative.',
 'Joining changed-scope milestones requires an explicit same-outcome judgement; three different deliverables excluded.',
 'Other unrecognized resumptions may exist. No original log record is modified.']}
(P/'OPENFILL_COST_STAGE13_REVIEWED_LINKS_NORMS_20261102.json').write_text(json.dumps(report,ensure_ascii=False,indent=1),encoding='utf8')
print('STAGE13_FROZEN',len(edges),'NEW_GROUP_BUDGETS',len(norms),'EXCLUDED',len(exclusions))

