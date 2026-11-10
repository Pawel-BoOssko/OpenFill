import csv, json, pathlib, re, collections, datetime, hashlib
from collections import defaultdict, Counter
ROOT=pathlib.Path(r'C:\Users\pboru\OpenFill\data\stable\logs')
OUT=pathlib.Path(__file__).resolve().parents[1]/'docs'/'of_action_accounting_20261106'
OUT.mkdir(parents=True,exist_ok=True)
RUNS={}
MISSING={"kind":Counter()}
def run_for(k):
    if k not in RUNS:
        RUNS[k]={"id":k,"calls":0,"input":0,"cache":0,"output":0,
                 "reasoning":0,"usd":0.0,"schema_chars":0,"task":"","task_key":"",
                 "continued_from":"","requester":"","outcome":"","stop_reason":"",
                 "first":"","last":"","tool_count":Counter(),"evidence":[],
                 "reported_summary":"","verified_ids":set(),"read_ok":0}
    return RUNS[k]
for file in sorted(ROOT.glob("usage_*.ndjson")):
    for raw in file.open(encoding="utf-8",errors="replace"):
        try:row=json.loads(raw)
        except json.JSONDecodeError:continue
        kind=row.get("kind")
        if kind not in ("call","task"): MISSING["kind"][kind]+=1;continue
        k=row.get("runId")
        if not k:continue
        r=run_for(k)
        if not r["first"]:r["first"]=row.get("ts","")
        r["last"]=row.get("ts","")
        r["task"]=row.get("task") or r["task"]
        if kind=="call":
            r["calls"]+=1
            for src,dest in (("inputTokens","input"),("cachedTokens","cache"),
                             ("outputTokens","output"),("reasoningTokens","reasoning")):
                r[dest]+=row.get(src) or 0
            r["usd"]+=row.get("costUsd") or 0
            r["schema_chars"]+=row.get("toolSchemaChars") or 0
            r["tool_count"].update(row.get("tools") or [])
        else:
            r["task_key"]=row.get("taskKey") or r["task_key"]
            r["continued_from"]=row.get("continuedFrom") or ""
            r["requester"]=row.get("requesterChat") or ""
            r["outcome"]=row.get("outcome") or r["outcome"]
            r["stop_reason"]=row.get("stopReason") or ""
            r["task_record"]=row
            r["task_records"]=r.get("task_records",0)+1
for file in sorted(ROOT.glob("app_*.ndjson")):
    for raw in file.open(encoding="utf-8",errors="replace"):
        try:e=json.loads(raw)
        except json.JSONDecodeError:continue
        k=e.get("runId")
        if k not in RUNS:continue
        r=RUNS[k]
        source=e.get("source"); event=e.get("eventType")
        data=e.get("data") or {}
        if source=="app" and event=="tab":
            r["task_key"]=r["task_key"] or data.get("taskKey") or ""
        if source=="session" and event=="end":
            r["outcome"]=r["outcome"] or data.get("status") or ""
            r["reported_summary"]=str(data.get("summary") or "")[:5000]
        if source=="tool" and event=="call":
            name=data.get("name","")
            if name=="finish":
                args=data.get("args") or {}
                r["reported_summary"]=r["reported_summary"] or str(args.get("summary") or "")[:5000]
            if name=="report_verified_action":
                args=data.get("args") or {}
                r.setdefault("effect_attempts",[]).append(args)
        if source=="tool" and event=="result":
            name=data.get("name",""); status=e.get("status")
            if name in ("get_page","read_text","read_view","navigate",
                        "page_capture","verify_view") and status=="ok":
                r["read_ok"]+=1
            if name=="report_verified_action" and status=="ok":
                args=(r.get("effect_attempts") or [{}])[-1]
                key=str(args.get("recipient",""))+"|"+str(args.get("confirmation_text",""))
                if key not in r["verified_ids"]:
                    r["verified_ids"].add(key)
                    r["evidence"].append(("verified_action",1,key[:60]))
            if name in ("whatsapp_send_batch","linkedin_send_batch") and status=="ok":
                preview=str(data.get("preview") or "")
                batch=re.search(r'"batch_id"\s*:\s*"([^"]+)"',preview)
                verified=re.search(r'"verified"\s*:\s*(\d+)',preview)
                if batch and verified:
                    key=name+"|"+batch.group(1)
                    if key not in r["verified_ids"]:
                        r["verified_ids"].add(key)
                        r["evidence"].append((name,int(verified.group(1)),key))
