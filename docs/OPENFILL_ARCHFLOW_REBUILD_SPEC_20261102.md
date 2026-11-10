# OpenFill: projekt przebudowy przepływu model, przeglądarka, dowód wykonania
Data: 2026-11-02. Status: projekt techniczny, nie wdrożenie nowego egzekutora.

## 1. Stan rzeczywisty, potwierdzony w SOURCE

Kod:
- `src/OpenFill.Core/Agent/AgentRunner__v0.29__2026-10-29_2314.cs`, `RunAsync` około wierszy 155-385.
- `src/OpenFill.Core/Model/OpenAIClient__v0.2__2026-10-05_1200.cs`: HTTP POST `/responses`.
- `src/OpenFill.Core/Agent/ToolRegistry__v0.22__2026-10-29_2314.cs`, `Build`, `act_many`, `act_locator`, `verify_view`, `read_view`, `navigate`.
- `src/OpenFill.Core/Browser/BrowserController__v0.10__2026-10-29_2314.cs`, `NavigateAsync`, `ExtractAsync`, `ActAsync`, `ActManyAsync`, `RunJsAsync`; CDP i WebView2.
- `src/OpenFill.Core/Agent/SemanticViewDigest__v0.1__2026-11-01.cs`, projekcja renderowanego DOM.
- `src/OpenFill.Core/Agent/PromptHistoryCompactor__v0.1__2026-10-23_1054.cs`, kompresja ukończonych tur.

Obecna sekwencja:
```text
User/MCP -> AppHost -> AgentRunner.RunAsync
  -> początkowy widok WhatsApp lub generic + site memory
  -> request JSON: model, instructions, input, tools, tool_choice=auto,
     parallel_tool_calls=false, store=false, prompt_cache_key, reasoning, include
  -> OpenAIClient -> POST /responses
  <- response.output: reasoning, message, function_call
  -> ToolRegistry.Build: handler(name, arguments)
  -> BrowserController -> CdpSession -> WebView2/CDP -> DOM/UI
  <- ToolResult.Output / opcjonalny screenshot
  -> AgentRunner: dopisanie function_call_output, screenshot do input
  -> NOWE POST /responses, z historią i pełnymi schematami narzędzi
```
Każde kolejne działanie wymagające decyzji modelu uruchamia nowe HTTP POST. Caching prefiksu i kompresja historii istnieją, lecz nie eliminują tej rundy. `act_many` już wykonuje lokalnie do 40 kolejnych działań, lecz wymaga gotowych lokatorów i nie jest ogólnym egzekutorem warunkowym. `navigate(view_mode=content)`, `read_view`, `verify_view` i lokalne polling także już istnieją. Nie traktować ich jako nowej architektury.

## 2. Diagnoza oparta na logach

Dane: `docs/OPENFILL_ARCHFLOW_TRACE_DIAGNOSTICS_20261102.json`, wejście stage14 non-Airbnb, pełne ledger usage_202610/202611.
- 1550 operacji, 1574 runId, 76566 model calls; 76534 wywołania funkcji, 0 odpowiedzi z wieloma funkcjami w tym zbiorze.
- `get_page` 20591, `act` 17182, `act_many` 1558, `wait` 6031, `run_js` 9014.
- Następstwo `act -> get_page` 10238, czyli 59.7% spośród 17148 `act` z następną rundą. `get_page -> act` 9039; `wait -> get_page` 4562; `run_js -> run_js` 4059.
- Liczba model calls na operację: mediana 28, p90 116, p95 160, maksimum 540.
- Rozmiar pełnego JSON request w znakach: mediana 125442, p95 435168.

Dodatkowo z `docs/OPENFILL_ARCHFLOW_TOOL_RESULTS_20261102.json`: 1574/1574 plików run log dostępnych, `act_many` 1490 wyników tool.status=ok i 67 error (1557 z 1558 wywołań widocznych w usage, jednego wyniku nie odtworzono z logów). Wśród 67 błędów: `UNKNOWN_ACTION` 24, `NOT_FOUND` 20, `Illegal invocation` 15, anulowanie 2, zatrzymanie semantic batch 1, pozostałe 5. Kategorie są oparte na skróconych preview, nie pełnym wyniku przeglądarki. `act_locator`: 13 ok i 3 error, `view_digest`: 12 ok. Nie porównywać tych proporcji jako aktualnego testu LIVE po zmianach.

