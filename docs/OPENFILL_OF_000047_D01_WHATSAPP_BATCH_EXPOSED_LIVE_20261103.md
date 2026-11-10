# OF-000047: D01 WhatsApp send_batch candidate dostępny w LIVE, bez rzeczywistej wysyłki

Wake: OF-000047-A01-103051; godzina silnika 2026-11-03 10:30:51 +01:00 Warszawa.
Host SOURCE/DEV/LIVE: PBOX1; lead OF-000040..049.

## Diagnoza
- D01 whatsapp.send_batch był kompletnie zaimplementowany w src/OpenFill.Core/Agent/WhatsAppBatchService__v0.1__2026-10-31.cs, ale ToolRegistry__v0.22__2026-10-29_2314.cs:503 rejestrował narzędzie wyłącznie przy _candidateStagingOnly. LIVE nie udostępniał użytkownikowi batch send mimo istniejącego serwisu.
- ProcedureCatalog: procedures/registry.json, whatsapp.send_batch status=candidate (nieweryfikowane skutkowo). ProcedureExecutor z definicji odrzuca candidate, gdy allowCandidateForStaging=false. Odrębne narzędzie whatsapp_send pozostaje dostępne; linkedin_send_batch nadal DEV.
- Istniejący QA OF-000025: OF025_WHATSAPP_BATCH_15_CHECKS_OK, prawdziwy SQLite i symulowany adapter, bez zewnętrznego send; preflight OF-000024 wymaga otwartego wątku i widocznego kompozytora przed durable attempted. Nie powtarzano zaliczonych testów.
- Istniejąca procedura wymaga verified sender (whatsapp.AccountMatchesAsync), exact recipient phone, wiadomości, request_id, requester/chat, jawnego authorized=true, per-target ledger, evidence SHA256 oraz nie ponawia attempted/uncertain po restarcie. Odpowiedź sent_ui nie jest dowodem dostarczenia.

## SOURCE
- ToolRegistry.cs: rejestracja whatsapp_send_batch także w LIVE bez warunku _candidateStagingOnly. Zmieniono opis na explicit-only candidate in LIVE, wymagający dokładnych numerów i treści.
- Wyłącznie to jedno narzędzie wywołuje istniejący WhatsAppBatchService.SendBatchAsync z allowCandidateForStaging=true po sprawdzeniu authorized=true; status procedury w registry pozostaje candidate, zatem auto-routing jej nie wybiera.
- whatsapp_send_effect i linkedin_send_batch pozostają tylko DEV; inne funkcje niezmienione.
- SOURCE backup: src/OpenFill.Core/Agent/ToolRegistry__v0.22__2026-10-29_2314.cs.before_of000047.

## Build i release
- dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000047 -v:q, exit 0, tylko wcześniejsze ostrzeżenia.
- SHA256 OpenFill.Core.dll: 39786F5E7A52BC4F44623349902C0A954E7A046D4ACDCB5882AA2A159599DB2A.
- DEV: tools/of_000047_activate_dev.ps1 exit 0, PID 25540, backup staging/of_000047_backup_pre_dev_activation.
- CheckOnly RELEASE_READY_NO_IDLE_GATE. LIVE tools/of_imp_release_live_ALWAYS_20261101.ps1: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True, exit 0. Bez czekania na idle.
- LIVE: PID 18424, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, zgodny SHA Core, EXE SHA256 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52, OpenFill-open Running, MCP True, working=0.
- Rollback C:/Users/pboru/OpenFill/app__rollback__before_release_20261103_123804.

## Acceptance LIVE bez efektu zewnętrznego
- Pierwsze polecenie weryfikacyjne OpenFill odrzucone jako rejected_rewrite_needed: zbędne instrukcje zakazu i ostrożności, nie błąd narzędzia ani blokada uprawnień. Przeredagowano do prostej prośby o nazwę narzędzia.
- Task OF-20261103-1239-3097, runId 20261103_093918_9bbe9c: success, odpowiedź modelu identyfikuje whatsapp_send_batch. Log agent.tool_schema_profile: full, exposedTools=50, totalTools=50, schemaChars=39902. Poprzednio 49 w LIVE.
- Log narzędzi: wyłącznie finish, toolCalls=1, toolErrors=0, pageReadRequests=0, cdpReadCalls=0, 1 model call, costUsd=0.0008365. Test nie wywołał send_batch ani whatsapp_send i nie wysłał żadnych wiadomości.
- Wniosek: ekspozycja produkcyjnego narzędzia zweryfikowana, nie jest to potwierdzenie działania wysyłki ani jej dostarczenia.

## Pozostałe
- D01 status PARTIAL: opt-in batch dostępny LIVE, realny send acceptance wymaga istniejących zatwierdzonych odbiorców/treści. Nie wysyłać testowych wiadomości do nieuzgodnionych osób.
- D02 LinkedIn wsadowe wysyłki nadal DEV candidate; D03 Airbnb ANULOWANE 2026-11-02; E03 partial dla innych zadań.
- Wake/rotacja niezmienione, następny lead po OF-000049.
