"""Read-only task-intent inventory for stage6 units missing a frozen reference."""
import collections,csv,json,pathlib,re
P=pathlib.Path(__file__).resolve().parents[1]/'docs'
R=json.loads((P/'OPENFILL_COST_STAGE6_FULL_COVERAGE_20261102.json').read_text(encoding='utf8'))
A=json.loads((P/'OPENFILL_COST_STAGE1_ACTIVITIES_20261102.json').read_text(encoding='utf8'))['runs']
history={}
for line in pathlib.Path(r'C:\Users\pboru\OpenFill\data\stable\history\tasks.ndjson').open(encoding='utf8',errors='replace'):
    try:r=json.loads(line)
    except ValueError:continue
    if r.get('runId'):history[r['runId']]=r.get('task','')
ledger={}
for fname in ('usage_202610.ndjson','usage_202611.ndjson'):
    for line in pathlib.Path(r'C:\Users\pboru\OpenFill\data\stable\logs',fname).open(encoding='utf8',errors='replace'):
        try:r=json.loads(line)
        except ValueError:continue
        if r.get('kind')=='task' and r.get('runId'):ledger[r['runId']]=r.get('task','')
# Rules deliberately describe WHAT the instruction asks, not whether the attempt succeeded.
rules=[
 ('connection_invitation',r'(?:zaprosze[nń]|invitation).{0,90}(?:wyślij|wycofaj|send|withdraw)|(?:wyślij|wyslij|send|withdraw).{0,90}(?:zaprosze[nń]|invitation)', 'medium'),
 ('channel_configuration',r'(?:youtube|kanał|channel).{0,180}(?:handle|playlist|listy odtwarzania|dostosowywanie|basic info|opis kanału)', 'medium'),
 ('insurance_price_comparison',r'(?:ubezpieczeni|insurance|generali|rankomat|cuk.pl).{0,180}(?:wycen|cena|ofert|kalkulat|quote)', 'medium'),
 ('service_price_comparison',r'(?:uber|taks[oó]wk|taxi|carrefour).{0,150}(?:cen|wycen|koszyk|cart|checkout|order)', 'medium'),
 ('domain_dns_hosting',r'(?:domen|domain|hosting|dns|lh.pl|seohost).{0,160}(?:konfigur|check|sprawd|wyb[oó]r|koszyk|cart|kup|purchase)', 'medium'),
 ('email_forwarding',r'(?:skrzynka|mailbox|email|poczta).{0,140}(?:przekierow|forward|kopi[aę]|redirect|smtp|imap)', 'medium'),
 ('company_directory_listing',r'(?:katalog|directory|panoramafirm|cylex|listing).{0,160}(?:wpis|dodaj|free|bezpł|create|add)', 'medium'),
 ('account_application',r'(?:aplikuj|apply|application|join as|consultant|freelancer).{0,160}(?:profil|konto|account|zł[oó]ż|complete|application|marketplace)', 'medium'),
 ('content_import',r'(?:medium|importuj|import).{0,160}(?:artykuł|article|post|histori|draft|szkic|url)', 'medium'),
 ('booking_service_configuration',r'(?:rezerwacj|booking).{0,160}(?:sms|whatsapp|integrac|system|config|konfigur)', 'medium'),
 ('read_only_inspection',r'(?:odczyt tylko|tylko odczyt|read only|read-only|wyłącznie odczytowe)', 'low'),
 ('chat_cleanup',r'whatsapp.{0,100}(?:archiwiz|usun|sprz[aą]t|archive|delete)', 'medium'),
 ('send_messages',r'(?:wyślij|wyslij|send).{0,70}(?:wiadomoś|message|dm|zaproszen|invitation)', 'medium'),
 ('content_schedule',r'(?:zaplanuj|schedule).{0,90}(?:post|reel|short|film|video)', 'medium'),
 ('video_upload',r'(?:wgraj|upload).{0,100}(?:short|wideo|video|film)', 'medium'),
 ('content_publishing',r'(?:opublikuj|publish).{0,90}(?:post|comment|komentarz|film|short|reel)', 'medium'),
 ('account_registration',r'(?:create|utw[oó]rz|zał[oó]ż|zaloz|register|sign.?up).{0,100}(?:konto|account|profil|profile|listing|wizyt[oó]wk)', 'medium'),
 ('account_recovery',r'(?:odzyskaj|recovery|recover|reset).{0,80}(?:konto|account|hasł|password|dost[eę]p)', 'medium'),
 ('analytics_configuration',r'(?:google analytics|gtm|ga4|tag manager).{0,180}(?:konfigur|ustaw|setup|configure|utw[oó]rz|create)', 'medium'),
 ('automation_workflow',r'(?:n8n|workflow|webhook).{0,150}(?:konfigur|napraw|zmień|zmien|utw[oó]rz|create|test|wait)', 'medium'),
 ('profile_edit',r'(?:profile|profil|featured|bio|skills).{0,150}(?:edit|zmień|zmien|uzupełn|uzupeln|dodaj|add|update)', 'medium'),
 ('website_edit',r'(?:wordpress|stron[ay]|website|site).{0,120}(?:edit|zmień|zmien|napraw|popraw|opublikuj|update)', 'medium'),
 ('export_data',r'(?:export|eksport|pobierz|download|zapisz plik).{0,130}(?:dane|data|list|csv|json|profile|tekst|text)', 'medium'),
 ('message_read',r'(?:read|odczytaj|przeczytaj|przejrzyj).{0,130}(?:wiadomoś|message|email|e-mail|mail|rozm[oó]w|chat)', 'medium'),
 ('list_audit',r'(?:tylko odczyt|read.only|audyt|audit|sprawdź|sprawdz|zweryfikuj|verify|inspect|list all|wypisz)', 'low'),
 ('navigation_search',r'(?:find|znajdź|znajdz|wyszukaj|search|locate|open)', 'low')]
