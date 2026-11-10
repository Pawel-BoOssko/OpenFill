import json, pathlib, collections, re, statistics, urllib.parse, datetime
base=pathlib.Path(r'C:\Users\pboru\OpenFill\data\stable\logs')
output=pathlib.Path(r'C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\docs')
rows={}; actions=collections.defaultdict(list); failures=collections.Counter(); parse_errors=collections.Counter()
for log in sorted(base.glob('app_202610*.ndjson')):
 for line in log.open(encoding='utf-8',errors='replace'):
  try: x=json.loads(line)
  except Exception: parse_errors[log.name]+=1; continue
  rid=x.get('runId'); src=x.get('source'); typ=x.get('eventType'); data=x.get('data') or {}
  if src=='session' and typ=='start' and rid:
   rows.setdefault(rid,{})['task_preview']=data.get('preview','')
  if src=='session' and typ=='end' and rid:
   rows.setdefault(rid,{})['outcome']=x.get('status'); rows[rid]['end_msg']=x.get('message')
  if src=='tool' and typ in ('call','result') and rid:
   name=data.get('name') or (x.get('message','').split(' ',1)[0])
   entry={'type':typ,'name':name,'status':x.get('status'),'msg':x.get('message','')[:180], 'args':data.get('args',{}) if typ=='call' else {},'preview':data.get('preview','')[:300]}
   actions[rid].append(entry)
  if x.get('status') in ('error','failed') and src!='network': failures[(src,typ,x.get('message','')[:120])]+=1
for line in (base/'usage_202610.ndjson').open(encoding='utf-8',errors='replace'):
 try:x=json.loads(line)
 except Exception:parse_errors['usage']+=1;continue
 rid=x.get('runId');
 if not rid:continue
 row=rows.setdefault(rid,{})
 if x.get('kind')=='task':
  row.update({k:x.get(k,0) for k in ('calls','inputTokens','cachedTokens','outputTokens','reasoningTokens','modelMs','costUsd')});row['task']=x.get('task','');row['date']=x.get('ts','')[:10]
 if x.get('kind')=='call':
  row.setdefault('calls_detail',[]).append({'step':x.get('step'),'tools':x.get('tools',[]),'input':x.get('inputTokens',0),'cached':x.get('cachedTokens',0),'output':x.get('outputTokens',0),'reasoning':x.get('reasoningTokens',0),'request_chars':x.get('requestChars',0),'cost':x.get('costUsd',0),'latency':x.get('latencyMs',0),'compactions':x.get('historyCompactions',0)})
for rid,r in rows.items():
 a=actions.get(rid,[]);r['tool_counts']=dict(collections.Counter(q['name'] for q in a if q['type']=='call'));r['tool_sequence']=[q['name'] for q in a if q['type']=='call'];r['tool_errors']=[q for q in a if q['type']=='result' and q['status'] in ('error','failed')];r['tool_calls']=len(r['tool_sequence'])
 urls=[]
 for q in a:
  if q['type']!='call':continue
  for v in (q.get('args') or {}).values():
   if isinstance(v,str):urls+=re.findall(r'https?://[^\s\"\u0027<>]+',v)
 r['domains']=dict(collections.Counter(urllib.parse.urlparse(u).hostname for u in urls if urllib.parse.urlparse(u).hostname))
 text=(r.get('task','')+' '+r.get('task_preview','')).lower()
 terms=[('WhatsApp',r'whatsapp|wa\.me|whats app'),('LinkedIn',r'linkedin'),('Airbnb',r'airbnb'),('Trello',r'trello'),('Google',r'google|gmail|maps\.google|youtube'),('Facebook/Meta',r'facebook|instagram'),('Booking',r'booking\.com'),('Medical/health',r'clinic|dermatolog|doctor|lekarz|szpital|hospital'),('Shopping',r'amazon|allegro|sklep|shopping|product|zakupy'),('Testing',r'example\.com|test page|qa |smoke|test zrzut')]
 r['site']=next((name for name,pat in terms if re.search(pat,text)),None)
 if not r['site'] and r['tool_sequence'] and any(q.startswith('whatsapp_') for q in r['tool_sequence']):r['site']='WhatsApp'
 if not r['site'] and r['domains']:
  r['site']=max(r['domains'],key=r['domains'].get)
 if not r['site']:r['site']='Other/unknown'
 r['repeated_tools']={name:n for name,n in r['tool_counts'].items() if n>=3}
 r['max_request_chars']=max((q['request_chars'] for q in r.get('calls_detail',[])),default=0)
 r['last_step_input']=r['calls_detail'][-1]['input'] if r.get('calls_detail') else 0
 r['uncached']=max(0,r.get('inputTokens',0)-r.get('cachedTokens',0))
