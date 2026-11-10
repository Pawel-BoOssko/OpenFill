"""Final registered benchmark B01-B07: compare model calls and cost per fulfilled action."""
import json, pathlib, csv, datetime, statistics
ROOT=pathlib.Path(__file__).resolve().parents[1]
LOGS=pathlib.Path(r"C:\Users\pboru\OpenFill\data\stable\logs")
BASE=ROOT/"docs"/"2026-11-07_13-40-00_CET_GPT-6_OpenFill_WynikiSzescTestowProdukcji_WywolanTokenowNaCelIKosztuAkcji_PorownanieZPrognoza.json"
six=json.loads(BASE.read_text(encoding="utf-8"))["benchmarks"]
seventh_id="OF-20261107-1343-787e"
fin=None; task=None
for raw in (LOGS/"usage_202611.ndjson").open(encoding="utf-8",errors="replace"):
 try:d=json.loads(raw)
 except:continue
 if d.get("taskKey")==seventh_id:
  if d.get("kind")=="business_goal_finish":fin=d
  if d.get("kind")=="task":task=d
assert fin and task, "B07 usage event missing"
state_path=ROOT/"staging"/"of_benchmark_5post_state_20261107.json"
state=json.loads(state_path.read_text(encoding="utf-8"))
new=[("Nowe zamowienia","Wpis testowy numer 6."),("Analiza marzy","Wpis testowy numer 7."),
     ("Reklamacje","Wpis testowy numer 8."),("Czas kompletacji","Wpis testowy numer 9."),
     ("Dostawy kurierskie","Wpis testowy numer 10.")]
old=[("Koszt sprzedazy","Wpis testowy numer 1."),("Rotacja towarow","Wpis testowy numer 2."),
     ("Termin dostawy","Wpis testowy numer 3."),("Stany magazynowe","Wpis testowy numer 4."),
     ("Zwroty klientow","Wpis testowy numer 5.")]
def count_exact(pairs):
 return sum(1 for title,body in pairs if sum(x.get("title")==title and x.get("body")==body for x in state)==1)
assert len(state)==10 and count_exact(old)==5 and count_exact(new)==5, "Server state not exactly ten expected posts"
six["B07"]={
 "id":seventh_id,"tag":"B07","pred":12,"min":8,"max":17,
 "category":"five_posts_warm","units":5,"verified":5,
 "run_id":fin["runId"],"calls":fin["calls"],"input":fin["inputTokens"],
 "cached":fin["cachedTokens"],"output":fin["outputTokens"],
 "reasoning":fin["reasoningTokens"],"total":fin["inputTokens"]+fin["outputTokens"],
 "usd":fin["costUsd"],"status":fin["outcome"],"provider_units":fin.get("providerVerifiedUnits",0),
 "reported_units":fin.get("agentReportedUnits",0),"read_units":fin.get("readCompletedUnits",0),
 "tool_calls":task["toolCalls"],"tool_errors":task["toolErrors"],"repeated_reads":task["repeatedPageReadRequests"],
 "tools":{},"errors":[],"model_usage_events":[],"read_events":0
}
for key,r in six.items():
 r["calls_per_verified_action"]=round(r["calls"]/r["verified"],2) if r["verified"] else None
 r["tokens_per_verified_action"]=round(r["total"]/r["verified"],2) if r["verified"] else None
 r["deviation"]=r["calls"]-r["pred"]
 r["deviation_percent"]=round(100*r["deviation"]/r["pred"],1)
 r["within_range"]=r["min"]<=r["calls"]<=r["max"]
 r["uncached_input"]=r["input"]-r["cached"]
 r["cached_pct_input"]=round(100*r["cached"]/r["input"],1) if r["input"] else None
overall={
 "predicted_calls":sum(r["pred"] for r in six.values()),
 "actual_calls":sum(r["calls"] for r in six.values()),
 "completed_actions":sum(r["verified"] for r in six.values()),
 "all_tokens":sum(r["total"] for r in six.values()),
 "input":sum(r["input"] for r in six.values()),
 "cached":sum(r["cached"] for r in six.values()),
 "output":sum(r["output"] for r in six.values()),
 "usd":round(sum(r["usd"] for r in six.values()),8),
 "tool_errors":sum(r["tool_errors"] for r in six.values()),
 "successful_outcomes":sum(r["status"]=="success" for r in six.values())
}
overall["calls_deviation_pct"]=round(100*(overall["actual_calls"]/overall["predicted_calls"]-1),1)
by_run={r["run_id"]:k for k,r in six.items()}
for r in six.values():
 r["model_requests_attempted"]=0
 r["model_failed_requests"]=0
