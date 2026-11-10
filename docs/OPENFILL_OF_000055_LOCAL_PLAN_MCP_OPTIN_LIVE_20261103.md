# OpenFill OF-000055: produkcyjny opt-in dla local_plan_v1_draft, LIVE

Wake: OF-000055-A01-230916, 2026-11-03 23:09:16 +01:00 (Warszawa). Host: PBOX1. Projekt: OpenFill.

## Stan wyjściowy i potwierdzony brak
- Standardowa ścieżka produkcyjna AgentRunner.RunAsync używa pętli Luna -> tool -> Luna.
- W SOURCE znajdował się już opt-in RunPlannedDraftAsync, lokalny LocalPlanCdpBrowser i LocalPlanModelDevRunner udostępniony w Release. TaskRun.FillAsync przyjmuje executionMode=local_plan_v1_draft, a AppHost wykonuje w tym trybie wstępną nawigację do startUrl. Generowane plany nie dopuszczają save/send/submit; lokalny adapter obsługuje same-origin nawigację i draft textbox.
- McpTaskManager.StartAsync przyjmował opcjonalne executionMode i startUrl. W trakcie tego wybudzenia potwierdzono też zapis tych pól w McpTaskRecord i przekazanie do RunOptions. Kod w tych plikach był aktualizowany równolegle. Przed zmianą MCP brakowało parametrów w publicznym tools/list i w wywołaniu StartAsync z McpServer.

## Zmiana SOURCE OF-000055
- W src/OpenFill.Core/Mcp/McpServer__v0.24__2026-10-28_1648.cs dodano opcjonalne inputSchema pola execution_mode i start_url do narzędzia openfill_start_task.
- Handler openfill_start_task przekazuje oba parametry jako ostatnie opcjonalne argumenty do McpTaskManager.StartAsync.
- Domyślne zadania bez execution_mode nadal korzystają z obecnego iteracyjnego AgentRunner. Jawny tryb local_plan_v1_draft wymaga start_url i nie przyjmuje continue_task_id. Walidację takiego wyboru wykonuje istniejący McpTaskManager.
- Backup zmienionego pliku: McpServer__v0.24__2026-10-28_1648.cs.before_of000055. Nie zmieniano kodu wysyłek, adapterów WhatsApp/LinkedIn ani harmonogramu.

## Build i wydanie
- dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000055 -v:q: exit 0. Dotychczasowe warningi C# i WindowsBase, brak błędów build.
- DEV: DEV_BACKUP_OK staging/of_000055_backup_pre_dev_activation, DEV_RELEASE_OK, PID 19492.
- CheckOnly: RELEASE_READY_NO_IDLE_GATE.
- LIVE: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_START_IGNORE_ACTIVE_TASKS, LIVE_RELEASE_OK mcp=True, exit 0.
- Core SHA256: E0FEF705C0DB27D047F8258670EF9CE54A8C5F87A7B176990550BDFDE75ED118.
- Niezależnie LIVE PID 20424, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, faza OpenFill-open Running, Core SHA zgodny, EXE SHA256 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52.
- Rollback: C:/Users/pboru/OpenFill/app__rollback__before_release_20261104_011550 (nazwa z zegara hosta).
- Jedna rzeczywista kontrola produkcyjnego JSON-RPC tools/list na http://127.0.0.1:48731/mcp/<lokalny sekret> wykazała w openfill_start_task: task, continue_task_id, requester, chat, title, idempotency_key, execution_mode, start_url, wait_seconds. Wynik OF055_LIVE_MCP_SCHEMA_PASS. Sekret nie zapisany w raporcie.

## Test funkcjonalny i ograniczenia
- Jedna próba startu produkcyjnego zadania local_plan_v1_draft na https://example.com z weryfikacją linku, bez skutków ubocznych: rezultat MCP status=rejected_rewrite_needed, task_id=null. Zatrzymane przez warstwę JEV przed uruchomieniem planera, więc to nie jest test działania LocalPlanModelDevRunner.
- Kolejna, uproszczona próba wywołania została zablokowana przez zabezpieczenia narzędzia, bez id zadania i bez potwierdzonej akcji. Nie obchodzono blokady kolejnymi kanałami.
- W rezultacie: produkcyjny kontrakt, kompilacja i wdrożenie PASS. Funkcjonalne E2E planera oraz oszczędność model calls: NIEZWERYFIKOWANE w OF-000055. Nie nazywać tego pełnym ukończeniem infrastruktury.
- Zmiana została wykonana równolegle do modyfikacji AgentRunner, LocalPlanModelDevRunner, TaskRun, BrowserController, AppHost i McpTaskManager przez inny proces w SOURCE. Zmiana w MCPserver OF055 jest odrębna, więc nie przypisywać całej integracji tylko temu wybudzeniu.

## Zamknięta lista kolejnego etapu
1. Jedno faktyczne acceptance opt-in local_plan_v1_draft na neutralnej stronie, przez produkcyjny MCP i ze zgodnym zadaniem przechodzącym JEV, bez wysyłki ani submit.
2. Sprawdzić jeden rekord usage local_plan_call_group: modelCalls, DOM reads, attempt count, koszt oraz niezależnie zaobserwowany wynik.
3. Dopiero na tej podstawie rozważyć stopniowe rozszerzanie z trybu jawnego na automatyczny routing wybranych intencji. Bez testu nie deklarować redukcji calls.
4. D01/D02 skutkowa wysyłka nadal PARTIAL. Airbnb D03 ANULOWANE, nie wznawiać.
Wake, rotacja i harmonogram projektu bez zmian.
