# OF-000040: E03 CDP read-call telemetry, SOURCE/DEV/LIVE

Wake: OF-000040-A01-081042, 2026-11-03 08:10:42 +01:00 Warszawa.
Lead: successor 3e6849275c9348999333da3a86b7dbde.
Host rozwoju i LIVE: PBOX1. Handoff od OF-000039 odczytany na SATANAMD z D:/OpenBrowserLiteHandoffs/of/HANDOFF_CURRENT.md. Zawieral projekt of, starego leada, stare URL czatu i ostatni wake 000039.

## Zmiana SOURCE
- src/OpenFill.Core/Cdp/CdpSession__v0.2__2026-10-05_1515.cs: CdpReadCallScope o zakresie AsyncLocal; licznik faktycznie wysylanych przez CdpSession read-like requestow Runtime.evaluate, Runtime.callFunctionOn i wybranych DOM.*.
- src/OpenFill.Core/Agent/AgentRunner__v0.29__2026-10-29_2314.cs: wlaczenie scope tylko dla jawnych narzedzi odczytu aktualnej strony, licznik CdpReadCalls oraz pole cdpReadCalls w istniejacych logach tool/result.
- src/OpenFill.Core/Hosting/TaskRun__v0.1__2026-10-07_1732.cs: cdpReadCalls w istniejacych usage kind=task i session/usage.
- Nie zmieniono wysylek, narzedzi browser act, backendu ledger, kont i idempotencji.
- Jest to licznik rzeczywistych zadan CDP dla okreslonych metod w ramach narzedzi odczytu, a NIE pelna liczba unikalnych DOM reads. Nie obejmuje direct ICdpConnection, prefetch poza narzedziem ani wszystkich niejawnych CDP accesses. Nie deduplikuje semantycznie roznych odczytow.

## Kompilacja i wydanie
- dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000040 -v:q: exit 0, dotychczasowe ostrzezenia.
- OpenFill.Core.dll SHA256: F43B705C038C603E8D08C6332566297ED358F4A23441AC2371B2F1B79DB80E5D.
- DEV: staging/of_imp_20261031 aktywowany przez tools/of_000040_activate_dev.ps1, exit 0, PID 6424, backup staging/of_000040_backup_pre_dev_activation.
- LIVE: tools/of_imp_release_live_ALWAYS_20261101.ps1, CheckOnly RELEASE_READY_NO_IDLE_GATE, BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True, exit 0. Bez czekania na idle.
- LIVE rollback: C:/Users/pboru/OpenFill/app__rollback__before_release_20261103_102057.
- LIVE weryfikacja po wydaniu: PID 27944, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, Core SHA zgodny, EXE SHA256 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52, OpenFill-open State=Running, MCP openfill_info OK, working=0.
- LC MultiHost zwracal dwukrotnie target_unavailable/fetch failed na PBOX1 przy CheckOnly; RDC na PBOX1 pozostawal online. Uzyto RDC do CheckOnly/release/weryfikacji bez zmiany hosta projektu.

## Realne acceptance
- LIVE task: OF-20261103-1023-d783, runId 20261103_072331_e73367, example.com, outcome=success.
- 3 model calls, 3 tool calls, 0 tool errors, 1 pageReadRequest, 0 repeatedPageReadRequests, 1 cdpReadCalls, toolElapsedMs=1151, modelMs=12018, costUsd=0.00111632.
- Rzeczywisty rekord LIVE: C:/Users/pboru/OpenFill/data/stable/logs/usage_202611.ndjson, kind=task, taskKey=OF-20261103-1023-d783, cdpReadCalls=1. Tytul i pierwszy akapit odczytane poprawnie.
- Bez wysylek, zmian na kontach i innych dzialan skutkowych.

## Status i kontynuacja
- E03 podzakres CDP read-call telemetry: SOURCE DONE, DEV ACTIVE, LIVE ACTIVE, REAL ACCEPTANCE PASS.
- E03 calosciowo PARTIAL: wiarygodna liczba unikalnych odczytow DOM, fizycznych odczytow poza CdpSession i analiza nieidentycznych zbednych odczytow nie sa ukonczone.
- D01 WhatsApp, D02 LinkedIn: rzeczywisty send acceptance pozostaje otwarty, wymaga zatwierdzonego celu/tresci, brak przypadkowych wysylek.
- D03 Airbnb ANULOWANE 2026-11-02.
- Harmonogram wake bez zmian, rotacja nowego leada OF-000040..049; nie nadpisywano handoffu poprzedniego leada, nastepny handoff przy 049.
