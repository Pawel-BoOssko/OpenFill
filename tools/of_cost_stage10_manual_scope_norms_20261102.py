"""Stage 10: individually reviewed task scopes, then frozen reference; no ledger opened."""
import json, pathlib, collections
P=pathlib.Path(__file__).resolve().parents[1]/"docs"
unresolved=json.loads((P/"OPENFILL_COST_STAGE7_UNKNOWN_ACTIVITIES_20261102.json").read_text(encoding="utf8"))
rows=[x for x in unresolved["task_inventory"] if x["new_activity"]=="requires_manual_classification"]
assert len(rows)==46, len(rows)
# Explicit index corresponds to the stage7 file, not live pricing or usage. 1 call = coordination phase,
# deterministic local DOM/terminal steps are not model calls. Low/high bands are declared.
# None denotes an underspecified or unlinked continuation, NOT zero price.
norms=[
 ("facebook_local_groups_research",4,"Identify distinct locality groups, collect details in one local batch and report"),
 ("youtube_duplicate_privacy_edit",4,"Verify two retained and two duplicated Shorts; change visibility of the two copies; confirm"),
 ("wordpress_privacy_policy_edit",4,"Open one privacy page, deterministic replacement of specified names, save and verify"),
 ("insurance_quote_compare",5,"Complete one UNIQA form for two people, check basic and expanded variants and report"),
 ("olx_send_cancellation",3,"Open one existing conversation, deduplicate, send cancellation, verify"),
 ("petcare_provider_outreach",6,"Search several providers in one batch, filter individually, send inquiries, record outcomes"),
 ("google_ads_unbilled_account_setup",8,"Enter correct Google Ads account, skip billing and campaign activation, configure draft or report block"),
 ("whatsapp_rejected_chats_cleanup",7,"Review exactly seven threads before optional sending/archiving; batch reads and confirmations"),
 ("linkedin_company_follower_invitations",5,"Open page invitation feature, select eligible contacts locally, send bounded batch and verify"),
 ("directadmin_forwarding",4,"Inspect forwarding rule, add second recipient, save and verify both targets"),
 ("google_signup_continuation",None,"Continuation without original scope; cannot set whole-task benchmark from this fragment"),
 ("provider_contact_lookup",3,"Find service contact in site/search and verify source"),
 ("ftp_password_update",3,"Enter specified main-account password twice, submit, confirm status"),
 ("whatsapp_open_waiting",2,"Open specific conversation tab and hand off without sending"),
 ("google_recovery_phones",4,"Inspect account and change up to two recovery contacts without enabling 2FA"),
 ("ftp_password_update",3,"Enter specified main-account password twice, submit, confirm status"),
 ("apple_business_claim",7,"Open Apple Business Connect, locate/create listing, fill core fields and submit or stop at verification"),
 ("ftp_password_update",3,"Enter specified main-account password twice, submit, confirm status"),
 ("google_recovery_status_check",3,"Inspect account security state and optionally add alternate number"),
 ("google_security_configuration",6,"Configure recovery methods and optionally 2FA with verification checkpoints"),
 ("whatsapp_dom_selectors",3,"One captured DOM, extract two selectors and validate attributes"),
 ("github_create_empty_private_repository",3,"Open form, enter fixed fields, submit, verify URL"),
 ("whatsapp_account_identity_read",3,"Read account identifier and stable DOM selector once"),
 ("apple_business_claim",7,"Open business connect, create/claim listing, stop at user-only verification"),
 ("dom_capture_benchmark_two_sites",5,"Capture compact DOM for two sites and compare observed counts"),
 ("hostido_login_read_only",3,"Open existing session, authenticate only if possible and inspect landing page"),
 ("cloudflare_signup_continuation",None,"Only a fragment of a prior signup; whole-task benchmark requires predecessor"),
 ("wordpress_application_password",3,"Check existing application passwords, add only if absent and return outcome"),
 ("news_homepage_read",2,"Open one page, summarize visible news, no interactions"),
 ("linkedin_company_page_create",5,"Authenticate, fill company listing, submit, verify or report gate"),
 ("linkedin_company_follower_invitations",5,"Open invitation feature and send one selected batch"),
 ("deterministic_two_query_fixture",2,"Open local/example fixture, perform two scripted queries on shared capture"),
 ("linkedin_company_follower_invitations",5,"Open invitation feature and send one selected batch"),
 ("pet_hotel_contact_enquiry",4,"Verify contact, send one nonbinding inquiry, verify acknowledgement"),
 ("single_page_fixture_text_read",1,"One deterministic navigation and element read"),
 ("single_page_fixture_product_read",1,"One deterministic navigation and two field read"),
 ("fixture_continuation",None,"Continuation with altered instructions and missing earlier scope"),
 ("unspecified_continue_instruction",None,"No defined measurable objective in standalone prompt"),
 ("search_input_fixture",2,"Open onet, enter fixed query, trigger search and report"),
 ("single_page_fixture_title_report_read",1,"One deterministic fixture navigation and two field read"),
 ("news_homepage_read",2,"One news homepage visit and short summary"),
 ("single_page_title_read",1,"One page load and title extraction"),
 ("single_page_title_read",1,"One page load and title extraction"),
 ("single_page_title_read",1,"One page load and title extraction"),
 ("single_page_title_read",1,"One page load and title extraction"),
 ("single_page_title_read",1,"One page load and title extraction")
]
assert len(norms)==46, len(norms)
manual={}
for row,(category,calls,why) in zip(rows,norms):
    assert row['task_text_preview'] and row['operation_key']
    manual[row['operation_key']]={'category':category,'calls':calls,
        'min_calls':max(1,round(calls*.75)) if calls is not None else None,
        'max_calls':max(calls,round(calls*1.5)) if calls is not None else None,
        'rationale':why,'scope_excerpt':row['task_text_preview'][:220],
        'estimate_type':'manual_hypothesis' if calls is not None else 'not_estimable_from_prompt'}
