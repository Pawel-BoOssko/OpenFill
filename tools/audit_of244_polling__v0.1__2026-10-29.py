# OpenFill - Metadata: wersja 0.1, data 2026-10-29 08:20
# Read-only MCP polling audit: aggregates method counts, never writes task content.
import json
import statistics
from collections import Counter, defaultdict
from datetime import datetime
from pathlib import Path

LOG_DIR = Path(r"C:\Users\pboru\OpenFill\data\stable\logs")
OUT = Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\docs\OpenFill__OF244_polling_audit__2026-10-29.md")
METHODS = (
    "openfill_task_status", "openfill_start_task", "openfill_info",
    "openfill_maintenance", "openfill_reply", "openfill_cancel_task",
    "openfill_full_status", "tools/list", "initialize"
)
counts = defaultdict(Counter)
status_times = defaultdict(list)
hourly_status = Counter()
bad = 0
all_requests = 0

for day in ("20261027", "20261028", "20261029"):
    file = LOG_DIR / f"app_{day}.ndjson"
    if not file.exists():
        continue
    with file.open("r", encoding="utf-8", errors="replace") as fp:
        for line in fp:
            try:
                rec = json.loads(line)
                if rec.get("source") != "mcp" or rec.get("eventType") != "request":
                    continue
                timestamp = datetime.fromisoformat(rec["tsUtc"].replace("Z", "+00:00"))
                message = rec.get("message") or ""
                method = next((x for x in METHODS if x in message), "other_or_unidentified")
                date = timestamp.strftime("%Y-%m-%d")
                counts[date][method] += 1
                all_requests += 1
                if method == "openfill_task_status":
                    status_times[date].append(timestamp)
                    hourly_status[timestamp.strftime("%Y-%m-%d %H:00 UTC")] += 1
            except (ValueError, KeyError, TypeError, json.JSONDecodeError):
                bad += 1

lines = [
    "# OF-244: Audyt wywołań MCP i okresu odpytywania",
    "",
    "Dane: app_20261027/28/29.ndjson, wyłącznie rekordy source=mcp i eventType=request.",
    "Bez treści zadań, task_id, domen i danych użytkowników.",
    "Pojedynczy log może łączyć ruch kilku klientów. Bez client_id nie da się wiarygodnie przypisać odstępów do konkretnego czatu.",
    "",
    "| Dzień UTC | Start | Status | Info | Inne | Razem |",
    "|---|---:|---:|---:|---:|---:|"
]
for d,c in sorted(counts.items()):
    lines.append(f"| {d} | {c['openfill_start_task']} | {c['openfill_task_status']} | {c['openfill_info']} | {sum(c.values())-c['openfill_start_task']-c['openfill_task_status']-c['openfill_info']} | {sum(c.values())} |")
lines.extend(["", "## Odstępy między kolejnymi statusami w całym ruchu", ""])
for day, t in sorted(status_times.items()):
    t.sort()
    gaps = [(b-a).total_seconds() for a,b in zip(t,t[1:])]
    nearest30 = sum(25 <= gap <= 35 for gap in gaps)
    nearest60 = sum(55 <= gap <= 65 for gap in gaps)
    lines.append(f"- {day}: {len(t)} wywołań status; odstępy {len(gaps)}; okolice 30s: {nearest30}; okolice 60s: {nearest60}; mediana {statistics.median(gaps):.1f}s" if gaps else f"- {day}: {len(t)} wywołań status, brak par do analizy odstępów")
lines.extend(["", "## Godziny o największej liczbie zapytań statusowych", "",
              "| Godzina UTC | Status calls |", "|---|---:|"])
for hour,n in hourly_status.most_common(15):
    lines.append(f"| {hour} | {n} |")
lines.extend(["", f"Łącznie rekordów MCP request: {all_requests}. Błędy parsowania: {bad}.",
              "Odstęp 30 sekund w logach nie dowodzi harmonogramu serwera: long-poll jest wywołaniem klienta, a licznik telemetrii jest lokalny.",
              "Wniosek przyczynowy wymaga identyfikacji klientów i porównania liczników przed/po zmianie kodu, nie wolno automatycznie utożsamiać ruchu z pętlą.",
              ""])
OUT.write_text("\n".join(lines), encoding="utf-8")
print("REPORT="+str(OUT))
print("REQUESTS="+str(all_requests)+" PARSE_ERRORS="+str(bad))
print("\n".join(lines[:20]))