for line in (LOGS/"app_20261106.ndjson").open(encoding="utf-8",errors="replace"):
 try:evt=json.loads(line)
 except:continue
 tag=by_run.get(evt.get("runId"))
 if tag and evt.get("source")=="model":
  if evt.get("eventType")=="request":six[tag]["model_requests_attempted"]+=1
  elif evt.get("eventType")=="error":six[tag]["model_failed_requests"]+=1
overall["model_requests_attempted"]=sum(r["model_requests_attempted"] for r in six.values())
overall["model_failed_requests"]=sum(r["model_failed_requests"] for r in six.values())
data={"author":"GPT-6","created_cet":datetime.datetime.now().astimezone().isoformat(),
      "registered_before_execution":True,"sandbox_saved_posts":len(state),"overall":overall,"tests":six}

DOC=ROOT/"docs"
STEM="2026-11-07_13-46-00_CET_GPT-6_OpenFill_BenchmarkSiedmiuZadan_WywolanModeluTokenowIKosztuAkcji_OcenaSprawnosciProdukcji"
json_file=DOC/(STEM+".json")
csv_file=DOC/(STEM+".csv")
md_file=DOC/(STEM+".md")
json_file.write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding="utf-8")
with csv_file.open("w",encoding="utf-8-sig",newline="") as f:
 fields=["tag","category","status","pred","calls","model_requests_attempted","model_failed_requests","deviation","deviation_percent",
         "min","max","verified","calls_per_verified_action","input","cached","uncached_input",
         "output","reasoning","total","tokens_per_verified_action","usd","tool_calls",
         "tool_errors","repeated_reads","id","run_id"]
 writer=csv.DictWriter(f,fields,extrasaction="ignore");writer.writeheader()
 for key,r in six.items():writer.writerow(r)
def num(x):return f"{x:,.0f}".replace(","," ")
labels={"B01":"Odczyt jednej strony","B02":"IANA domeny",
        "B03":"Dwie biografie Wikipedii","B04":"Dokument Python",
        "B05":"Formularz Selenium","B06":"Pierwsze pięć wpisów",
        "B07":"Pięć kolejnych wpisów"}
lines=[
 "# OpenFill: siedem realnych testów efektywności modelu na cel i wykonaną akcję",
 "",
 "Data: 7 listopada 2026, Europe/Warsaw (CET). Autor: GPT-6. Host: PBOX1.",
 "Prognozy zapisane przed testami. Produkcyjny OpenFill LIVE, efekty lokalnego serwera weryfikowane niezależnie.",
 "",
 "## Wynik zbiorczy",
 "",
 f"- Przewidywane wywołania: {overall['predicted_calls']}; rzeczywiste: {overall['actual_calls']}; odchylenie +{overall['calls_deviation_pct']}%.",
 f"- Wszystkie tokeny: {num(overall['all_tokens'])}; input {num(overall['input'])}; cached input {num(overall['cached'])}; output {num(overall['output'])}.",
 f"- Koszt modeli: {overall['usd']:.8f} USD; błędy narzędzi: {overall['tool_errors']}.",
 f"- Wyniki OpenFill: {overall['successful_outcomes']} success, {len(six)-overall['successful_outcomes']} partial (B03 podał poprawne dane).",
 f"- Stan lokalnej aplikacji: {len(state)} wpisów, zgodnych 5 pierwszych i 5 kolejnych.",
 "",
 f"- Rozpoczęte żądania do modelu: {overall['model_requests_attempted']}, z odpowiedzią i pomiarem tokenów: {overall['actual_calls']}, z błędem: {overall['model_failed_requests']}.",
 "Nieudana próba wystąpiła w B03 na kroku 5: Cannot reach OpenAI: An error occurred while sending the request. Model ponowił krok i zwrócił poprawną treść, lecz bez wywołania finish, przez co raport OpenFill ma status partial.",
 "",
 "## Prognozy i realny koszt",
 "",
 "| Test | Prognoza calls | Rzeczywiste calls | Różnica | Tokeny celu | Efekty | Tokeny/efekt | Błędy narzędzi | Status |",
 "|---|---:|---:|---:|---:|---:|---:|---:|---|"
]
for key,r in six.items():
 lines.append(f"| {key}: {labels[key]} | {r['pred']} | {r['calls']} | {r['deviation']:+} | {num(r['total'])} | {r['verified']} | {num(r['tokens_per_verified_action'])} | {r['tool_errors']} | {r['status']} |")
