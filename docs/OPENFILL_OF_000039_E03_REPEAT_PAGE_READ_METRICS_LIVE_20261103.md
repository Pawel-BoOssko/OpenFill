# OF-000039: E03 pomiar powtarzanych żądań odczytu strony, LIVE

Wake: OF-000039-A01-075034, 2026-11-03 07:50:34 +01:00 (Warszawa).
Host SOURCE i LIVE: PBOX1. SOURCE: C:/Users/pboru/Downloads/OpenFill__v0.1__2026-10-04_1911/OpenFill.
Checkpoint poprzedni: docs/OPENFILL_OF_000038_E03_BATCH_ITEM_TIMINGS_LIVE_20261103.md.
Obowiązujący plan: docs/OPENFILL_SCALONY_PLAN_WDROZENIA_20261031.md.

## Powód zmiany
E03 miało już metryki kosztu zadania, narzędzi, decyzji read_view oraz operacji wsadowych, ale brakowało rzeczywistego licznika żądań odczytu stron i powtórek. Zgodnie z planem E03 rozróżniamy techniczne żądania odczytu od faktycznych wywołań DOM/CDP; zewnętrzne wywołanie narzędzia nie musi być pojedynczym odczytem DOM.

## SOURCE
- src/OpenFill.Core/Agent/AgentRunner__v0.29__2026-10-29_2314.cs: liczniki PageReadRequests i RepeatedPageReadRequests, zestaw sygnatur odczytu od ostatniej akcji browser.
- Jawnie zliczane narzędzia: get_page, read_text, view_digest, inspect_view, read_view bez capture_id, page_capture, observe_query. Nie zlicza read_view z capture_id (odczyt wyłącznie z pliku). Nie zlicza run_js jako wyłącznie odczytowego, bo może zmieniać DOM, ani navigate, bo to operacja nawigacji (często sama zwraca widok).
- Powtórzenie: identyczna nazwa i argumenty JSON bez pośredniej operacji navigate/act/act_many/act_locator/run_js/upload_file/whatsapp_send/whatsapp_open. Jest to kandydat do dalszej analizy, nie dowód marnotrawstwa (strona mogła się odświeżyć asynchronicznie).
- Tool/result zapisuje pageReadRequest i repeatedPageReadRequest obok elapsedMs.
- src/OpenFill.Core/Hosting/TaskRun__v0.1__2026-10-07_1732.cs: rekord usage kind=task i event session/usage otrzymują oba liczniki. Zero zmian w sekwencji wywołań, obsłudze akcji, ledgerze, idempotencji i kontach.

## Publikacja
- dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000039 -v:q: exit 0; jedynie dotychczasowe ostrzeżenia.
- Core SHA256: 2058D5C2D1E6DC02C0DF0915AE7C81543629086FE27AD7FAA0B62BCD6C4A5EF7.
- DEV backup: staging/of_000039_backup_pre_dev_activation. DEV staging/of_imp_20261031 aktywny po restarcie; DEV PID 21328.
- CheckOnly: RELEASE_READY_NO_IDLE_GATE, następnie tools/of_imp_release_live_ALWAYS_20261101.ps1: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True, exit 0, bez czekania na idle.
- LIVE rollback: C:/Users/pboru/OpenFill/app__rollback__before_release_20261103_095558.
- LIVE PID 17444, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, Core SHA zgodny. EXE SHA256 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52.
- CheckOnly po release: LIVE_ALREADY_CURRENT, exit 0.

## Rzeczywiste acceptance
- Zadanie produkcyjne OF-20261103-0957-ff91: odczyt tytułu i treści https://example.com/ z użyciem read_view, outcome success; 5 model calls/5 tool calls/0 tool errors, 5 kroków.
- LIVE data/stable/logs/usage_202611.ndjson: kind=task, runId=20261103_065702_9b9f9f, taskKey=OF-20261103-0957-ff91, pageReadRequests=3, repeatedPageReadRequests=0, toolElapsedMs=1956, inputTokens=42046, cachedTokens=32986, outputTokens=525, modelMs=60975, costUsd=0.00149836.
- Widoczna sekwencja: navigate(view_mode=content), read_view bez argumentów, read_view detail+regex, read_view auto+regex, finish. Trzy odczyty miały różne argumenty, więc dokładny licznik powtórek zero. Potencjalnie dwa odczyty były zbędne dla krótkiej statycznej strony, ale nie stwierdzono błędu aplikacji; kolejną optymalizację oceniać według kosztu/korzyści, nie na podstawie samej liczby.
- Brak jakichkolwiek działań skutkowych.

## Pozostałe zadania i przekazanie
- E03 częściowo gotowe. Brakuje wiarygodnych metryk rzeczywistych wywołań DOM/CDP, nie można ich uzyskać z samej liczby narzędzi bez instrumentacji adaptera. Następny lead może przeanalizować zbyt liczne nieidentyczne odczyty na przykładzie tego runId, ale nie należy tworzyć złożonego systemu na siłę.
- D01 WhatsApp, D02 LinkedIn: kod/procedury candidate, realne acceptance wysyłek na zatwierdzonych celach nadal otwarte. Nie wysyłać testowej wiadomości do niezadanego odbiorcy.
- D03 Airbnb anulowane przez właściciela 2026-11-02, nie wykonywać.
- Projekt rotuje na OF-000039. Handoff CURRENT na SATANAMD ma wskazywać niniejszy raport i główny checkpoint; wake na 0 należy do nowego leada, bez zatrzymywania harmonogramu.
