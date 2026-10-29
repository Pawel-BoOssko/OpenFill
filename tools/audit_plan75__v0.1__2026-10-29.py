# Read-only audit of historical 75-step plan, 2026-10-29
import re, json
from pathlib import Path
from collections import Counter
R=Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill")
F=R/"docs"/"OpenFill__plan_dalszych_prac__v2.0__2026-10-23_1259.md"
MAIN=R/"docs"/"OPENFILL_AUDYT_PELNEGO_BACKLOGU_2026-10-29.json"
OUT=R/"docs"/"OPENFILL_AUDYT_PLANU_75_KROKOW_2026-10-29.md"
rows={}
for i,l in enumerate(F.read_text(encoding="utf-8").splitlines(),1):
    m=re.match(r"^\*\*(\d{2})\.\s+\[OF-([^]]+)\]\s+(.*)",l)
    if not m: continue
    num=int(m.group(1))
    if num<=75:
        rows[num]={"step":num,"line":i,"refs":m.group(2),"requirement":m.group(3)}
DON=set([1,4,6,7,8,10,11,13,14,15,20,24,25,26,35,36,61,66,68])
NOT=set([16,19,22,29,30,31,38,39,40,41,49,54,55,56,58,62,63,73,74])
ids={v["id"]:v for v in json.loads(MAIN.read_text(encoding="utf-8"))["entries"]}
for n,row in rows.items():
    refids=list(dict.fromkeys(re.findall(r"OF-\d{3}", "OF-"+row["refs"])))
    matches=[ids[x] for x in refids if x in ids]
    if n in DON: state="ZROBIONE"
    elif n in NOT: state="NIEZROBIONE"
    else: state="CZĘŚCIOWO ZROBIONE"
    row["state"]=state
    row["ref_states"]=", ".join(f"{v['id']}:{v['state']}" for v in matches)[:150]
    if 29<=n<=31:
        row["proof"]="ANULOWANE: zastępczy MCP OF-239 został usunięty decyzją właściciela 29.10; brak wdrożenia jest poprawny."
    elif state=="ZROBIONE":
        row["proof"]="Dowód w LIVE/SOURCE i checkpointcie: "+(matches[0]["evidence"][:145] if matches else "zapis wykonania w checkpointcie projektu.")
    elif state=="CZĘŚCIOWO ZROBIONE":
        row["proof"]="Związany kod lub etap jest w źródłach, ale nie udowodniono całego osobnego kryterium tego kroku. "+(matches[0]["evidence"][:140] if matches else "")
    else:
        row["proof"]="Brak wykonanego wymaganego testu/odbioru tego kroku; wcześniejszy plan nie jest dowodem wykonania."
count=Counter(x["state"] for x in rows.values())
lines=["# Audyt historycznego planu 75 kroków: stan wykonania",
"","Data 2026-10-29. Zawiera kroki planu v2.0 z 23.10.2026. Nie zastępuje kanonicznego backlogu OF i nie przywraca anulowanych prac.",
f"**Liczba kroków:** {len(rows)}. ZROBIONE: {count['ZROBIONE']}. CZĘŚCIOWO ZROBIONE: {count['CZĘŚCIOWO ZROBIONE']}. NIEZROBIONE: {count['NIEZROBIONE']}.",
"","| Krok | Powiązane OF | Status | Zakres | Dowód/brak |","|---:|---|---|---|---|"]
def esc(s):return str(s).replace("|","/").replace("\n"," ").strip()
for n,row in sorted(rows.items()):
    lines.append(f"| {n:02d} | {esc(row['refs'])} | {row['state']} | {esc(row['requirement'][:115])} | {esc(row['proof'])} |")
lines+=["","Zmiana decyzji właściciela 29.10: krok 06 dawnej instrukcji o zakazie restartu przy płatności nie stanowi już blokady publikacji; aktualizacja przerywa wszystkie zadania po bezpiecznym preflight. Kroki 29–31 oraz OF-239 anulowane.",""]
OUT.write_text("\n".join(lines),encoding="utf-8")
print("PLAN_ROWS="+str(len(rows))+" COUNTS="+json.dumps(dict(count),ensure_ascii=False))
print("OUTPUT="+str(OUT))
print("MISSING="+str(sorted(set(range(1,76))-set(rows))))
