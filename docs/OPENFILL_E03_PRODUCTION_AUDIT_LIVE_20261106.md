# OpenFill: produkcyjny audyt E03, poprawka run_js i wydanie LIVE

Data audytu: 2026-11-06, PBOX1. Repozytorium SOURCE: C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill.
Źródła: C:\Users\pboru\OpenFill\data\stable\logs\usage_202611.ndjson i app_20261105.ndjson. Narzędzia audytu: tools/of_usage_prod_audit_20261106.py, tools/of_prod_error_probe_20261106.py, tools/of_prod_error_context_20261106.py.

## Dane produkcyjne, okno ostatnich 30 godzin
Stan pomiaru: 2026-11-06 12:00:53 UTC; od 2026-11-05 06:00:53 UTC.
- Zakończone zadania: 24, w tym 14 success, 7 partial, 2 interrupted, 1 cancelled.
- Wywołania modelu w tym oknie (również zadań w toku): 587.
- Koszt tych wywołań: 0.218334 USD, inputTokens 9412324, cachedTokens 8460103, cache udział około 89.9%.
- Nie utożsamiać liczby zakończonych zadań z liczbą runId. Licznik call obejmuje także otwarte przebiegi.

## Zamknięta lista zaobserwowanych problemów
1. LinkedIn runId 20261105_075235_393302, task OF-20261105-0852-215b zakończone cancelled: 107 model calls, 0.03665 USD, 29 compactions; 40 run_js, 15 search_logs, 14 get_page, 9 screenshot, 7 get_network. Sześć błędów tool, w tym run_js: 2 x timeout 60s, 2 x SyntaxError Unexpected token ';', 1 x code=29 (liczba zamiast JS string), 1 x act_many zwróciło stopped/uncertain po skutkowych krokach. W tym przypadku błędy są realne, nie liczyć jednak całych 107 calls jako błędnych.
2. W trakcie audytu aktywny runId 20261106_115046_bdb6f6 na lh.pl miał 78 calls, 0.02764 USD, 22 get_page, 16 act, 10 read_text, 10 run_js, 7 compactions i cztery błędy narzędzi. Zadanie było nadal w toku. Nie anulowano go ani nie ponawiano żadnych operacji skutkowych.
3. Zadanie aplikacyjne Mistral AI runId 20261105_125414_a51675, partial, 39 model calls, 0.01599 USD: upload_file dał FILE_NOT_FOUND dla brakującego CV; kolejne terminal_exec z nieistniejącym katalogiem i szerokim przeszukiwaniem profilu zakończyło się error/timeout 30 s. To osobny problem rozpoznawania faktycznej ścieżki pliku, bez zmian w tym wydaniu.

## Jedna poprawka SOURCE (nie zmienia działań skutkowych)
W pliku src/OpenFill.Core/Agent/ToolRegistry__v0.22__2026-10-29_2314.cs:
- run_js.code wymaga teraz rzeczywistej niepustej wartości typu string przez JsonValue.TryGetValue<string>; błędne liczby, obiekty, pusty tekst są zwracane jako run_js.code_invalid_type z przykładem poprawnego kodu, bez próby wykonania w WebView2.
- run_js.mode pobierane przez ToString i walidowane wyłącznie jako expression/statements.
- Opis model-visible narzędzia preferuje wbudowane get_page/read_view/view_digest do odczytów, opisuje mode=statements przy wielokrotnych średnikach, odradza ślepe ponawianie operacji przy SyntaxError/timeout i powtarzane read-only run_js po błędach.
- Nie wprowadzono heurystycznego wykonywania JS ponownie po wyjątku ani automatycznego uznawania skutków za nieudane.
Backup SOURCE: ten sam plik + .before_e03_runjs_20261106.
Test SOURCE: tools/of_imp_smoke/ToolRegistryEffectRegistrationSmoke.cs, backup .before_e03_runjs_20261106. Dodano walidacje błędnego typu i pustego stringa w obu środowiskach testowych (dev/stable).

## Testy i release
- dotnet build Core Release: 0 błędów, istniejące ostrzeżenia.
- dotnet build FileFilterSmoke Release: 0 błędów.
- dotnet FileFilterSmoke.dll of022-effect-registry-only: OF023_LINKEDIN_BATCH_AND_RUN_JS_GATES_18_CHECKS_OK, exit 0.
- dotnet run OpenFill.Tests -- mcp-origin-only: Passed 212, Failed 0, exit 0.
- Publish Release staging/of_e03_runjs_20261106, exit 0.
- DEV activation tools/of_e03_runjs_20261106_activate_dev.ps1, DEV_RELEASE_OK PID22304.
- LIVE CheckOnly RELEASE_READY_NO_IDLE_GATE.
- Kanoniczny LIVE tools/of_imp_release_live_ALWAYS_20261101.ps1: LIVE_RELEASE_OK mcp=True, exit 0.
- Backup rollback C:\Users\pboru\OpenFill\app__rollback__before_release_20261106_131217.
- Niezależna weryfikacja: tools/of_e03_independent_live_verify_20261104.ps1, E03_INDEPENDENT_LIVE_OK, exit 0, LIVE PID21564, EXE C:\Users\pboru\OpenFill\app\OpenFill.exe, phase Running, MCP True, working 0 w chwili sprawdzenia.
- Core SHA256 staging=LIVE: 3E25547519A2DDF2CC705153287666712A91C671241CD153B3F752074E150CA2.

## Granice i dalszy odbiór
- Powyższe 18 testów potwierdza brak wyjątku konwersji dla nieprawidłowego inputu i walidację bez przeglądarki. Brak porównywalnej próby produkcyjnej po wydaniu, która dowodziłaby redukcji kosztów/calls. Nie deklarować procentowej oszczędności tego patcha.
- Kolejne przyszłe zadania należy porównywać w oparciu o narzędzia/of_usage_prod_audit_20261106.py: liczba calls, liczba błędów run_js per task, koszty, task outcome, liczba history compactions, repeatedPageReadRequests. Nie agregować różnych typów zadań jako jedna populacja A/B.
- D02 zewnętrzny send, D04 rzeczywista publikacja, natywny pełny Stop E2E pozostają bez nowych testów skutkowych w tej sesji.
- Airbnb D03 pozostaje anulowane, wake of wyłączone. Nie resetowano repo, nie wykonywano wysyłek ani publikacji.