from urllib.parse import urlparse
def intent(raw):
    s=(raw or "").strip()
    if s.lower().startswith("context: this task continues"):
        m=re.search(r'Earlier task:\s*["“](.+)',s,re.I|re.S)
        if m:s=m.group(1).strip(' "”')
    return re.sub(r"\s+"," ",s)
def category(raw):
    s=intent(raw).lower()
    if re.search(r"^(tylko odczyt|read.only)",s):return "site_read_or_research"
    if re.search(r"rejestracj[ęa]|zarejestr|załóż konto|utwórz konto|create account|sign.up|register\b",s):return "registration"
    if re.search(r"wyślij.{0,60}zaprosze|send.{0,60}invitation|invite.{0,40}contact",s):return "invitation"
    if re.search(r"wyślij.{0,50}wiadomo|send.{0,50}message|wyślij.{0,50}sms|odpowiedz.{0,50}wiadomo",s):return "message"
    if "google tag manager" in s or "gtm kontener" in s:return "configuration_publish"
    if re.search(r"opublikuj|publikuj|umieść.{0,40}wpis|dodaj.{0,35}(wpis|post|komentarz)|publish|post.{0,22}(comment|post)|write.{0,22}comment",s):
        if not re.search(r"^(read.only|tylko odczyt)",s):return "publication"
    if re.search(r"prześlij.{0,30}plik|załącz.{0,30}plik|upload.{0,30}file",s):return "file_upload"
    if re.search(r"złóż.{0,30}aplikacj|apply.{0,40}job|submit.{0,30}application",s):return "application"
    if re.search(r"forum|hosting|serwer[0-9]|dns|phpbb|\bsmf\b|domen[ayę]",s) and re.search(r"uruchom|skonfigur|ustaw|utwórz|zainstaluj|przygotuj|włącz|dokończ|deploy|configure|create|install",s):
        return "site_setup"
    if re.search(r"otwórz|odczytaj|przeczytaj|sprawdź|przejrzyj|znajdź|wyszukaj|pobierz|analizuj|read|browse|collect|inspect|research|find|search|retrieve|open ",s):
        return "site_read_or_research"
    if re.search(r"zmień|zapisz|ustaw|wprowadź|edytuj|fill|update|change|set ",s):return "form_or_update"
    return "other"
def requested_units(raw,cat):
    if cat not in ("publication","invitation","message","registration","site_read_or_research","file_upload"):return None
    s=intent(raw).lower()
    if cat=="registration":return 1
    units=r"wpis|post|komentarz|comment|zaprosze|invitation|wiadomo|message|artykuł|article|reel|film|plik|file"
    m=re.search(r"(\d{1,3})\s*(?:nowych\s+)?(?:"+units+r")",s)
    if m:return int(m.group(1))
    if cat=="invitation":
        m=re.search(r"lista ma (\d+) pozycji|łącznie najwyżej (\d+)|najwyżej (\d+) zaprosze",s)
        if m:return int(next(x for x in m.groups() if x))
    for word,num in (("pięć",5),("five",5),("cztery",4),("four",4),("trzy",3),("three",3),("dwa",2),("dwie",2),("dwóch",2),("two",2),("jedną",1),("one",1)):
        if re.search(r"\b"+re.escape(word)+r"\s+(?:"+units+r")",s):return num
    if re.search(r"wszystki|every|all\b|kilka|several|multiple|listę",s):return None
    return 1 if cat in ("message","file_upload") else None
def first_host(task):
    found=re.search(r"https?://[^\s<>'\"\]\)]+",intent(task),re.I)
    if not found:return ""
    try:return urlparse(found.group(0).rstrip(".,;")).hostname or ""
    except ValueError:return ""
KEY_TO_RUN={r["task_key"]:rid for rid,r in RUNS.items() if r["task_key"]}
def root_for(r):
    key=r["task_key"]
    if not key:return "unlinked:"+r["id"],"no_explicit_lineage"
    seen={key}
    while key in KEY_TO_RUN:
        parent=RUNS[KEY_TO_RUN[key]].get("continued_from","")
        if not parent or parent in seen:break
        seen.add(parent);key=parent
    return "chain:"+key, "explicit_lineage" if len(seen)>1 else "independent"
