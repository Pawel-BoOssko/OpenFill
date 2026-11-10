# OF-000049: D01, wymagany kontrakt items WhatsApp batch w LIVE, finalizacja leada

Wake: OF-000049-A01-111052, 2026-11-03 11:10:52 +01:00 (Warszawa).
SOURCE, DEV, LIVE: PBOX1. Rotacja: następne wybudzenie OF-000050 należy do nowego leada, harmonogram niezmieniony.

## Problem w istniejącym LIVE
- Po OF-000047 narzędzie whatsapp_send_batch jest dostępne w LIVE, ale model-visible schema deklarował tylko items: array, bez opisu pojedynczych pozycji. Model nie miał formalnego kontraktu pól target/message, mimo że handler ich oczekuje.
- Handler pomijał elementy tablicy o innym typie przez OfType<JsonObject>() i przepuszczał puste requester/chat; utrudniało to jednoznaczną identyfikację i powtarzalne wykonanie batch.
- Existing durable ProcedureExecutor, WhatsAppBatchService i WhatsAppBrowser pozostają bez zmian. Wysłanie wiadomości nadal wymaga authorized=true, weryfikacji własnego konta i dokładnych numerów/treści.

## SOURCE
- src/OpenFill.Core/Agent/ToolRegistry__v0.22__2026-10-29_2314.cs; kopia .before_of000049.
- Narzędzie whatsapp_send_batch: items teraz array of strict-ish schema objects, z required target i message, opisanymi dla modelu.
- Handler: wymaga niepustych requester/chat i 1-100 kompletnych obiektów {target,message}. Błędny JSON jest odrzucany przed wejściem do wykonawcy i przed jakąkolwiek próbą send. Idempotencja, ledger i kontrola profilu nadawcy bez zmian.
- Nie rozszerzano automatycznego routingu procedur candidate. D03 Airbnb nie dotknięte.

## Publikacja
- dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000049 -v:q: exit 0, tylko dotychczasowe warnings.
- SHA256 OpenFill.Core.dll 37D1514C755D4BAB865988DB193EA5C770690DCF83748957E5F5E0E4D848AB25.
- DEV tools/of_000049_activate_dev.ps1, exit 0, PID 5068, backup staging/of_000049_backup_pre_dev_activation.
- CheckOnly RELEASE_READY_NO_IDLE_GATE. LIVE tools/of_imp_release_live_ALWAYS_20261101.ps1: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True, bez idle.
- LIVE PID 4460, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, Core SHA zgodny, EXE SHA256 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52, phase Running, MCP OK, working=0.
- Rollback C:/Users/pboru/OpenFill/app__rollback__before_release_20261103_131348.

## Realne LIVE, bez send
- Task OF-20261103-1315-cbda, outcome success, model identyfikuje wymagane pola pojedynczego items jako target oraz message. Nie wywołano wysyłki; to acceptance schematu, nie skutku.
- D01 realny send nadal PARTIAL bez zatwierdzonego odbiorcy/tekstu; D02 LinkedIn send_batch w LIVE od OF-000048, realny send także PARTIAL.
- D03 Airbnb ANULOWANE zgodnie z decyzją właściciela 2026-11-02.

## Handoff
- Ostatnie wybudzenie leada: OF-000049-A01-111052.
- Nowy lead od OF-000050 niezależnie od handoffu; nie wyłączać harmonogramu.
- Dokument przekazania: D:/OpenBrowserLiteHandoffs/of/HANDOFF_CURRENT.md na SATANAMD (nadpisywany).
- Główny checkpoint: docs/OPENFILL_OF_IMP_20261031_EXECUTION_CHECKPOINT.md w SOURCE na PBOX1.