def classify(s):
    s=re.sub(r'\s+',' ',str(s or '')).casefold()
    for label,pattern,confidence in rules:
        if re.search(pattern,s,re.I):return label,confidence
    return 'requires_manual_classification','low'
rows=[]
for op in R['operations']:
    if op['comparison_status']=='comparable':continue
    texts=[str(value or '') for rid in op['run_ids'] for value in (A.get(rid,{}).get('task_excerpt'),history.get(rid),ledger.get(rid))]
    txt=max(texts,key=len) if texts else ''
    label,confidence=classify(txt)
    rows.append({'operation_key':op['operation'],'service':op['service'],
        'old_intent':op['intent'],'new_activity':label,'confidence':confidence,
        'source_run_ids':op['run_ids'],'linked_task_ids':op['task_ids'],
        'task_text_preview':txt[:700],
        'has_explicit_continuation':bool(re.search(r'earlier task|previous task|kontynuuj|wzn[oó]w|resume|continue',txt,re.I)),
        'baseline_calls':None})
assert len(rows)==491
counts=collections.Counter(x['new_activity'] for x in rows)
output={'stage':7,'source':'stage6 excluded operations, original task text only, no costs read for classification',
 'unclassified_units_reviewed':len(rows),'by_intent':dict(counts),
 'high_or_medium_confidence':sum(x['confidence']=='medium' for x in rows),
 'still_requires_manual':counts['requires_manual_classification'],
 'task_inventory':rows,
 'note':'Descriptions are heuristic only. Not a claim of completed work; no baseline is assigned yet. Rule precedence can produce errors.'}
(P/'OPENFILL_COST_STAGE7_UNKNOWN_ACTIVITIES_20261102.json').write_text(json.dumps(output,ensure_ascii=False,indent=1),encoding='utf8')
with (P/'OPENFILL_COST_STAGE7_UNKNOWN_ACTIVITIES_20261102.csv').open('w',encoding='utf-8-sig',newline='') as f:
    fields=['operation_key','service','old_intent','new_activity','confidence','source_run_ids','linked_task_ids','has_explicit_continuation','task_text_preview']
    w=csv.DictWriter(f,fieldnames=fields);w.writeheader()
    for r in rows:w.writerow({k:';'.join(r[k]) if isinstance(r.get(k),list) else r.get(k) for k in fields})
(P/'OPENFILL_COST_STAGE7_UNKNOWN_ACTIVITIES_20261102.md').write_text(
    '# OpenFill: 491 operacji bez odniesienia normatywnego\n\n'+
    'To inwentarz CZYNNOŚCI z tekstów poleceń, bez wycen.\n\n'+
    '\n'.join(f'- {k}: {v}' for k,v in counts.most_common())+
    '\n\nBrak normy nie oznacza kosztu zero. Nie wyznaczano norm po poznaniu realnych kosztów.\n',encoding='utf8')
print('STAGE7_ACTIVITY_ONLY',len(rows),'MEDIUM',output['high_or_medium_confidence'],'MANUAL',output['still_requires_manual'])
print('BUCKETS',counts.most_common(18))



