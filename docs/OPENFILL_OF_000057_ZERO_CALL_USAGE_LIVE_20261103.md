# OpenFill OF-000057: kompletność końcowych logów zadań, także przy zero model calls

Wake: OF-000057-A01-234916, 2026-11-03 23:49:16 +01:00 (Warszawa). Host PBOX1. Zakres jedno rzeczywiste P0, minimalny build i wdrożenie.

## Ustalenie z SOURCE
- W `src/OpenFill.Core/Hosting/TaskRun__v0.1__2026-10-07_1732.cs`, `finally` emitował `kind=task` i `session/usage` wyłącznie po `if (runner is { Usage.Calls: > 0 })`.
- Zadania z istniejącym runnerem i dokładnie 0 wywołań nie dostawały końcowego usage. Gdy błąd/cancel występował przed stworzeniem runnera, również brakowało rekordów. To rzeczywista wada SOURCE, ale bez dowodu, że tłumaczy wszystkie dawne rekordy `outcome=null`.
- Wybór P0: kompletne logowanie przebiegów, nie naruszając zasad wysyłania, usług domenowych, logiki anulowania i LocalPlan.

## Zmiana SOURCE
- Kopia: `TaskRun__v0.1__2026-10-07_1732.cs.before_of000057`. Wstępny source SHA256: `09B05B8A386E6BBF74438D8B85BA3721E70E310EBBC10E5AA395DD6914DDE025`.
- `finally`: gdy runner istnieje, `kind=task` zapisuje się niezależnie od `Usage.Calls`. Nie zmieniono liczników ani formatu dotychczasowego rekordu.
- Przy `runner=null`: nowy `kind=task` z `calls=0`, `stage=before_agent_runner`, `outcome`, `stopReason` i polami korelacyjnymi; event `session/early-termination`. Nie zakłada, że przyczyna jest znana. Gdy brak przyczyny, `pre_runner_termination` jest etykietą techniczną, nie przypisaniem winy modelowi.
- Wyjątek inny niż OperationCanceledException ma teraz `session/exception` i stopReason `model_exception` dla ModelException albo `runtime_exception` dla innych. Wyjątek jest ponownie zgłaszany do dotychczasowej obsługi w AppHost.
- Zasady `user_stop`, `requester_cancel`, `upgrade_interrupted`, `timeout` i task-review pozostały bez zmian. Nie włączono automatycznego retry.

## Wydanie
- Publish: `dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000057 -v:q`, exit 0, znane warnings nullable/unused/WindowsBase, bez błędów.
- DEV `tools/of_000057_activate_dev.ps1`: DEV_RELEASE_OK PID14884, kopia `staging/of_000057_backup_pre_dev_activation`.
- CheckOnly: RELEASE_READY_NO_IDLE_GATE, SHA zgodny.
- LIVE `tools/of_imp_release_live_ALWAYS_20261101.ps1`: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True, exit 0. Brak idle gate.
- Niezależny LIVE PID21172, EXE `C:/Users/pboru/OpenFill/app/OpenFill.exe`, Core SHA256 `303B823A24254A48366142A8E65DD150DF8BC42338A686EA19F993B29DC15443`, EXE SHA256 `6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52`, `OpenFill-open` Running, `openfill_info` -> `working=[]`.
- Rollback: `C:/Users/pboru/OpenFill/app__rollback__before_release_20261104_015408`.

## Audyt i granice
- `tools/of_efficiency_audit_20261104.ps1` rozszerzony o osobne liczniki zero-call, pre-runner, model_exception, runtime_exception. Rerun PASS, ostatnie 100 realnych rekordów: 27 success, 13 partial, 2 failed, 19 cancelled_legacy (aktor nieznany), 2 interrupted, 37 bez outcome, 2510 calls, 1.0092 USD, review candidates 28.
- W zbiorze historycznym licznik zero-call = 0, pre-runner = 0, model_exception = 0, runtime_exception = 0. Nie stanowi to testu nowej funkcji, gdyż nie wywołano po wydaniu błędu ani zakończenia 0-call.
- Zgodnie z dyspozycją nie generowano sztucznych awarii, anulowań ani długiej serii testów. Weryfikacja obejmuje kompilację, uruchomiony LIVE, SHA, MCP i działający audyt.
- Następne rzeczywiste przypadki ze znanym `stop_reason` i zero-call monitorować poprzez usage. Kontynuować jedno nieukończone P0 na wake, minimalne wydania LIVE z rollbackiem. Jawny produkcyjny `local_plan_v1_draft` ma status BUILD/LIVE, ale E2E nadal niepotwierdzony po odrzuceniu JEV. Nie obchodzić blokady. D01/D02 batch send acceptance PARTIAL, Airbnb D03 ANULOWANE.