chain=json.loads((P/'OPENFILL_COST_STAGE6_FULL_COVERAGE_20261102.json').read_text(encoding='utf8'))
# Keys are task IDs of the eight reviewed full-business-operation chains. Distinct phases, one budget per chain.
chain_rules={
 'H20261011-200222-bb22':('ga4_gtm_two_service_setup',12,'Account validation, GA4 configuration, stream, GTM workspace and final verification'),
 'H20261011-153302-d9f0':('facebook_schedule_four_posts',9,'Read four prepared files, create four scheduled drafts using deterministic browser actions, final audit'),
 'H20261012-200258-877a':('malt_recovery_with_profile_audit',8,'Attempt login, recover through Gmail, inspect existing profile and registration completeness'),
 'H20261027-152721-24ff':('tiktok_profile_edit',6,'Ensure correct identity, adjust display name/bio/photo/link, verify'),
 'H20261011-195049-7b26':('bing_gsc_site_import',5,'Login via Google, import verified site, check sitemap and report'),
 'H20261011-223411-64bf':('linkedin_company_audit_readonly',3,'One company page capture, extract fields and inspect visible company tabs'),
 'H20261019-204000-cc32':('whatsapp_all_chat_list_readonly',5,'One authenticated list, deterministic paged DOM scroll, aggregate all previews and report'),
 'H20261005-161334-8196':('malt_signup_email_verification',6,'Find verification mail, enter code, complete signup, report profile onboarding')
}
chains={}
for x in chain['operations']:
    if not x['task_ids']:continue
    key=x['operation']
    assert key in chain_rules,key
    cat,calls,why=chain_rules[key]
    chains[key]={'category':cat,'calls':calls,'min_calls':max(1,round(calls*.7)),
        'max_calls':round(calls*1.65),'rationale':why,
        'measured_completeness':not bool(x['missing_history_attempts']),
        'note':'Reference to whole business operation, not separately to each retry'}
assert len(chains)==8
out={'stage':10,'locked_reference_before_stage11_usage_comparison':True,
  'inputs':'Stage7 task descriptions, and complete original text of eight confirmed chains; no stage6 token or cost metrics used in calculating values',
  'method':'Reviewed individual goals; few model coordination phases, deterministic page actions per batch. Retrospective estimates only.',
  'manual_task_norms':manual,'whole_operation_norms':chains,
  'task_entries':len(manual),'task_estimates':sum(x['calls'] is not None for x in manual.values()),
  'whole_chains':len(chains),
  'missing_task_norms':sum(x['calls'] is None for x in manual.values())}
(P/'OPENFILL_COST_STAGE10_MANUAL_NORMS_20261102.json').write_text(json.dumps(out,ensure_ascii=False,indent=1),encoding='utf8')
print('STAGE10_FROZEN',len(manual),out['task_estimates'],'CHAIN',len(chains),
      'NO_TASK',out['missing_task_norms'])

