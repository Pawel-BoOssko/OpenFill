# OpenFill OF-000056: odróżnianie historycznego anulowania i bezpieczna kontynuacja

Wybudzenie: OF-000056-A01-232922, 2026-11-03 23:29:22 +01:00 (Warszawa). Host: PBOX1.

## Fakty z LIVE
- Audyt wykonano na ostatnich 100 rekordach `kind=task` w `C:/Users/pboru/OpenFill/data/stable/logs/usage_202611.ndjson`, z korelacją `runId` do `app_YYYYMMDD.ndjson`.
- W 19 rekordach ze starszym `outcome=failed` wystąpiły w jednym `runId` zdarzenia `app/stop` i `session/cancel`. Stary `AppHost.Stop` mylnie opisywał każde źródło jako Stop użytkownika. Dlatego audyt rozpoznaje jedynie **anulowanie o nieustalonym inicjatorze**, a nie winę Luny ani pewny user_stop.
- Klasyfikacja 100 rekordów: 27 success, 13 partial, 2 failed, 19 cancelled_legacy (aktor nieznany), 2 interrupted, 37 bez outcome. Łącznie 2510 wywołań, raportowany koszt 1.0092 USD. Kandydatów do przeglądu pozostało 28 (wcześniej 38 po pierwszej korekcie; jeszcze wcześniej 74 w niedokładnej klasyfikacji).
- Przykładowe trzy potwierdzone stare anulowania z dużym licznikiem wywołań: `OF-20261104-0005-1cdf` 147 calls, `OF-20261103-1504-dcd2` 127, `OF-20261104-0025-a2f3` 92. Te zadania nie musiały być pętlami: samo zatrzymanie i liczba calls tego nie dowodzą.
- Oryginalnych rekordów usage ani zadań nie zmieniono. Zmieniono tylko metodę interpretacji w raporcie `docs/OPENFILL_EFFICIENCY_LIVE_20261104.md`.

## Zmiana SOURCE i dlaczego
- `src/OpenFill.Core/Mcp/McpTaskManager__v0.21__2026-10-28_2216.cs`:
  - `FindSimilarFinished(task, requester)` przy automatycznym doborze poprzedniej sesji wymaga teraz pełnej równości `Requester` (w tym identyfikatora czatu), więc nie otwiera niejawnie cudzego tasku.
  - Pomija `StopReason` user_stop, requester_cancel, upgrade_interrupted, crash_interrupted, external_cancel_unknown oraz stany stopped_by_user, stopped, cancelled, interrupted.
  - Jawne `continue_task_id` pozostaje możliwe jako odrębna intencja; brak automatycznego retry niepewnych efektów.
  - Przed edycją kopia `.before_of000056`.
- `tools/of_efficiency_audit_20261104.ps1`: korelacja zdarzeń app/stop i session/cancel, osobne `cancelled_legacy` i `legacy_cancel_actor_unknown`; 37 starych rekordów bez outcome nie są uznawane za failed. Odczyt `app_YYYYMMDD.ndjson` przez Get-Content (oryginalne [IO.File]::ReadLines zwróciło IOException o pliku używanym przez inny proces). Naprawiono też interpretację UTF-8 w Windows PowerShell 5.1: skrypt został zapisany z BOM (początkowo nie miał BOM, a polskie znaki powodowały `Unexpected token`), po czym raport wykonał się PASS.
- Żadnych zmian adapterów, wysyłania, ledger, SQLite, LocalPlan ani listy anulowanych domen (Airbnb D03 pozostaje ANULOWANE).

## Wydanie
- `dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000056 -v:q`: exit 0, bez nowych błędów, znane ostrzeżenia C#/WindowsBase.
- `tools/of_000056_activate_dev.ps1`: DEV_RELEASE_OK PID 5600, backup `staging/of_000056_backup_pre_dev_activation`.
- CheckOnly: RELEASE_READY_NO_IDLE_GATE.
- LIVE `tools/of_imp_release_live_ALWAYS_20261101.ps1`: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True, exit 0, bez oczekiwania na idle.
- Niezależna kontrola LIVE: PID 3488, EXE `C:/Users/pboru/OpenFill/app/OpenFill.exe`, `OpenFill-open` phase Running, Core SHA256 `6008296080C053FBD8277A62F78F4AA2D19A3BA19049AF60C3D50CC219E25155`, EXE SHA256 `6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52`. MCP `openfill_info` -> `working=[]`.
- Rollback LIVE: `C:/Users/pboru/OpenFill/app__rollback__before_release_20261104_013553`.

## Granice potwierdzenia i dalszy krok
- Potwierdzona kompilacja/aktywacja, PID/SHA/MCP, wykonanie audytu na realnych logach. Nie wykonano sztucznego anulowania po wydaniu; wykluczenie auto reuse potwierdzono przeglądem SOURCE i buildem, nie nowym zadaniem E2E.
- Nie utożsamiać modelCalls z liczbą koniecznych wywołań. W kolejnych naturalnych taskach sprawdzać `stop_reason`, `stop_actor`, `task-review`, `read-streak` i `executionMode`, modelCalls/planDomReads/planActionAttempts.
- Produkcyjny opt-in `local_plan_v1_draft` pozostaje wdrożony, ale test E2E był blokowany przez JEV; nie twierdzić, że 1-call planner daje już produkcyjne oszczędności. Nie obchodzić blokady przez tworzenie przypadkowych testów. Dalej wyznaczać P0 z logów.
- D01/D02 batch send acceptance nadal PARTIAL, D03 Airbnb ANULOWANE, harmonogram wake bez zmian.
