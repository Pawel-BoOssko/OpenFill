# OF-000013: integracja lokalnego planu z AgentRunner w DEV i pomiar

Wybudzenie: OF-000013-A01-230605, 2026-11-02 23:06:05 +01:00 (Warszawa). Host: PBOX1.

## Zmiana SOURCE

- Dodano `src/OpenFill.Core/Agent/LocalPlanDevPipeline__v0.1__2026-11-02.cs` (kompilowany tylko z DEBUG). Akceptuje `of.plan.v1` lub `of.flow.v1` i kieruje do istniejących LocalPlanExecutor / LocalPlanControlFlow. Nie wprowadza narzędzi modelu i nie wywołuje Responses API.
- Dodano do istniejącego `AgentRunner__v0.29__2026-10-29_2314.cs` **jawny, wyłącznie DEBUG** punkt `RunLocalPlanDevAsync`. Nie zmieniono standardowej metody RunAsync ani domyślnego ToolRegistry.
- Telemetria obejmuje `businessOperationId`, `taskId`, `runId`, wersję protokołu, `status` i `reason`; liczniki wywołań `browser.ReadAsync` i skutecznych odczytów, prób i applied `ClickNavigationAsync`, prób i applied `FillTextAsync`, potwierdzonych lokalnych warunków, liczby iteracji, czasu trwania. `modelCalls=0` dotyczy jedynie ścieżki DEV bez modelu, nie całego zadania użytkownika.
- `EventLog.Write(source=archflow,eventType=local_plan_dev)` zapisuje wyłącznie liczby i identyfikatory do NDJSON, nie zapisuje tekstu pól, DOM ani treści planu.
- Rozszerzono `tools/of_archflow_webview2_smoke/Program.cs`: wywołanie faktycznej metody `AgentRunner.RunLocalPlanDevAsync` w osadzonym WebView2, z zerowymi współpracownikami model/ToolRegistry/config. Dzięki temu przypadkowe wejście w legacy pętlę wywołałoby błąd zamiast fałszywego wyniku. Potwierdzono istnienie 2 zdarzeń NDJSON i poprawne identyfikatory operacji.

## Wynik rzeczywistego testu DEV

Środowisko: PBOX1, interaktywna sesja Windows=1, lokalny serwer HTML, realny WebView2/DevToolsProtocol.
Komenda uruchomienia: `Start-ScheduledTask -TaskName 'OpenFill-OF012-WebView2-Smoke'`. Harness to test interaktywny, ale nie aplikacja LIVE.

- Wynik: `ARCHFLOW_WEBVIEW2_EMBEDDED_19_CHECKS_OK`, `EXIT=0`.
- Zarejestrowane: `flowReads=5`, `flowIterations=1`, `flowModelCalls=0`; `planReads=6`, `planNavigations=1`, `planFills=1`, `observed=2`, `planModelCalls=0`.
- Razem w tych dwóch fragmentach ścieżki DEV: 11 odczytów semantycznego DOM, 2 próby akcji przeglądarki, 0 wywołań modelu. Nie uwzględnia to testowych odczytów przed i po egzekucji; liczniki obejmują tylko przebieg pipeline. Nie jest to porównywalne A/B względem legacy. Brak zmierzonej redukcji kosztu.

## Naprawy po błędach

1. Podczas pierwszej próby kompilacji kodu testu: `CS1003` i `CS1056` w wierszu sprawdzającym NDJSON, wywołane nieprawidłowym escapowaniem cudzysłowów w `Contains`. Naprawiono przez proste `Contains("local_plan_dev", StringComparison.Ordinal)`.
2. Kolejne wykonanie skompilowane, lecz test `EXIT=1`: `IOException The process cannot access the file app_20261102.ndjson because it is being used by another process`. Przyczyna: EventLog nadal zapisuje NDJSON, a `File.ReadAllLines` otwierał plik z niezgodnym trybem dzielenia. Zmiana na `FileStream(FileShare.ReadWrite)`; następny przebieg zaliczył 19 kontroli.
3. Istniejący problem porządkowania efemerycznego profilu WebView2: plik `BrowserMetrics*.pma` zgłasza `Access denied` po przejściu testu. Sygnał `ARCHFLOW_WEBVIEW2_PROFILE_DEFERRED` pojawia się na stderr, kod wyjścia testu pozostaje 0. Przyczyna blokady pliku nieustalona. Kontynuacja testów nie wymaga ponawiania tych samych kontroli.

## Granice i następne zadanie

Tylko SOURCE/DEBUG. LIVE ani automatycznego RunAsync nie modyfikowano, brak kont zewnętrznych, wiadomości, zapisów w serwisach, Airbnb i wysyłek. `of.flow.v1` wciąż jest osobną read-only ścieżką. Potwierdzona ścieżka nie uruchamia modelu w ogóle; pozostaje do zrobienia wywołanie planner-mode Responses API tylko przy tworzeniu lub naprawie planu oraz rzeczywiste A/B jednego całego zadania (jakość, czas, calls, tokens). Efekty send/save/submit dopiero po integracji trwałego EffectJournal.