usable=[(rid,r) for rid,r in rows.items() if r.get('calls')]
print('TOTAL_RUNS',len(rows),'USAGE_TASKS',len(usable),'PARSE_ERRORS',dict(parse_errors))
print('TOTAL', {k:round(sum(r.get(k,0) for _,r in usable),3) for k in ('calls','inputTokens','cachedTokens','outputTokens','reasoningTokens','costUsd','modelMs')})
print('BY_SITE')
site_groups=collections.defaultdict(list)
for rid,r in usable:site_groups[r['site']].append((rid,r))
site_summary=[]
for site,rr in sorted(site_groups.items(),key=lambda z:-sum(r.get('costUsd',0) for _,r in z[1])):
 n=len(rr);cost=sum(r.get('costUsd',0) for _,r in rr);calls=sum(r.get('calls',0) for _,r in rr)
 rec={'site':site,'runs':n,'calls':calls,'calls_per_task':round(calls/n,1),'cost_usd':round(cost,4),'avg_cost':round(cost/n,4),'input':sum(r['inputTokens'] for _,r in rr),'cached':sum(r['cachedTokens'] for _,r in rr),'uncached':sum(r['uncached'] for _,r in rr),'over_10_steps':sum(r['calls']>10 for _,r in rr),'retries_3plus':sum(bool(r['repeated_tools']) for _,r in rr),'max_input':max(r['last_step_input'] for _,r in rr)}
 site_summary.append(rec);print(json.dumps(rec,ensure_ascii=False))
print('TOP_COST_RUNS')
for rid,r in sorted(usable,key=lambda z:z[1].get('costUsd',0),reverse=True)[:25]:
 print(json.dumps({'run':rid,'date':r.get('date'),'site':r['site'],'calls':r['calls'],'cost':round(r['costUsd'],4),'input':r['inputTokens'],'cached':r['cachedTokens'],'last_step_input':r['last_step_input'],'max_req_chars':r['max_request_chars'],'tools':r['tool_counts'],'outcome':r.get('outcome'),'task':r.get('task','')[:180]},ensure_ascii=False))
print('TOP_UNCACHED_RUNS')
for rid,r in sorted(usable,key=lambda z:z[1].get('uncached',0),reverse=True)[:16]:
 print(json.dumps({'run':rid,'site':r['site'],'uncached':r['uncached'],'input':r['inputTokens'],'cached':r['cachedTokens'],'calls':r['calls'],'tools':r['tool_counts']},ensure_ascii=False))
print('TOOL_FREQUENCIES')
bytool=collections.Counter();by_site_tool=collections.defaultdict(collections.Counter)
for rid,r in usable:
 bytool.update(r['tool_counts']);by_site_tool[r['site']].update(r['tool_counts'])
print(json.dumps(bytool.most_common(40),ensure_ascii=False))
for site,cnt in sorted(by_site_tool.items(),key=lambda z:-sum(z[1].values()))[:13]:print(site,json.dumps(cnt.most_common(15),ensure_ascii=False))
print('BY_DAY')
byday=collections.defaultdict(list)
for rid,r in usable:byday[r.get('date','?')].append(r)
for d,rr in sorted(byday.items()):print(d,'runs',len(rr),'calls',sum(r['calls'] for r in rr),'cost',round(sum(r['costUsd'] for r in rr),4),'input',sum(r['inputTokens'] for r in rr))
result={'totals':{'runs':len(usable),'calls':sum(r['calls'] for _,r in usable),'cost_usd':sum(r['costUsd'] for _,r in usable)},'sites':site_summary,'runs':{rid:r for rid,r in usable},'tool_errors':dict((str(k),v) for k,v in failures.items()),'parse_errors':dict(parse_errors)}
path=output/'OPENFILL_TOKEN_AUDIT_DATA_20261031.json'
path.write_text(json.dumps(result,ensure_ascii=False,indent=1),encoding='utf-8')
print('DATA_PATH',str(path))