Przyczyna kodowa części błędów: `Assets/actions__v0.3__2026-10-05_1515.js` w `root.act` nie obsługuje `key`, `realClick`, `realType`, choć wcześniejszy opis legacy `act_many` mógł sugerować ich użycie. `root.actMany` przed poprawką wykonywał dalsze kroki także po niepowodzeniu. `nativeSet` wybiera setter `HTMLInputElement` dla elementów innych niż textarea/select, co przy nieedytowalnym elemencie może powodować `Illegal invocation`; nie udowodniono, że tłumaczy wszystkie 15 historycznych przypadków. `root.resolve` szuka `data-of-id` na aktualnym DOM, więc rerender może spowodować `NOT_FOUND`. To są rzeczywiste mechanizmy w kodzie, ale historyczne kategorie błędów nie zawierają pełnego dowodu konkretnego elementu.

Wniosek udowodniony: model jest wielokrotnie angażowany pomiędzy sekwencyjnymi operacjami UI, mimo istniejących lokalnych funkcji. Część `act_many` kończyła się rzeczywistymi błędami nieobsługiwanej akcji, nieodnalezionego elementu lub wykonania JS, a nie tylko brakiem dobrej instrukcji dla modelu. Hipotezy wymagające pomiaru: (a) zbyt wiele `selector:null` w widokach; (b) model wybiera `get_page` mimo możliwego lokalnego `assert`; (c) lokatory tracą ważność po przejściu SPA; (d) nie da się przewidzieć następnego kroku bez nowego odczytu; (e) narzędzia zwracają zbyt szerokie payloady. Nie przypisywać wszystkim odczytom statusu redundantnych.

## 3. Nowy podział odpowiedzialności

1. **Planner LLM**: tworzy ograniczony plan z jawnie określonymi warunkami i kryteriami sukcesu. Nie decyduje o każdym pojedynczym kliknięciu. Dostaje krótki stan i dowody, nie pełną historię wszystkich HTML.
2. **SemanticStateTracker**: czyta aktualny DOM przez WebView2/CDP, wystawia stabilne referencje do elementów oparte o role, accessible name, etykiety, tekst wiersza i atrybuty. Rozróżnia `visible`, `absent`, `ambiguous`, `stale`; nie zgaduje lokatora. Po mutacji odświeża tylko potrzebny region, z numerem rewizji.
3. **LocalPlanExecutor**: wykonuje w kolejności plan z `if`, `foreach`, `wait_until`, `assert` i jawnie oznaczonymi side effects. Każdy node ma lokalny wynik i dowód. Na nieznanym stanie kończy plan z `needs_replan`, nie powtarza niepewnej akcji.
4. **EffectJournal**: per `business_operation_id`, `target_id`, `node_id`: `planned`, `attempted`, `observed`, `uncertain`. Przed kliknięciem wysłania zapisuje `attempted`. Wznowienie `uncertain` nie klika ponownie.
5. **ContextAssembler**: do modelu wysyła tylko cel, aktualny snapshot i jego różnice, dotychczasowe dowody, brakujące informacje i pytanie o nowy fragment planu. Schemat narzędzi planner-mode ograniczony do `submit_plan`, `finish`, `ask_user`; legacy tools pozostają fallback.

## 4. Proponowany kontrakt wejścia do Responses API

To jest projekt nowego payloadu logicznego `input`, nie obecnie wysyłany JSON.
```json
{
  "protocol": "of.plan.v1",
  "business_operation_id": "op-123",
  "task_goal": {"kind": "send_messages", "targets": ["r1", "r2"], "success": "outgoing_ui_observed_each"},
  "observed_state": {
    "revision": 8, "host": "web.whatsapp.com", "view": "conversation",
    "account": {"status": "verified", "ref": "account-1"},
    "elements": [
      {"ref": "e1", "role": "textbox", "name": "Message", "enabled": true},
      {"ref": "e2", "role": "button", "name": "Send", "enabled": true}
    ]
  },
  "known_facts": [{"key": "recipient.r1", "value": "jid-1", "source": "task"}],
  "constraints": {"send_sequentially": true, "no_retry_uncertain": true},
  "previous_action_outcomes": [],
  "pending_uncertainties": [],
  "plan_request": {"mode": "initial", "max_nodes": 30, "max_actions": 60}
}
```
Cały zewnętrzny HTTP request nadal ma `model`, `instructions`, `input`, `tools`, `store=false`, `reasoning`. Kontrakt planu powinien być jedynym znaczącym narzędziem `submit_plan`, nie dodatkiem do pełnej listy kilkudziesięciu narzędzi.

## 5. Proponowana odpowiedź modelu i lokalny DSL

