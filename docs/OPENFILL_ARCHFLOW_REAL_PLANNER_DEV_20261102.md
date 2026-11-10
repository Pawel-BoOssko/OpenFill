# OF-000014: rzeczywiste planowanie Responses API -> LocalPlanExecutor -> WebView2

Start wybudzenia silnika: `OF-000014-A01-232606`, 2026-11-02 23:26:06 +01:00 (Warszawa).
Host: PBOX1. SOURCE/DEV, bez wdrożenia LIVE.

## Wdrożony kod

- Nowy `src/OpenFill.Core/Agent/LocalPlanModelDevRunner__v0.1__2026-11-02.cs`, wyłącznie `#if DEBUG`. Jeden wymuszony function call `submit_plan(plan_json)` przez istniejący `IModelClient.CreateResponseAsync`, kompatybilny z `OpenAIClient` i `POST /responses`. `store=false`, `parallel_tool_calls=false`.
- Kontekst: cel, bieżący host, rewizja i maks. 80 elementów z rolą, nazwą i enabled, bez wartości textbox/HTML. Treść strony pozostaje danymi, nie instrukcjami.
- Ścisłe parsowanie i walidacja `of.plan.v1`, sprawdzenie hosta i referencji; plan z efektem `send/save/submit`, nieznaną operacją lub błędnym bind nie dociera do przeglądarki.
- Gdy plan zawiera niezdefiniowany `$binding`, dopuszczona jedna korekta struktury bez wykonania akcji. Po zaobserwowanym zastosowaniu nawigacji, gdy późniejszy warunek nie wystąpił, dopuszczona jedna kontynuacja z aktualnego DOM, bez ponawiania nawigacji. Nie ma kontynuacji po `uncertain_effect` ani automatycznego ponawiania `fill`.
- Liczniki: rzeczywiste HTTP/model calls (również nieudane), tokeny z odpowiedzi API, rozmiar żądań, odczyty planowania i egzekutora, próby nawigacji i wypełnień, zaobserwowane potwierdzenia i całkowity elapsedMs. W przypadku dwóch wywołań wartości są sumowane.
- `AgentRunner__v0.29__2026-10-29_2314.cs`: nowy `RunPlannedDevAsync` oparty o `_model`, event `archflow/planned_dev` w NDJSON. Standardowy `RunAsync` i ToolRegistry bez zmian.
- `tools/of_imp_smoke/LocalPlanModelSmoke.cs`, `archflow-planner-only`: 79 kontroli PASS (exit 0). Fake API, wymuszenie jednego narzędzia, bezpieczne odrzucanie błędnych planów, agregacja tokenów, brak powtórnej nawigacji i jedna naprawa strukturalna.
- `tools/of_archflow_webview2_smoke/Program.cs`: tryb `--real-planner`, prawdziwy `OpenAIClient` z lokalnym kluczem dev i modelem z config stable, rzeczywista kontrolka WebView2 w sesji interaktywnej Windows 1. Strona testowa jest lokalna (`127.0.0.1`), bez kont zewnętrznych.

## Diagnostyka rzeczywistego modelu

Pierwsza próba rzeczywistego modelu (`gpt-6-luna`): `ModelCalls=1`, 487 wejściowych/624 wyjściowych tokenów. Plan otwierał link Customer 42, potem błędnie czekał na ponowne pojawienie się tego samego linku. `wait_timeout`, model nie wypełnił pola. Bieżący DOM zawierał już textbox Customer name, więc rzeczywista nawigacja zadziałała. Zidentyfikowano błąd planowania.

Druga próba po poprawie instrukcji: `ModelCalls=1`, 548/245 tokenów, plan użył niezdefiniowanego `$binding` przy textbox; walidator zgłosił `plan_unknown_binding` zanim cokolwiek wykonał. Dodano jednorazowy feedback o błędzie struktury i ścisłe instrukcje kolejności locate/bind.

## Finalny rzeczywisty test, wynik PASS

- Model: `gpt-6-luna`, prawdziwe `POST /responses` i realny WebView2.
- `Status=completed`, `Reason=null`, `ModelCalls=1`.
- `InputTokens=610`, `OutputTokens=319`, `RequestChars=2683`, `ElapsedMs=5076`.
- `TotalDomReads=7`, `NavigationAttempts=1`, `FillAttempts=1`, `ObservedConditions=2`.
- Model sam zaplanował `locate link -> click -> wait_until textbox -> locate textbox -> fill -> assert_value`, formularz otrzymał wartość `Planner verified` i wynik został potwierdzony ponownym odczytem rzeczywistego DOM.
- `ARCHFLOW_WEBVIEW2_EMBEDDED_23_CHECKS_OK`, `interactive.exit=0`. Po teście nadal występuje `ARCHFLOW_WEBVIEW2_PROFILE_DEFERRED` dla pliku `BrowserMetrics*.pma`, bez wpływu na kod wyjścia.
- `dotnet build OpenFill.App -c Release --no-restore -v:q`: exit 0, 0 błędów, 6 istniejących ostrzeżeń.
- Tester `run_interactive.ps1` przywrócony do domyślnego uruchamiania BEZ `--real-planner`; kolejne zwykłe uruchomienie zadania nie wywołuje modelu automatycznie.

Granice: rezultat jednego lokalnego scenariusza i trzech prób planowania. Nie dowodzi jakości na dowolnych witrynach ani oszczędności wobec legacy, bo nie uruchomiono porównywalnego A/B. Dotychczasowe `of.flow.v1` jest osobną read-only ścieżką; `EffectJournal` nadal nie jest podłączony do side effects. Brak zmian w usługach zewnętrznych i LIVE.

Następne: kontrolowane A/B całej operacji (legacy kontra planner), na tej samej lokalnej stronie i z niezależną oceną rezultatu; następnie warunkowe node i efekty z `business_operation_id` oraz trwałym dziennikiem.
