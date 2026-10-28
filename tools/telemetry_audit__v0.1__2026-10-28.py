# OpenFill - Metadata: wersja 0.1, data 2026-10-28 22:21
# Audit read-only telemetry; no changes to sampling or live OpenFill.
import json
import statistics
from collections import defaultdict
from datetime import datetime
from pathlib import Path

ROOT = Path(r"C:\Users\pboru\OpenFill\data\stable\logs\telemetry")
OUT = Path(r"C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\docs\OpenFill__telemetry_audit__v0.1__2026-10-28.md")
FILES = [ROOT / f"resources_{day}.ndjson" for day in ("20261027", "20261028")]
groups = defaultdict(list)
by_pid = defaultdict(list)
errors = 0
for file in FILES:
    with file.open(encoding="utf-8") as h:
        for line in h:
            try:
                row = json.loads(line)
                date = datetime.fromisoformat(row["tsUtc"].replace("Z", "+00:00"))
                pid = row["rootPid"]
                key = (pid, int(row.get("tabs") or 0), int(row.get("activeTasks") or 0))
                groups[key].append((date, row))
                by_pid[pid].append((date, row))
            except (ValueError, KeyError, TypeError, json.JSONDecodeError):
                errors += 1

def stat(vals):
    vals = [float(v) for v in vals if v is not None]
    if not vals:
        return "n/a"
    return f"{statistics.mean(vals):.2f} (p95={sorted(vals)[min(len(vals)-1, int(0.95*len(vals)))]:.2f})"

lines = [
    "# Audyt telemetrii OpenFill, 27-28.10.2026",
    "Źródło: pliki resources_*.ndjson, bez dodatkowego próbkowania i bez zmian w LIVE.",
    "CPU: cpuPctMachine jest udziałem całej maszyny z 4 logicznymi CPU. P95 dotyczy pojedynczych próbek.",
    "Pamięć workingSetMb to suma roboczych zestawów procesów, nie unikatowe fizyczne strony RAM.",
    "",
    "| Root PID | Karty | Aktywne | Próbki | Czas UTC | CPU avg (p95), % | WS avg (p95), MB | WebView2 avg |",
    "|---:|---:|---:|---:|---|---|---|---:|",
]
for key, rows in sorted(groups.items(), key=lambda kv:(kv[0][0],kv[0][1],kv[0][2])):
    if len(rows) < 5:
        continue
    rows.sort(key=lambda x:x[0])
    pid, tabs, active = key
    lines.append(f"| {pid} | {tabs} | {active} | {len(rows)} | {rows[0][0].strftime('%m-%d %H:%M')} to {rows[-1][0].strftime('%m-%d %H:%M')} | {stat(x[1].get('cpuPctMachine') for x in rows)} | {stat(x[1].get('workingSetMb') for x in rows)} | {statistics.mean(x[1].get('webViewProcessCount',0) for x in rows):.1f} |")
lines.extend(["", "## Ciągłość PID i próbki", ""])
for pid,rows in sorted(by_pid.items()):
    rows.sort(key=lambda x:x[0])
    gaps = [(b[0]-a[0]).total_seconds() for a,b in zip(rows,rows[1:])]
    over = [round(x) for x in gaps if x > 90]
    first,last = rows[0][0], rows[-1][0]
    lines.append(f"- PID {pid}: {len(rows)} próbek, {first.isoformat()} do {last.isoformat()}, median interwału {statistics.median(gaps) if gaps else 0:.1f}s, luki >90s: {len(over)}, maksymalna luka {max(gaps) if gaps else 0:.1f}s")
lines.extend(["",f"Nieczytelne lub odrzucone rekordy: {errors}.","Nie porównywać średnich z różnych PID bez uwzględnienia liczby kart i aktywności.", ""])
OUT.write_text("\n".join(lines), encoding="utf-8")
print(f"REPORT={OUT}")
print(f"GROUPS={len(groups)} PIDS={len(by_pid)} ERRORS={errors}")
print("\n".join(lines[:20]))
