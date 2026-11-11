# OF-IMP-20261031 E01: intent mining from existing aggregate run audit, entirely on disk.
import collections
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "docs" / "OPENFILL_TOKEN_AUDIT_DATA_20261031.json"
OUT = ROOT / "docs" / "OPENFILL_INTENT_FREQUENCY_20261031.json"
REPORT = ROOT / "docs" / "OPENFILL_INTENT_FREQUENCY_20261031.md"

def classify(text, tools, domain):
    s = (text or "").lower()
    tools = tools or {}
    match = lambda pattern: bool(re.search(pattern, s))
    if tools.get("whatsapp_send", 0) > 0 or match(r"\b(send|wyslij|wyślij|napisz|message|wiadomo[sś][cć]|private message|dm)\b.*\b(to|do|na|contact|kontakt|adresat)"):
        return "send_message"
    if match(r"\b(zapro[sś]|invite|invitation|connect with|zaproszenie)\b"):
        return "send_invitation"
    if domain == "airbnb" and match(r"\b(price|pricing|ceny|cena|stawki|podnie[sś]|zmie[nń]|increase|decrease|discount|calendar|kalendarz)\b"):
        return "update_listing_or_price"
    if match(r"\b(publish|post|opublikuj|udost[eę]pnij|komentarz|comment)\b"):
        return "publish_or_comment"
    if match(r"\b(read|odczytaj|przeczytaj|poka[zż]|summari[sz]|history|histori[aeę]|conversation|conversations|messages|inbox|wiadomo[sś]ci|czat|chat)\b"):
        return "read_messages_or_data"
    if match(r"\b(find|search|znajd[zź]|poszukaj|szukaj|lookup|profile|profil|person|people|contact|kontakt|kogo)\b"):
        return "search_or_resolve"
    if match(r"\b(update|edit|change|zmie[nń]|zaktualizuj|edytuj|ustaw|configure|setting|formularz|fill)\b"):
        return "edit_record_or_settings"
    if match(r"\b(debug|diagnos|diagno|error|test|sprawd[zź]|problem|napraw|fix)\b"):
        return "diagnostic"
    if match(r"\b(extract|export|download|pobierz|eksport|zapisz|capture|scrape)\b"):
        return "extract_or_export"
    return "other_uncertain"

def normalize_site(site):
    s=(site or "").lower()
    if "linkedin" in s: return "linkedin"
    if "whatsapp" in s: return "whatsapp"
    if "airbnb" in s: return "airbnb"
    if "facebook" in s or "messenger" in s or "instagram" in s or "meta" in s: return "meta"
    if "youtube" in s: return "youtube"
    if "mail.google" in s or "gmail" in s: return "gmail"
    if "business.google" in s: return "google_business"
    if "google" in s: return "google_other"
    return "other"

data=json.loads(SRC.read_text(encoding="utf-8"))
runs=data["runs"]
groups=collections.defaultdict(lambda:{"tasks":0,"calls":0,"cost":0.0,"tokens":0,"cached":0,"uncached":0,"tool_calls":0,"examples":[]})
for run_id, item in runs.items():
    site=normalize_site(item.get("site"))
    task=item.get("task_preview") or item.get("task") or ""
    intent=classify(task,item.get("tool_counts"),site)
    key=(site,intent)
    group=groups[key]
    group["tasks"] += 1
    group["calls"] += int(item.get("calls") or 0)
    group["cost"] += float(item.get("costUsd") or 0)
    group["tokens"] += int(item.get("inputTokens") or 0)
    group["cached"] += int(item.get("cachedTokens") or 0)
    group["uncached"] += int(item.get("uncached") or 0)
    group["tool_calls"] += int(item.get("tool_calls") or 0) if isinstance(item.get("tool_calls"),int) else sum((item.get("tool_counts") or {}).values())
    if len(group["examples"]) < 5: group["examples"].append(run_id)

table=[]
for (site,intent),metric in groups.items():
    row={"site":site,"intent":intent,**metric}
    row["cost"]=round(row["cost"],4)
    row["model_calls_per_task"]=round(row["calls"]/row["tasks"],1)
    table.append(row)
table.sort(key=lambda r:(-r["calls"],-r["tasks"]))
OUT.write_text(json.dumps({"method":"heuristic intent classification from task text and specialized action counts","total_runs":len(runs),"rows":table},indent=2,ensure_ascii=False),encoding="utf-8")
lines=["# OpenFill: częstotliwość intencji z audytu zadań","",
       "Źródło: OPENFILL_TOKEN_AUDIT_DATA_20261031.json. Klasyfikacja heurystyczna oparta na poleceniach i dostępnych tool_counts. Wyniki są szacunkami, nie ręcznie potwierdzonymi etykietami każdej czynności.",
       "",
       "| Serwis | Intencja | Zadania | Model calls | Calls/zadanie | Koszt logowany USD |",
       "| --- | --- | ---: | ---: | ---: | ---: |"]
for r in table:
    lines.append(f"| {r['site']} | {r['intent']} | {r['tasks']} | {r['calls']} | {r['model_calls_per_task']} | {r['cost']:.4f} |")
lines+=["","Nie klasyfikować kategorią 'other_uncertain' jako pojedynczą procedurą. Każdego kandydata wdrożenia potwierdzić na reprezentatywnych run_id z wynikowego JSON.",""]
REPORT.write_text("\n".join(lines),encoding="utf-8")
print(json.dumps({"total_runs":len(runs),"groups":len(table),"top":table[:12],"json":str(OUT),"md":str(REPORT)},ensure_ascii=False))