```json
{
  "protocol": "of.plan.v1",
  "preconditions": [{"kind": "host_equals", "value": "web.whatsapp.com"}],
  "nodes": [
    {"id": "n1", "op": "locate", "by": {"role": "textbox", "name": "Message"}, "bind": "composer"},
    {"id": "n2", "op": "fill", "target": "$composer", "value_ref": "message.r1"},
    {"id": "n3", "op": "assert", "kind": "value_equals", "target": "$composer", "value_ref": "message.r1"},
    {"id": "n4", "op": "click", "by": {"role": "button", "name": "Send"},
      "effect": {"kind": "send_message", "target": "r1", "journal_key": "op-123/r1/send"}},
    {"id": "n5", "op": "wait_until", "timeout_ms": 5000,
      "condition": {"kind": "new_outgoing_ui", "target": "r1"}},
    {"id": "n6", "op": "if",
      "condition": {"kind": "evidence_observed", "node": "n5"},
      "then": [{"id": "n7", "op": "mark_observed", "node": "n4"}],
      "else": [{"id": "n8", "op": "stop", "reason": "uncertain_effect"}]}
  ],
  "on_unknown": "needs_replan",
  "on_uncertain_effect": "stop_without_retry"
}
```
Pozostałe dozwolone node: `navigate`, `select`, `focus`, `type`, `foreach` po bieżących renderowanych rekordach, `screenshot_fallback`, `stop`. Locator: `role/name`, `label`, `text`, `testid`, `relative_row`, `css_unique` wyłącznie z aktualnego snapshotu. Nie wolno wykonywać nieograniczonego JS generowanego przez model. Plan walidowany przed wykonaniem: wersja, host, liczba node, liczba akcji, maksymalna głębokość gałęzi, unikalne identyfikatory, typy argumentów, zakaz odwołań do nieznanych danych.

Algorytm: `validate(plan) -> snapshot(current) -> resolve(node) -> precondition -> journal.attempted(if effect) -> execute browser action -> observe narrow postcondition -> journal.observed OR uncertain -> next node OR needs_replan`. Dla `foreach` każdy element jest ponownie rozwiązywany względem bieżącego widoku; nie wolno trzymać indeksu wiersza po zmianie listy. `if` może rozstrzygać tylko na podstawie lokalnie zaobserwowanego stanu, nie dowolnego przypuszczenia.

## 6. WhatsApp: pięć wiadomości, pięć osobnych skutków UI

Warunek wejściowy: pięć znanych JID/telefonów i pięć tekstów. Jeden plan może zawierać pętlę po pięciu elementach. Egzekutor wykonuje **sekwencyjnie** dla każdego odbiorcy: `navigate/open` -> `assert recipient header and account` -> `fill composer` -> `assert composer value` -> `journal attempted` -> `click send` -> `wait_until outgoing bubble` -> `journal observed`. Dopiero potem następny odbiorca. Minimum pięć oddzielnych kliknięć wysłania, typowo co najmniej 15 interakcji otwarcie/wpisanie/wysłanie plus lokalne odczyty. Liczba LLM calls zależy od jakości znanych danych, lokatorów i potwierdzeń: możliwy jeden plan plus zero lub kilka napraw, **nie ma gwarancji jednego wywołania**. Nie wolno wysłać ponownie po `uncertain`.

Dla nieznanego odbiorcy: planner żąda obserwacji listy; resolver wyszukuje widoczny dokładny wiersz; brak jednoznaczności = replan/ask_user, nie wysyłka do pierwszego podobnego nazwiska.

## 7. Strona dowolnego serwisu, bez adaptera

`snapshot(list)` -> `foreach visible record where text_matches(task_target)` -> `assert unique row` -> `click row` -> `wait_until form` -> `locate by label` -> `fill` -> `assert value` -> `journal attempted` -> `click save` -> `wait_until success marker` -> `mark observed`. Gdy lista jest wirtualizowana, lokalny tracker może przewijać i odczytywać nowe rekordy w ustalonym budżecie. Gdy wynik nie jest jednoznaczny, zatrzymuje się i oddaje kontrolę modelowi. Brak założeń o CSS konkretnego serwisu.

## 8. Dokładny zakres zmian kodu

- `AgentRunner.RunAsync`: tryb `planner` zamiast pełnej pętli function_call na każdą akcję; wywołanie modelu tylko na start, `needs_replan`, koniec lub prośbę o informację. Tryb legacy zachowany przełącznikiem.
- `ToolRegistry.Build`: `submit_plan` ze ścisłym schematem, oddzielony od legacy `act_many` i `act_locator`. Istniejące narzędzia nie są usuwane.
- `SemanticViewDigest.Script`: mierzyć rzeczywiste pokrycie selektorami. Następnie rozszerzyć snapshot o role/nazwy i relacje, bez domenowych adapterów.
- `BrowserController` i `CdpSession`: `ReadSemanticSnapshotAsync`, `ResolveSemanticLocatorAsync`, `ActAndObserveAsync`, z obserwacją bieżącego DOM po każdym krytycznym działaniu.
- `PromptHistoryCompactor`: w planner-mode zastąpić pełną historię cykli modelem stanu i krótkim dziennikiem dowodów. Nie usuwać śladu trwałych side effects.
- `UsageTotals`, `EventLog`, `AppHost`: jawne `business_operation_id` dziedziczone przez kontynuacje, `plan_id`, `node_id`, `model_call_id`, `browser_action_id`, `observed_revision`, `evidence_ref`, `status`.

