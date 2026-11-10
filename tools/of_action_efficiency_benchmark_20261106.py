import json,pathlib,collections,statistics,math
ROOT=pathlib.Path(__file__).resolve().parents[1]
SOURCE=ROOT/'docs'/'of_action_accounting_20261106'/'all_business_goals.jsonl'
TARGET=ROOT/'docs'/'OPENFILL_ACTION_EFFICIENCY_BENCHMARK_20261106.md'
goals=[json.loads(s) for s in SOURCE.open(encoding='utf-8')]
def fmt(x):return f'{x:,.0f}'.replace(',',' ')
def med(values):return statistics.median(values) if values else None
def pct(values,p):
    if not values:return None
    seq=sorted(values)
    return seq[math.ceil((len(seq)-1)*p)]
def label(x):
    text=(x.get('goal','')+' '+x.get('summary','')).lower()
    if 'facebook' in text:return 'facebook'
    if 'instagram' in text:return 'instagram'
    if 'linkedin' in text:return 'linkedin'
    if 'whatsapp' in text:return 'whatsapp'
    return 'other'
supported=[x for x in goals if x.get('supported_completed_units',0)>0]
unknown=[x for x in goals if x.get('supported_completed_units',0)==0]
groups=collections.defaultdict(list)
for r in supported:groups[(r['category'],label(r))].append(r)
lines=[
'# OpenFill: koszty celów i akcji, diagnostyka historyczna',
'',
'Stan danych: snapshot 2026-11-06, wejściowe dane z całego archiwum 5 października do 6 listopada.',
'Wszystkie wartości obejmują tokeny wejścia (również cache) oraz wyjścia.',
'Jednostki wykonane: tylko udokumentowane w ledgerze, odczycie live lub zaraportowane przez agenta. Dowód z raportu agenta nie zastępuje niezależnego sprawdzenia.',
'',
'## Rozkład tokenów na jedną opisaną akcję',
'',
'| Typ akcji | Usługa | Zadań z efektami | Potwierdzone/zgłoszone jednostki | Mediana tokenów na efekt | P90 |',
'|---|---|---:|---:|---:|---:|'
]
for (kind,site),items in sorted(groups.items(),key=lambda kv:-len(kv[1])):
    if len(items)<2:continue
    values=[x['tokens_per_supported_total'] for x in items]
    lines.append(f"| {kind} | {site} | {len(items)} | {sum(x['supported_completed_units'] for x in items)} | {fmt(med(values))} | {fmt(pct(values,.90))} |")
lines += [
'',
'## Koszt nierozstrzygniętych celów',
'',
f"- Cele z policzalną liczbą efektów: {len(supported)}.",
f"- Cele bez ustalonej liczby wykonanych efektów: {len(unknown)}.",
f"- Tokeny przypisane celom bez określonego efektu: {fmt(sum(x['total_tokens'] for x in unknown))}. Nie jest to automatycznie liczba tokenów zmarnowanych.",
'',
'Najdroższe nierozstrzygnięte grupy według zużycia tokenów:',
'',
'| Klasa | Outcome | Wywołania modelu | Tokeny | Krótki opis celu |',
'|---|---|---:|---:|---|'
]
for x in sorted(unknown,key=lambda r:r['total_tokens'],reverse=True)[:12]:
    preview=x['goal'].replace('|','/').replace('\n',' ')[:105]
    lines.append(f"| {x['category']} | {x['status']} | {x['calls']} | {fmt(x['total_tokens'])} | {preview} |")
lines += [
'',
'## Interpretacja',
'',
'1. Porównuj koszty tylko w podobnym typie zadania i przy podobnej liczbie wymaganych akcji. Rejestracja konta, krótki odczyt strony i pięć wpisów to różne produkty.',
'2. Do kosztu wykonanej akcji wlicz przygotowanie, ponawianie, autoryzację i sprawdzenie skutku. Nie utożsamiaj liczby wywołań modelu z liczbą czynności.',
'3. Nierozstrzygnięte cele są kandydatami do diagnostyki dowodów; nie nazywaj ich bez dowodu błędami i zmarnowanymi tokenami.',
'4. Wysokie P90 przy stabilnej medianie wskazuje duże wahania; przed optymalizacją trzeba rozpoznać dostępność konta, limity portalu, różne formularze i dodatkowe pytania.',
'5. Pełne dane źródłowe i ślady techniczne: docs/of_action_accounting_20261106/all_business_goals.jsonl.',
'6. Nowe wpisy telemetrii business_goal_start, business_goal_effect i business_goal_finish będą pozwalały rozliczać jednostkę automatycznie w kolejnych zadaniach.',
'']
TARGET.write_text('\n'.join(lines),encoding='utf-8')
print('ACTION_EFFICIENCY_BENCHMARK_OK',TARGET,'supported_goals',len(supported),'unknown_effects',len(unknown))
