import json,pathlib,statistics
ROOT=pathlib.Path(__file__).resolve().parents[1]
SRC=ROOT/"docs"/"of_action_accounting_20261106"
data=json.loads((SRC/"summary.json").read_text(encoding="utf-8"))
rows=[json.loads(s) for s in (SRC/"all_business_goals.jsonl").open(encoding="utf-8")]
T=data["total"]
def fmt(n):return f"{n:,.0f}".replace(","," ")
def price(n):return f"{n:.5f}"
lines=["# OpenFill: historyczne tokeny na cel użytkowy i wykonaną akcję","",
"Źródło: pełne logi produkcyjne usage i app NDJSON, październik i listopad 2026.",
"Stan pomiaru: "+data["period_last"],"",
"## Reguła rozliczenia","",
"Cały cel = suma wejścia i wyjścia modelu przez wszystkie powiązane próby i kontynuacje tego samego zamiaru, również nieudane.",
"Jedna potwierdzona akcja = całkowite tokeny tego celu / liczba faktycznie potwierdzonych efektów.",
"Jeżeli planowano 5 publikacji, ale potwierdzono 3, koszt wykonanej publikacji = całość / 3. Przy braku potwierdzenia nie ma wyniku jednostkowego.",
"Koszt nominalny = całość / liczba planowanych działań, wyłącznie jako plan, nigdy jako rzeczywista cena wykonania.",
"Tokeny rozumowania są podzbiorem wyjścia. Cached input jest podzbiorem wejścia.",
"",
"## Bilans wszystkich dostępnych danych","",
f"- Wywołania modelu: {fmt(T['calls'])}.",
f"- Tokeny wejściowe: {fmt(T['input'])}, z cache: {fmt(T['cache'])}, bez cache: {fmt(T['input']-T['cache'])}.",
f"- Tokeny wyjściowe: {fmt(T['output'])}, w tym rozumowania: {fmt(T['reasoning'])}.",
f"- Tokeny razem: {fmt(T['input']+T['output'])}. Koszt historyczny: {price(T['usd'])} USD.",
f"- Techniczne przebiegi: {data['raw_runs']}, zakończone rekordy: {data['terminal_records']}.",
f"- Grupy celów: {data['total_goals']} (prowizoryczne przy brakujących relacjach historycznych), jawne łańcuchy kontynuacji: {data['explicit_chained_goals']}.",
f"- Wywołania bez końcowego rekordu zadania: {data['unaccounted_calls']}. Ich tokeny są uwzględnione.",
""]
labels={"single_page_read":"Odczyt pojedynczej strony","research":"Badanie / wielostronicowy odczyt",
"registration":"Rejestracja konta lub zgłoszenie","publication":"Publikowanie wpisów / filmów / recenzji",
"invitation":"Wysyłanie zaproszeń","message":"Wysyłanie wiadomości",
"application":"Zgłoszenie aplikacji","site_setup":"Uruchomienie witryny",
"configuration_publish":"Publikowanie konfiguracji","form_or_update":"Zmiana danych / formularz","other":"Cel nierozpoznany z archiwalnego skrótu"}
lines+=["## Bilans według rodzaju celu","",
"| Typ celu | Grupy | Sukces według agenta | Tokeny wszystkie | Potwierdzone technicznie akcje | Akcje z raportu agenta | Tokeny na policzalną akcję* |",
"|---|---:|---:|---:|---:|---:|---:|"]
for cat,ss in data["categories"].items():
    cost=ss["input"]+ss["output"]
    per=ss.get("tokens_per_supported_unit")
    lines.append("| "+labels.get(cat,cat)+" | "+str(ss["goals"])+" | "+str(ss["successful_goals"])+" | "+
        fmt(cost)+" | "+str(ss["verified_units"])+" | "+str(ss["agent_reported_units"])+" | "+
        (fmt(per) if per is not None else "n/d")+" |")
lines+=["","*Koszt jednostkowy w tabeli dotyczy tylko grup, dla których liczba efektów jest znana. Efekty z raportów agenta nie są niezależnie zweryfikowane. Nie wolno dzielić kosztu wszystkich prób przez niewielką liczbę udokumentowanych akcji z innych prób.*","",
"## Przykłady całego zadania i jednej akcji","",
"| Zadanie | Liczba efektów | Wejście | Cache | Wyjście | Wszystkie tokeny | Tokeny na akcję | Podstawa |",
"|---|---:|---:|---:|---:|---:|---:|---|"]
def add_example(title,row):
    n=row["supported_completed_units"];per=row["tokens_per_supported_total"]
    lines.append("| "+title+" | "+str(n)+" | "+fmt(row["input_tokens"])+" | "+
        fmt(row["cached_tokens"])+" | "+fmt(row["output_tokens"])+" | "+
        fmt(row["total_tokens"])+" | "+(fmt(per) if per is not None else "n/d")+" | "+
        row["supported_evidence"]+" |")
def pick(cat,n=None,fragment=None):
    found=[r for r in rows if r["category"]==cat and r["supported_completed_units"]>0]
    if n is not None:found=[r for r in found if r["supported_completed_units"]==n]
    if fragment:found=[r for r in found if fragment in r["goal"].lower()]
    return sorted(found,key=lambda x:x["total_tokens"],reverse=True)
examples=[
 ("Facebook: publikacja 3 wpisów",pick("publication",3,"facebook")),
 ("Instagram: publikacja 3 wpisów",pick("publication",3,"instagram")),
 ("LinkedIn: wsad 5 zaproszeń",pick("invitation",5)),
 ("Rejestracja Cylex",pick("registration",1,"cylex")),
 ("Odczyt pojedynczej strony",pick("single_page_read",1)),
 ("Wysłanie pojedynczej wiadomości",pick("message",1))]