Nowe klasy: `PlanProtocolValidator` (JSON -> validated nodes), `SemanticStateTracker` (WebView2 -> state + revision), `SemanticLocatorResolver` (state + locator -> unique/ambiguous/missing), `LocalPlanExecutor` (validated plan + browser -> node outcomes), `EffectJournal` (idempotentny zapis przed efektem), `ModelContextAssembler` (goal + state delta + outcomes -> bounded input).

Pierwszy pionowy slice: read-only lista -> locate unique -> click -> local assert -> return plan outcome, z fake browser. Następnie jeden formularz z zapisem do fikcyjnej witryny i ledger attempted/uncertain. Dopiero po tych testach WhatsApp, bez rzeczywistych wysyłek podczas testów.

## 9. Pomiary i eksperyment

Jednostka pomiaru: `business_operation_id`, łączy wszystkie `task_id`, `run_id`, retry i wznowienia. Nie łączyć po identycznym prompt bez dowodu tego samego celu. Liczyć osobno model calls, browser actions, DOM reads, local polls, input cached/uncached, output tokens, liczbę i rodzaj postconditions, status skutku. Dla każdego node: `observed`, `uncertain`, `failed_before_action`; dla lokatorów: `unique`, `ambiguous`, `missing`, `stale`.

Przykładowy pojedynczy, niesumowany scenariusz dla `act -> get_page` (10238 przejść):
`saved_calls = 10238 * fraction_eligible * local_success_rate`.
- 10% kwalifikowalnych, 50% lokalnie obsłużonych: około 512 calls, 0.67% z 76566.
- 30% kwalifikowalnych, 70% obsłużonych: około 2150 calls, 2.81%.
- 60% kwalifikowalnych, 80% obsłużonych: około 4914 calls, 6.42%.
- Formalny limit tylko tej klasy: 10238 calls, 13.37%, przy nierealistycznym 100% kwalifikacji i sukcesu.

Nie dodawać osobno `get_page -> act` i `wait -> get_page` do tych oszczędności, bo sąsiednie przejścia współdzielą rundy. Właściwy test: te same zadania w kontrolowanym środowisku, porównanie stary runtime vs planner-executor, z identycznym celem i weryfikacją rezultatu, nie tylko licznika calls.

## 10. Stan wykonania w OF-000008

SOURCE wykonane: do `SemanticViewDigest.Script` dodano liczniki `locator_coverage`; do `ToolRegistry.view_digest` dodano logowane coverage, do `act_locator` reason_code nieudanego rozwiązania i do semantycznej gałęzi `act_many` status każdego lokalnego node; do `AgentRunner.RecordUsage` dodano długości `instructionsChars`, `toolSchemaChars`, `inputHistoryChars`, `inputItems`, a do `tool.result` rozmiar outputBytes. Rozmiar schematu obliczany raz na task, długość historii wykorzystywana z już istniejącej serializacji. Bez zapisu treści promptu. QA: `archflow-coverage-only` 4/4 PASS, `dotnet build OpenFill.App --no-restore` PASS, 0 errors, 6 dotychczasowych warnings; `node --check` dla `SemanticViewDigest.Script` PASS. Analiza historyczna: `tools/of_archflow_trace_diagnostics_20261102.py` i raport JSON/MD, asercja pełnego pokrycia 76566 calls PASS. Dodatkowo SOURCE: `actions.js root.actMany` zatrzymuje legacy batch po pierwszym błędzie zamiast wykonywać kolejne potencjalne side effects, `root.act` odrzuca `set/type/clear` na nieedytowalnych elementach jako `NOT_EDITABLE`, `ToolRegistry.LegacyBatchStepsSupported` sprawdza wszystkie kroki przed uruchomieniem i wyklucza `key/realClick/realType` z legacy batch. QA Node `ARCHFLOW_LEGACY_BATCH_FAILFAST_5_CHECKS_OK`, JS syntax PASS; QA C# `ARCHFLOW_LEGACY_BATCH_VALIDATION_3_CHECKS_OK`, exit 0. Finalny App build po wszystkich zmianach: PASS, 0 errors, 6 istniejących warnings. Nowy planner i executor nie są jeszcze napisane, nie przypisywać im oszczędności. LIVE niezmienione.