lines += [
 "",
 "B01-B05: liczba wykonanych działań zweryfikowana ocennie na podstawie odpowiedzi i stanu przeglądarki. B03: jeden merytorycznie ukończony cel mimo statusu partial.",
 "B06 i B07: pięć nowych wpisów potwierdzonych niezależnie przez dziesięć unikalnych par tytuł/treść w pliku danych serwera testowego.",
 "",
 "## Konkretne przyczyny nadmiarowych wywołań",
 "",
 "1. B05: act_many zawierał identyfikator id_nonexistent, powodując NOT_FOUND. act zakończył się błędem action_not_attempted:postcheck_selector_required_or_too_broad. Formularz ostatecznie wysłano prawidłowo.",
 "2. B06: agent próbował zapisać kilka formularzy w jednej serii act_many. Każdy POST przeładowuje dokument, więc następne akcje na starym stanie CDP są zawodne. Dwa błędy Runtime.evaluate: Value does not fall within the expected range i dodatkowe odczyty podniosły liczbę calls do 18.",
 "3. B07: dwa błędy Runtime.evaluate i jeden NOT_FOUND w act_many; kolejne odczyty DOM i ponowienia. Zadanie zakończone, ale przy 19 calls, zatem strona już poznana nie dała obniżenia zużycia tokenów.",
 "4. B03: obie prawidłowe daty 1815 i 1791 oraz związek z maszyną analityczną, lecz outcome=partial. Jakość i status muszą być mierzone osobno.",
 "5. B02: agent prawidłowo wskazał, że example.net nie jest wymienione dosłownie w tekście IANA, tylko wynika ze źródłowego RFC 2606; nie zmyślił zawartości strony.",
 "",
 "## Porównanie pierwszej i powtórnej serii",
 "",
 f"- B06: {six['B06']['calls']} calls, {num(six['B06']['total'])} tokenów, {num(six['B06']['tokens_per_verified_action'])} tokenów na wpis.",
 f"- B07: {six['B07']['calls']} calls, {num(six['B07']['total'])} tokenów, {num(six['B07']['tokens_per_verified_action'])} tokenów na wpis.",
 f"- Różnica: {(six['B07']['total']/six['B06']['total']-1)*100:.1f}% tokenów na wpis. Hipoteza poprawy przez znajomość strony nie potwierdzona.",
 "",
 "## Rekomendowane poprawki",
 "",
 "P0: Dla formularzy kończących się przeładowaniem strony wykonywać jeden submit na aktualnym DOM, następnie sprawdzić efekt i dopiero zaczynać kolejny wpis. Nie kontynuować act_many na nieaktualnych identyfikatorach elementów.",
 "P0: Po błędzie Runtime.evaluate odczytać rzeczywisty skutek POST przed ponowieniem, aby nie duplikować działań.",
 "P1: Stosować najtańsze wystarczające potwierdzenie pojedynczej akcji zamiast kilku pełnych odczytów DOM.",
 "P1: Rozszerzyć detekcję goalType dla polecenia dodaj pięć wpisów: B07 błędnie otrzymał kategorię other i brak efektywnego dzielnika w standardowym raporcie, mimo kompletnego sukcesu.",
 "P1: Sprawdzić rozbieżność outcome partial przy kompletnej odpowiedzi B03.",
 "",
 "## Referencje i ograniczenia",
 "",
 "Prognozy przed wykonaniem: docs/2026-11-07_13-30-16_CET_GPT-6_OpenFill_PlanBenchmarkuWywolanModeluNaCelIKluczoweAkcje_PorownaniePrzewidywanIZmierzony.md.",
 "Źródło: produkcyjne usage_202611.ndjson i app_20261106.ndjson; szczegóły w plikach JSON i CSV o tym samym stemie.",
 "Sandbox: tools/2026-11-07_13-30-16_CET_GPT-6_OpenFill_PiecWpisowSandboxSerwer_DoPomiaruTokenowNaWpis_BezPublikacjiZewnetrznej.py; dane staging/of_benchmark_5post_state_20261107.json.",
 "W tym eksperymencie nie wdrażano zmian kodu agenta: wyniki dokumentują aktualne działanie, nie oszczędności uzyskane po optymalizacji.",
 ""
]
md_file.write_text("\n".join(lines),encoding="utf-8")
print("BENCHMARK_7_OK",overall)
print("FILES",json_file,csv_file,md_file)