for title,matches in examples:
    if matches:add_example(title,matches[0])
lines+=["",
"Uwaga: przykłady publikacji i zaproszeń są ustalane z końcowego raportu agenta o tym, co widział w UI. Pełne tokeny obejmują przygotowanie, weryfikację i nieudane próby.",
"Raport agenta nie jest równoważny niezależnej kontroli każdego wpisu lub konta. Szczegóły poziomu dowodów pozostają w pliku wszystkich celów.",""]
five=[r for r in rows if r["category"]=="publication" and r["requested_units"]==5]
confirmed_five=[r for r in five if r["supported_completed_units"]==5]
lines+=["## Działania po pięć sztuk","",
f"- Cele publikacji z pięcioma zamówionymi wpisami widoczne w skróconych historycznych opisach: {len(five)}.",
f"- W tym cele z pięcioma potwierdzonymi albo zaraportowanymi efektami: {len(confirmed_five)}.",
"Nie przeliczam brakujących sztuk na wykonane wpisy. W danych szczegółowych pozostaje koszt nominalny 1/5 oraz odrębny koszt na faktycznie rozpoznaną akcję."]
newreads=[r for r in rows if r["category"]=="single_page_read" and r.get("first_observed_host") and r["supported_completed_units"]]
lines+=["","## Odczyt nieznanej dotąd witryny","",
f"- Odczyty jednej strony zakończone sukcesem i z dowodem live: {sum(r['supported_completed_units'] for r in rows if r['category']=='single_page_read')}.",
f"- Spośród nich odczyty hostów występujących po raz pierwszy w dostępnej historii zadań: {len(newreads)}."]
if newreads:
    lines.append("- Mediana tokenów na odczyt pierwszego zaobserwowanego hosta: "+fmt(statistics.median(r["total_tokens"] for r in newreads))+".")
lines+=["",
"To definicja operacyjna, nie wiedza o tym, czy model lub użytkownik znał witrynę wcześniej. Historyczne logi nie odtwarzają rzeczywistej wcześniejszej znajomości danej strony.",
"",
"## Granice dokładności rekonstrukcji","",
"1. Starsze przebiegi nie mają identyfikatorów powiązania kontynuacji. Rozliczenie celów bez jawnego łańcucha jest ostrożnym zbiorem osobnych prób, a nie gwarantowaną liczbą unikalnych projektów.",
"2. Zadania z długim poleceniem są w logach użycia skrócone. Dlatego część celów i liczby zamówionych sztuk pozostają nierozpoznane zamiast być zgadywane.",
"3. Sukces zgłoszony w końcowym podsumowaniu nie zawsze oznacza potwierdzoną akcję skutkową. Liczby z ledgeru, obserwacji UI i samego planu mają osobne pola.",
"4. W wieloetapowym celu koszt całego zadania jest znany dokładnie. Koszt pojedynczej różnorodnej czynności wewnątrz tego celu nie jest możliwy do precyzyjnego podziału bez telemetrii przypisującej wywołania do faz.",
"5. Cache wpływa na koszt USD, ale tokeny cached są nadal input tokens. Nie oblicza się oszczędności jako różnicy tokenów pomiędzy nieporównywalnymi celami.",
""]
lines+=["## Obowiązujący model pomiaru dla przyszłych zadań","",
"Każde zlecenie zapisuje nadrzędny cel biznesowy (np. zarejestruj konto X albo opublikuj 5 wpisów Y). Kolejne próby i uruchomienia dziedziczą ten sam cel.",
"Każdy efekt zapisuje typ akcji, identyfikator przedmiotu efektu, stan prepared/attempted/confirmed/uncertain oraz dowód weryfikacji.",
"Liczniki obejmują: requested, attempted, confirmed, partial, failed i skipped. Należy odróżnić dokonane skutki zewnętrzne od samego kliknięcia.",
"Zużycie modeli jest przypisywane do celu, a tam gdzie to możliwe także do aktualnej fazy: przygotowanie, wykonanie, kontrola. Wspólny narzut celu pozostaje wspólny.",
"Dla pięciu identycznych potwierdzonych publikacji koszt jednej = koszt wszystkich prób i całego zadania / 5; dla trzech potwierdzonych = /3. Dla mieszanych akcji nie wolno udawać, że dokładny koszt każdej wynika z podzielenia przez liczbę różnych efektów.",
"Najważniejsze wskaźniki: całkowite tokeny na zrealizowany cel, wejście bez cache, wyjście, koszt USD na cel i na potwierdzoną akcję, koszt prób niezakończonych sukcesem.",
"Porównania modeli i optymalizacji prowadzić na tych samych typach celów, z podobną liczbą akcji i stopniem trudności.",
"",
"## Pliki i odtwarzalność","",
"- Komplet 1726 prowizorycznych celów: docs/of_action_accounting_20261106/all_business_goals.jsonl.",
"- Bilans kategorii: docs/of_action_accounting_20261106/summary.json.",
"- Odtworzenie: python tools/of_action_accounting_20261106.py, następnie python tools/of_action_report_20261106.py.",
"- Ekstrakcja potwierdzonych i zgłoszonych skutków: tools/of_action_evidence_20261106.py.",
"- Historyczne identyfikatory run/task istnieją w JSONL tylko jako odnośniki diagnostyczne. Podstawą agregacji są cele i efekty.",
""]
DEST=ROOT/"docs"/"OPENFILL_ACTION_UNIT_ACCOUNTING_20261106.md"
DEST.write_text("\n".join(lines),encoding="utf-8")
print("SEMANTIC_ACTION_REPORT_OK",DEST)
print("EXAMPLE_COUNT",sum(1 for _,m in examples if m))
print("FIVE_POSTS",len(five),"FIVE_SUPPORTED",len(confirmed_five))
