# OF-000038: E03 metryki operacji wsadowych, DEV + LIVE

Wybudzenie OF-000038-A01-073056, 2026-11-03 07:30:56 +01:00 Warszawa.
Host PBOX1. SOURCE C:/Users/pboru/Downloads/OpenFill__v0.1__2026-10-04_1911/OpenFill.
Punkt wyjścia: docs/OPENFILL_OF_000037_E03_TOOL_AND_TASK_COST_METRICS_LIVE_20261103.md.

## Luka E03
Zaimplementowany wcześniej ProcedureExecutor raportował trwałe stany per operation i evidence_ref, ale nie czas sprawdzenia celu (preflight), działania i całej serii. Obok E03 routing/read_view i task-cost brakowało tej metryki wynikowej na poziomie biznesowej operacji.

## Wykonana zmiana SOURCE
- src/OpenFill.Core/Agent/ProcedureExecutor__v0.1__2026-10-31.cs:
  - Stopwatch per realnie wykonywany preflight i action, bez nowych odczytów WebView2 i bez przechowywania payloadu w metrykach.
  - W każdym blokowanym preflight dodano preflight_elapsed_ms; w rzeczywiście wykonanym wierszu preflight_elapsed_ms i action_elapsed_ms.
  - W końcowej odpowiedzi serii metrics: batch_elapsed_ms, preflight_elapsed_ms, action_elapsed_ms, executed_this_run, items_returned.
  - Rozróżnienie: executed_this_run mierzy tylko faktycznie wywołane akcje. Nie zalicza wznowionych verified/uncertain i blokad przed MarkAttempted.
  - Idempotency/request scope, trwały ledger, evidence, uncertain/no-retry i brak skutku zewnętrznego przy blocked preflight pozostawiono bez zmian.
- tools/of_imp_smoke/WhatsAppBatchEffectSmoke.cs: 4 dodatkowe sprawdzenia obecności duration na zablokowanym i wykonanym wierszu oraz spójności liczników i metryk serii; wcześniejsze 15 zachowane.

## QA, wydanie i środowisko
- Test celowany: dotnet run --project FileFilterSmoke.csproj -c Debug --no-restore -- of025-whatsapp-batch-only z katalogu tools/of_imp_smoke: OF025_WHATSAPP_BATCH_19_CHECKS_OK, exit 0.
- dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000038 -v:q: exit 0, tylko znane dotychczasowe ostrzeżenia.
- Core SHA256: 76E69DC98BA779DE7DA1496926801CF419BB8CA5CD825292CEC8452A415FEB8E.
- DEV backup staging/of_000038_backup_pre_dev_activation; skopiowano wydanie do staging/of_imp_20261031, uruchomiono task OpenFill-OFIMP-dev-20261031, DEV PID 20912.
- CheckOnly przed: RELEASE_READY_NO_IDLE_GATE, exit 0. tools/of_imp_release_live_ALWAYS_20261101.ps1: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True, exit 0, bez czekania na idle.
- Rollback LIVE: C:/Users/pboru/OpenFill/app__rollback__before_release_20261103_093719.
- LIVE PID 16104, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, odczytany Core SHA256 identyczny z release.
- CheckOnly po: LIVE_ALREADY_CURRENT, exit 0; openfill_info working=[].
- Faza: SOURCE DONE -> DEV ACTIVE -> LIVE ACTIVE. Dla per-item timings realny acceptance efektu na kontach nie został wykonany, bo brak zleconych odbiorców i treści; działa celowany test z rzeczywistym ProcedureExecutor, SQLite i fałszywym portem przeglądarki.

## Pozostałe
- D01 WhatsApp i D02 LinkedIn: realna wysyłka i jej dowód nadal niepotwierdzone. Nie wysłano wiadomości.
- D03 Airbnb anulowane przez właściciela 2026-11-02, nie podejmować.
- Lead OF-000030..039, handoff przy OF-000039. Harmonogram wybudzeń bez zmian.
