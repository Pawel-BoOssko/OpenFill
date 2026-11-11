# OpenFill: LocalPlan explicit opt-in, produkcja 2026-11-04

## Zakres
Wydanie z kontrolowanym trybem execution_mode=local_plan_v1_draft. MCP openfill_start_task otrzymał opcjonalne pola execution_mode i start_url. Zwykłe zadania używają starego AgentRunner, bez migracji i bez automatycznego fallback.

LocalPlan generuje plan of.plan.v1 przez bounded wywołanie Responses API i wykonuje lokalnie dozwolone operacje na aktualnym CDP: locate, read-only assertions, same-origin link navigation, wait_until, plaintext textbox fill i assert_value. Validator odrzuca effect/send/save/submit i arbitrary JS. Nie dotyka providerowych batch send, SQLite/ledger ani anulowanego Airbnb. Limit planowania: początkowe wywołanie i maksymalnie jedno wznowienie naprawcze, nie więcej niż dwa wywołania modelu. Wynik częściowy nie uruchamia zwykłego agenta ani automatycznego ponawiania niepewnych czynności.

## Wejście i ograniczenia
- Tylko jawne wywołanie openfill_start_task z execution_mode=local_plan_v1_draft oraz start_url.
- start_url: bezwzględny http(s), bez userinfo i fragmentu; task <= 2000 znaków.
- Zakazane continue_task_id w trybie draft, brak automatycznego dziedziczenia starego tasku. Dedup oraz idempotency_key obejmują task text, mode i start URL.
- Każdy plan wymaga hosta zgodnego ze snapshotem; browser pozwala jedynie na nawigację w obrębie origin. Akcje na elementach sprawdzają fingerprint DOM; przy niepewności zwracają needs_replan/partial.
- API/MCP-first dla standardowej poczty i wyjątek dla Luny czytającej wiadomości uwierzytelniające pozostają bez zmian.

## Implementacja SOURCE
- LocalPlanModelDevRunner i LocalPlanDevPipeline: skompilowane także w Release; istniejące metody eksperymentalne AgentRunner nadal #if DEBUG, natomiast jawny production RunPlannedDraftAsync używa tego samego sprawdzonego wykonawcy.
- BrowserController: dostęp wewnętrzny do bieżącej sesji CDP; TaskRun przekazuje LocalPlanCdpBrowser bez tworzenia nowej przeglądarki.
- IRunHost.RunOptions, McpTaskRecord, McpTaskManager, McpServer, AppHost: selekcja trybu, adres startowy, walidacja, trwały zapis i routing.
- AgentRunner: osobne modelCalls/inputTokens/outputTokens/modelMs, planDomReads i planActionAttempts. ModelMs liczy czas rzeczywistych wywołań modelu, nie łącznie z lokalnym wykonaniem.
- TaskRun: liczby planu w usage kind=task i session/usage. task-review nie traktuje user_stop, requester_cancel i upgrade_interrupted automatycznie jako winy modelu; można nadal zakwalifikować długi przebieg do audytu po jego licznikach.

## Publikacje
1. Pierwsza integracja: dotnet publish staging/of_localplan_optin_20261104 exit 0. DEV PID19112, LIVE_RELEASE_OK. Core SHA256 BD65827C060B369A726DF83C404C65D0E6CB31ADFBA02821E6AEACD08249E939. LIVE PID17476. Rollback C:/Users/pboru/OpenFill/app__rollback__before_release_20261104_011904.
2. Uzupełnienie dokładnych metryk: dotnet publish staging/of_localplan_metrics_20261104 exit 0, DEV_RELEASE_OK PID11908, CheckOnly RELEASE_READY_NO_IDLE_GATE. LIVE_RELEASE_OK exit 0, mcp=True. Core SHA256 A3489FFFA8820A47462E0709C87BD5D285C544028E6A3EBEB1F3F15A715618FB. Niezależny LIVE PID7496, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, phase Running, OpenFill MCP openfill_info working=[].
3. Aktualny rollback: C:/Users/pboru/OpenFill/app__rollback__before_release_20261104_012440.
4. Backupy SOURCE dziewięciu plików *.before_localplan_live_20261104. Żadnych zmian schematu SQLite.

## Granice dowodu
Kompilacja, PID, SHA i MCP zostały niezależnie sprawdzone. Nie uruchomiono nowego produkcyjnego scenariusza pełnego opt-in; nie przypisywać mu rzeczywistej oszczędności kosztu czy tur. Historyczne DEV A/B 5 -> 1 model calls dotyczy jednego lokalnego formularza i nie dowodzi wyników portfela produkcyjnego. Narzędzie jest nową opt-in ścieżką, nie automatycznym routingiem intencji. Przed szerszym routingiem zbierać naturalne rekordy usage i wyniki potwierdzonych scenariuszy.

## Kontynuacja
- Włączyć automatyczne kierowanie do LocalPlan dopiero po paru rzeczywistych zakończeniach z outcome, modelCalls, planDomReads i planActionAttempts.
- Utrzymać szybkie wdrożenia, rollback oraz odróżnianie przyczyn anulowania; unikać dodatkowych QA kampanii.
- D01/D02 batch send acceptance nadal PARTIAL; D03 Airbnb ANULOWANE.
