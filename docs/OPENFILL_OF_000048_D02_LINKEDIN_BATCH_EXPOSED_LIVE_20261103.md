# OF-000048: D02 LinkedIn send_batch udostępnione w LIVE

Wake: OF-000048-A01-105343. Godzina silnika: 2026-11-03 10:53:43 +01:00 Warszawa.
Host SOURCE, DEV i LIVE: PBOX1. Lead OF-000040..049.

## Faktyczny brak
- ToolRegistry__v0.22__2026-10-29_2314.cs: linkedin_send_batch było rejestrowane wyłącznie gdy _candidateStagingOnly=true. LIVE miało 50 narzędzi i nie widziało tej funkcji.
- LinkedInSendBatchService__v0.1__2026-11-01.cs już istniał. Waliduje od 1 do 50 odbiorców, dokładny profil nadawcy /in/, profil odbiorcy /in/, adres wątku /messaging/thread/, unikalność i długość tekstu.
- Preflight LinkedInThreadPreflight wymaga dokładnego jednego widocznego nadawcy i odbiorcy, zgodnego wątku i jednego edytora. ExecuteBatchAsync zapisuje operationId i attempted do SQLite PRZED efektem. Adapter LinkedInCurrentThreadSendAdapter po próbie kliku wymaga nowej wychodzącej wiadomości w tym samym wątku i zapisuje dowód. Nie powtarza automatycznie attempted/uncertain. UI evidence nie jest potwierdzeniem dostarczenia.
- procedures/registry.json zachowuje linkedin.send_batch status=candidate. Kandydat nie jest wybierany automatycznie. OF-000023 miał test rejestracji QA 14 checks PASS w DEV, ale nie posiada realnej akceptacji wysyłki. Nie powtarzano historycznego QA.

## Zmiana SOURCE
- Plik src/OpenFill.Core/Agent/ToolRegistry__v0.22__2026-10-29_2314.cs: usunięto wyłącznie warunek if (_candidateStagingOnly) przed rejestracją narzędzia linkedin_send_batch.
- Nowy opis wyraźnie wymaga explicit authorized=true, dokładnych danych i zatwierdzonej treści, podkreśla status candidate oraz brak potwierdzenia dostarczenia.
- Service nadal jest wywoływany z stagingOnly:true jako jawny bypass candidate (istniejąca ścieżka); nie zmieniono procedury, silnika wysyłki, automatycznego routingu, pojedynczej wysyłki ani WhatsApp.
- Backup SOURCE: ten sam plik .before_of000048.

## Build i release
- dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000048 -v:q: exit 0, jedynie poprzednie ostrzeżenia.
- SHA256 OpenFill.Core.dll: 7549357258CC90E319AA1B5DFF0A63A8CD9BBED90B62E6EF24732CB218B551D5.
- DEV aktywowany tools/of_000048_activate_dev.ps1, PID 11636, backup staging/of_000048_backup_pre_dev_activation.
- CheckOnly RELEASE_READY_NO_IDLE_GATE. LIVE tools/of_imp_release_live_ALWAYS_20261101.ps1: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True, exit 0. Bez czekania na idle.
- LIVE PID 9204, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, zgodny Core SHA, EXE SHA256 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52, phase OpenFill-open Running, MCP OK, working=0.
- Rollback C:/Users/pboru/OpenFill/app__rollback__before_release_20261103_125626.

## Sprawdzenie realnego LIVE, bez efektów zewnętrznych
- OpenFill task OF-20261103-1257-f523, runId 20261103_095724_c4cefe, outcome success. Model otrzymał profil full, exposedTools=51, totalTools=51, schemaChars=41343 (poprzednio 50). W odpowiedzi wskazał linkedin_send_batch.
- W logu tylko finish, żadnego linkedin_send_batch, 0 błędów narzędzi, 0 pageReadRequests, 0 cdpReadCalls. 1 model call, costUsd=0.0008712. Zewnętrznych wiadomości nie wysłano.
- Zmierzono faktyczną ekspozycję narzędzia, NIE akceptację skutkową wysyłki.

## Dalsze kroki
- D02: funkcja dostępna LIVE jako candidate explicit-only; nie ma dowodu realnej wysyłki ani odbiorców/tekstów zatwierdzonych dla testu, więc nie oznaczać D02 jako DONE.
- D01 WhatsApp send_batch dostępny LIVE, realny acceptance nadal pending. D03 Airbnb anulowane 2026-11-02.
- E03 dalsza analiza wyłącznie realnych, rozmaitych zadań, bez powtarzania fixture example.com. Harmonogram wake bez zmian, rotacja leada po OF-000049.
