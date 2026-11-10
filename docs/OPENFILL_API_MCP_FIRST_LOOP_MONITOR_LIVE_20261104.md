# OpenFill: API/MCP first i detekcja nadmiernych przebiegów, LIVE 2026-11-04

## Dyspozycja
Preferować dostępne i autoryzowane API/MCP zamiast automatyzacji browser, szczególnie Gmail, Calendar, Drive i Slack. OpenFill to fallback przy braku akcji w connectorze lub żądanym UI. Nie zakładać dostępności connectora bez sprawdzenia. Mierzyć prawdziwy wynik zadania i koszt, nie sztywno minimalizować liczbę tur. Szybkie wydania i rollback, ograniczone testowanie.

## Dowody
Ostatnie 35 zakończonych tasków LIVE (usage_202611.ndjson): 9 success, 15 failed, 11 partial. Przykładowe zakończone przebiegi: 95 i 127 model calls, odpowiednio 57 i 51 pageReadRequests, mimo repeatedPageReadRequests 1 i 0. Nie oznacza to automatycznie marnotrawstwa, ale wymaga możliwości detekcji. FailedCallGuard blokuje identyczne dwukrotne błędy; AgentRunner.NoteLoop ostrzega o pięciu identycznych akcjach w ostatnich dziesięciu akcjach i może zakończyć po trzech ostrzeżeniach. Krążenie między różnymi odczytami nie jest tym mechanizmem rozpoznawane.

## Zmiany SOURCE
- McpServer.Initialize i ToolList/openfill_start_task: API/MCP FIRST dla modelu zlecającego. Preferencja, nie blokada domen.
- AgentRunner.SystemPrompt: preferencja API/MCP, bez zgadywania connectorów zewnętrznego klienta.
- AgentRunner.NoteReadStreak: pasywne odczyty/wait bez pośrednich akcji browser, ostrzeżenia przy 12 i 24, event agent/read-streak i wskazówka dla Luny. Bez automatycznego stop.
- AgentRunner.MaxReadStreak i TaskRun usage kind=task oraz session/usage: licznik najdłuższej serii.
- TaskRun: jeden event session/task-review dla outcome innym niż success, modelCalls >= 60 albo maxReadStreak >= 12. Pełne liczniki i classification=review_candidate. Próg 60 to filtr do przeglądu, nie limit i nie dowód zbędnych wywołań.
- Kopie trzech zmienionych plików z końcówką .before_api_first_loop_audit_20261104. Bez zmian send, SQLite, ledger i LocalPlan.

## Build / DEV / LIVE
- dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_api_first_loop_20261104 -v:q: exit 0, dotychczasowe warnings, bez błędów.
- DEV aktywowany przez tools/of_api_first_loop_20261104_activate_dev.ps1: PID 5824, backup staging/of_api_first_loop_20261104_backup_pre_dev_activation.
- CheckOnly: RELEASE_READY_NO_IDLE_GATE.
- LIVE: tools/of_imp_release_live_ALWAYS_20261101.ps1: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True, bez czekania na idle.
- Niezależny LIVE: PID 19052; EXE C:/Users/pboru/OpenFill/app/OpenFill.exe; phase Running; Core SHA256 AF37B2C43D3159B525F8F78770090C0A6F2776057F181988201AED6CF2212DDA; EXE SHA256 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52. MCP openfill_info working=[].
- Rollback LIVE C:/Users/pboru/OpenFill/app__rollback__before_release_20261104_002437.

## Granice
Nie wykonano sztucznej długiej kampanii testowej wyłącznie dla wywołania alertów. Nowe eventy należy oceniać w kolejnych realnych zadaniach. Finalny failed może wynikać z zatrzymania przez użytkownika. API-first nie jest technicznym blokowaniem Gmail ani rozpoznawaniem connectorów klienta.
Następny duży etap: rozważyć włączenie istniejącego LocalPlan z DEV do produkcyjnego routingu po minimalnej kontroli rzeczywistych skutków; obecnie LocalPlanModelDevRunner i integracja są #if DEBUG.
