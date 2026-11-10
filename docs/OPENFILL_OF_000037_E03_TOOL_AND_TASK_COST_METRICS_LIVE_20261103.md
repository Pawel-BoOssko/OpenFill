# OF-000037: E03 koszt zadania, wynik i czasy narzędzi, LIVE

Wybudzenie OF-000037-A01-071041: 2026-11-03 07:10:41 +01:00 (Warszawa).
Host PBOX1, SOURCE C:/Users/pboru/Downloads/OpenFill__v0.1__2026-10-04_1911/OpenFill.
Poprzedni checkpoint: docs/OPENFILL_OF_000036_E03_VIEW_READ_TELEMETRY_LIVE_20261103.md.

## Niedokończona część E03
- OF-000036 dodał decyzję read_view i elapsedMs/bytes, ale wynik całego zadania znajdował się osobno od rekordów usage kind=task, bez zliczonego czasu narzędzi i ich błędów.
- D01/D02 skutkowe acceptance nadal bez wskazanych odbiorców i treści. E01 v3 oraz A05/A06 nie wymagały ponownego wykonania.

## Wykonana zmiana SOURCE
1. src/OpenFill.Core/Agent/AgentRunner__v0.29__2026-10-29_2314.cs:
   - Pola ToolCalls, ToolErrors, ToolElapsedMs.
   - Stopwatch dla każdego faktycznie obsłużonego wywołania tool, w tym błędów argumentów/guard/timeout.
   - Istniejące zdarzenie tool/result zawiera teraz elapsedMs obok step/name/outputBytes.
   - ToolErrors oznacza błędy techniczne; nie stan zewnętrznego efektu.
2. src/OpenFill.Core/Hosting/TaskRun__v0.1__2026-10-07_1732.cs:
   - Własny finalOutcome ustawiony ze statusu zwróconego RunConclusion (domyślnie failed przy wyjątku/anulowaniu).
   - Istniejący rekord usage kind=task: nowe outcome, toolCalls, toolErrors, toolElapsedMs.
   - Istniejący event session/usage: te same metryki plus wcześniejsze totals z tokenami i costUsd.
   - Korelacja po runId/taskKey, bez nowego systemu raportowego, nowej bazy ani ponownego odczytu DOM.

## Kompilacja, DEV i LIVE
- dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000037 -v:q: exit 0, znane warnings nullable/WindowsBase/unused, bez błędów.
- SHA256 OpenFill.Core.dll: 29DB9E17E8FC5A0E47C6F3B8D66B027484224EE4730220A3744E2D48DB3A6661.
- Backup DEV: staging/of_000037_backup_pre_dev_activation. DEV OpenFill-OFIMP-dev-20261031 zatrzymano, opublikowano wydanie do staging/of_imp_20261031 i wznowiono; DEV PID 23468.
- CheckOnly przed LIVE: RELEASE_READY_NO_IDLE_GATE, exit 0. Release tools/of_imp_release_live_ALWAYS_20261101.ps1: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True, exit 0.
- Rollback LIVE: C:/Users/pboru/OpenFill/app__rollback__before_release_20261103_091843.
- LIVE PID 8824, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe.
- Zmierzone LIVE Core SHA256 identyczne; EXE SHA256 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52.
- CheckOnly po LIVE: LIVE_ALREADY_CURRENT, exit 0.

## Rzeczywiste acceptance
- Task OF-20261103-0920-2d63: odczyt example.com, done/success, 3 kroki, zgłoszony koszt ~0.0011 USD.
- Plik C:/Users/pboru/OpenFill/data/stable/logs/usage_202611.ndjson, runId=20261103_062009_a8f9ce, taskKey=OF-20261103-0920-2d63, kind=task: outcome=success, toolCalls=3, toolErrors=0, toolElapsedMs=1745, calls=3, inputTokens=24667, cachedTokens=16129, outputTokens=225, reasoningTokens=125, modelMs=12141, costUsd=0.00112759.
- Plik C:/Users/pboru/OpenFill/data/stable/logs/app_20261103.ndjson, source=tool,eventType=result, zawiera elapsedMs per wywołanie (np. finish 2 ms); source=session,eventType=usage seq63 zawiera outcome=success i liczniki narzędzi oraz totals.
- Przy kompilacji pierwsze wywołanie LC MultiHost zwróciło target_unavailable/fetch failed; sprawdzono PBOX1 jako Online przez Remote Desktop Commander, wykonano dotnet publish na PBOX1 przez start_process exit 0. Sporadyczne blokady konkretnych komend LC MultiHost nie przerwały wdrożenia; do zatrzymania DEV zastosowano Remote Desktop Commander start_process.
- Weryfikacja to realny log działań runtime, nie symulowany test jednostkowy.

## Status
- E03 metryki narzędzia + wynik/koszt taska: SOURCE DONE, DEV ACTIVE, LIVE ACTIVE, runtime PASS. Inne zakresy przekrojowych metryk E03 nadal otwarte.
- D01/D02 skutkowe wysyłki pozostają niepotwierdzone i nie były wykonywane. D03 Airbnb ANULOWANE 2026-11-02.
- Handoff obecnego leada dopiero OF-000039; nie zmieniano harmonogramu wybudzeń.
