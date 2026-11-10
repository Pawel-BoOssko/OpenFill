# OF-000043: E03 first-turn navigation bez get_page na stronie startowej, LIVE
Wake: OF-000043-A01-091101. Data silnika: 2026-11-03 09:11:01 +01:00 (Warszawa).
Host SOURCE, DEV, LIVE: PBOX1. Lead OF-000040..049.

## Zweryfikowany problem
- OF-000042 LIVE runId 20261103_075547_1b4207: startowa karta Google, get_page w kroku 1, navigate(view_mode=content) w kroku 2, finish w kroku 3. 3 model calls, 2 pageReadRequests, 9 cdpReadCalls, koszt USD 0.00148284.
- Przyczyna w SOURCE: Hosting/AppHost.BuildPrompt doklejal do kazdego zadania typu continuation: "Look at the page first (get_page) before you act.", takze przy nowej karcie, niebedacej karta poprzedniego zadania.
- Agent/ToolRegistry.InitialGenericViewAsync dodatkowo wstrzykiwal semantyczny widok Google przed pierwsza tura modelu, chociaz biezacy task jawnie wskazywal example.com jako cel.

## Zmiany SOURCE
- src/OpenFill.Core/Hosting/AppHost__v0.23__2026-10-29_0807.cs: dla nowej karty continuation NIE sugerowac get_page na stronie startowej, wskazywac bezposrednia navigate(view_mode=content) do docelowego URL. Dla rzeczywiscie zachowanej/odtworzonej karty nadal mozna sprawdzic kontekst.
- src/OpenFill.Core/Agent/ToolRegistry__v0.22__2026-10-29_2314.cs: InitialGenericViewAsync pomija odczyt startowej karty, gdy biezace zadanie zaczyna sie od jawnego czasownika otworz/odczytaj/przejdz/read/open/visit/etc, ma jeden unikalny URL HTTP(S) i docelowa domena jest inna od startowej. W continuation URL wyznaczany z aktualnej czesci \n\nTask: ..., nie z historii zadania. Zdarzenie: view_inspection.initial_view_skipped_unrelated_url. Bez zmiany WhatsApp prefetch oraz bez automatycznej nawigacji bez decyzji modelu.
- Kopie: oba pliki z sufiksem .before_of000043. AgentRunner przed tym wake tez zabezpieczony .before_of000043, lecz niezmieniany.

## Build, DEV, LIVE
- dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000043 -v:q: exit 0, tylko dotychczasowe ostrzezenia.
- OpenFill.Core.dll SHA256: A0B24B326077FEFA6CC7A6AE27337B4BE3EEB3B4EC27B1AE50A95E93BA44FEBD.
- DEV aktywacja tools/of_000043_activate_dev.ps1: exit 0, PID 25128, backup staging/of_000043_backup_pre_dev_activation.
- LIVE: CheckOnly RELEASE_READY_NO_IDLE_GATE; tools/of_imp_release_live_ALWAYS_20261101.ps1 exit 0, BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True. Bez bramki idle.
- LIVE po wdrozeniu: PID 23216, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, Core SHA zgodny, EXE SHA256 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52, OpenFill-open Running, MCP OK, working=0.
- Rollback C:/Users/pboru/OpenFill/app__rollback__before_release_20261103_111617.

## Realny acceptance
- Task OF-20261103-1117-a152, runId 20261103_081707_1a2dfd: example.com, outcome success, wymagany tytul i pierwszy akapit.
- Pierwszy log initial_view_skipped_unrelated_url: startHost www.google.com, destinationHost example.com. Step 1: navigate(url=https://example.com/, view_mode=content, expected_view_type=detail). Step 2: finish. BRAK get_page na stronie startowej.
- 2 model calls, 2 tool calls, 0 tool errors, 1 pageReadRequest, 0 repeatedPageReadRequests, 8 cdpReadCalls, modelMs 6789, toolElapsedMs 1201, costUsd 0.00109363.
- Porownanie z OF-000042 w podobnym zadaniu: model 3 -> 2, page reads 2 -> 1, CDP 9 -> 8, koszt 0.00148284 -> 0.00109363 USD (26,2% mniej w tej probie). Rozne warunki przebiegow i latencja API ograniczaja uogolnianie wyniku; jedna proba wystarczy do potwierdzenia skutecznej sciezki.
- Do kontynuacji: D01/D02 skutkowy acceptance bez podanych autoryzowanych odbiorcow i tresci pozostaje otwarty. E03 telemetry/cost analysis PARTIAL, mimo zalatwienia konkretnej redundantnej sciezki. D03 Airbnb ANULOWANE 2026-11-02. Harmonogram wake bez zmian, kolejna planowa rotacja po OF-000049.