GROUPS={}
for runid,r in RUNS.items():
    root,lineage=root_for(r)
    if root not in GROUPS: GROUPS[root]=[]
    GROUPS[root].append(r)
ROWS=[]
for root,items in GROUPS.items():
    items.sort(key=lambda v:v["first"])
    original=next((v for v in items if not intent(v["task"]).lower().startswith("context:")),items[0])
    task=intent(original["task"])
    typ=category(task)
    host=first_host(task)
    evidence={}
    for v in items:
        for name,n,key in v["evidence"]: evidence[name+"|"+key]=(name,n,key)
    verified_effects=sum(e[1] for e in evidence.values())
    calls=sum(v["calls"] for v in items)
    total={k:sum(v[k] for v in items) for k in ("input","cache","output","reasoning","usd","schema_chars")}
    last=items[-1]
    success=any(v["outcome"].lower() in ("success","completed") for v in items)
    row={"goal_index":len(ROWS)+1,"goal":task[:250],"category":typ,"host":host,
         "start":items[0]["first"],"end":last["last"],"runs":len(items),
         "lineage":root_for(items[-1])[1],"status":last["outcome"] or "unknown",
         "any_success":success,"calls":calls,"requested_units":requested_units(task,typ),
         "verified_effect_units":verified_effects,"read_evidence":sum(v["read_ok"] for v in items),
         "input_tokens":total["input"],"cached_tokens":total["cache"],
         "uncached_tokens":total["input"]-total["cache"],
         "output_tokens":total["output"],"reasoning_tokens_in_output":total["reasoning"],
         "total_tokens":total["input"]+total["output"],"usd":round(total["usd"],8),
         "schema_chars_sent":total["schema_chars"],
         "trace_run_ids":[v["id"] for v in items],
         "trace_task_keys":[v["task_key"] for v in items if v["task_key"]],
         "effects":list(evidence.values())[:20],
         "summary":last["reported_summary"][:5000]}
    ROWS.append(row)
ROWS.sort(key=lambda v:(v["start"],v["goal_index"]))
seen_hosts=set()
for row in ROWS:
    if row["category"]=="site_read_or_research":
        urls=re.findall(r"https?://[^\s<>'\"\]\)]+",row["goal"])
        if len(urls)==1 and "(+" not in row["goal"] and re.search(r"tytuł|title|nagłówek|h1|read.*page|odczytaj.*stron|open.*page",row["goal"],re.I):
            row["category"]="single_page_read"
        else:row["category"]="research"
    if row["host"]:
        row["first_observed_host"]=row["host"].lower() not in seen_hosts
        seen_hosts.add(row["host"].lower())
    else:row["first_observed_host"]=None
    if row["category"]=="single_page_read" and row["any_success"] and row["read_evidence"]>0:
        row["verified_read_units"]=1
    else:row["verified_read_units"]=0
    n=row["verified_effect_units"]+row["verified_read_units"]
    row["verified_completed_units"]=n
    row["tokens_per_verified_unit"]=round(row["total_tokens"]/n,1) if n else None
    row["usd_per_verified_unit"]=round(row["usd"]/n,7) if n else None
for row in ROWS:
    events=row["effects"]
    has_batch=any(e[0].endswith("_send_batch") for e in events)
    if has_batch:
        n=sum(e[1] for e in events if e[0].endswith("_send_batch"))
    elif row["category"] in ("message","invitation"):
        n=sum(e[1] for e in events if e[0]=="verified_action")
    else:n=0
    row["verified_effect_units"]=n
    denom=n+row["verified_read_units"]
    row["verified_completed_units"]=denom
    row["tokens_per_verified_unit"]=round(row["total_tokens"]/denom,2) if denom else None
    row["usd_per_verified_unit"]=round(row["usd"]/denom,8) if denom else None
    row["unit_evidence"]="provider_ledger" if n else "success_and_live_read" if row["verified_read_units"] else "unverified"
    planned=row["requested_units"]
    row["tokens_per_requested_unit_nominal"]=round(row["total_tokens"]/planned,2) if planned else None
    row["usd_per_requested_unit_nominal"]=round(row["usd"]/planned,8) if planned else None
    row["reported_success_unit"]=bool(row["any_success"] and row["category"]=="registration")
from of_action_evidence_20261106 import reported_units
for row in ROWS:
    n,source=reported_units(row["category"],row["requested_units"],
                            row["summary"],row["status"])
    if row["requested_units"] is not None and n>row["requested_units"]:
        n=0;source="count_exceeds_requested"
    row["agent_reported_units"]=n
    row["agent_evidence_type"]=source
    row["supported_completed_units"]=row["verified_completed_units"] or n
    row["supported_evidence"]=row["unit_evidence"] if row["verified_completed_units"] else source if n else "none"
    units=row["supported_completed_units"]
    for field,key in (("total_tokens","total"),("input_tokens","input"),
                      ("cached_tokens","cached"),("uncached_tokens","uncached"),
                      ("output_tokens","output")):
        row["tokens_per_supported_"+key]=round(row[field]/units,2) if units else None
    row["usd_per_supported_unit"]=round(row["usd"]/units,8) if units else None
bycat=defaultdict(list)
for row in ROWS:bycat[row["category"]].append(row)
summary={}
for cat,items in sorted(bycat.items()):
    n=len(items); success=sum(bool(x["any_success"]) for x in items)
    unit=sum(x["verified_completed_units"] for x in items)
    complete_goal=[x for x in items if x["any_success"]]
    summary[cat]={"goals":n,"successful_goals":success,
        "calls":sum(x["calls"] for x in items),
        "input":sum(x["input_tokens"] for x in items),
        "cached":sum(x["cached_tokens"] for x in items),
        "output":sum(x["output_tokens"] for x in items),
        "usd":round(sum(x["usd"] for x in items),6),
        "verified_units":unit,
        "agent_reported_units":sum(x["agent_reported_units"] for x in items if not x["verified_completed_units"]),
        "supported_units":sum(x["supported_completed_units"] for x in items),
        "supported_goals":sum(x["supported_completed_units"]>0 for x in items),
        "tokens_per_supported_unit":round(sum(x["total_tokens"] for x in items if x["supported_completed_units"])/sum(x["supported_completed_units"] for x in items),2) if sum(x["supported_completed_units"] for x in items) else None,
        "tokens_per_verified_unit":round(sum(x["total_tokens"] for x in items if x["verified_completed_units"])/unit,2) if unit else None,
        "mean_tokens_per_successful_goal":round(sum(x["total_tokens"] for x in complete_goal)/success,1) if success else None,
        "unknown_unit_goals":sum(x["verified_completed_units"]==0 for x in items)}
totals={k:sum(x[k] for x in RUNS.values()) for k in ("calls","input","cache","output","reasoning","usd")}
data={"period_first":min((r["first"] for r in RUNS.values() if r["first"]),default=""),
      "period_last":max((r["last"] for r in RUNS.values() if r["last"]),default=""),
      "raw_runs":len(RUNS),"terminal_records":sum(bool(r.get("task_record")) for r in RUNS.values()),
      "explicit_chained_goals":sum(len(v)>1 for v in GROUPS.values()),
      "total_goals":len(ROWS),"total":totals,"categories":summary,
      "unaccounted_calls":sum(r["calls"] for r in RUNS.values() if not r.get("task_record")),
      "unaccounted_goals":sum(x["status"]=="unknown" for x in ROWS)}
with (OUT/"all_business_goals.jsonl").open("w",encoding="utf-8") as f:
    for row in ROWS:f.write(json.dumps(row,ensure_ascii=False)+"\n")
(OUT/"summary.json").write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding="utf-8")
print("GOAL_ACCOUNTING_OK",data["total_goals"],"raw_runs",data["raw_runs"])
print("GLOBAL_CALLS",totals["calls"],"INPUT",totals["input"],"CACHE",totals["cache"],"OUTPUT",totals["output"])
for cat,s in summary.items():
    print(cat,"goals",s["goals"],"success",s["successful_goals"],"verified_units",s["verified_units"],
          "calls",s["calls"],"tokens_per_unit",s["tokens_per_verified_unit"])
