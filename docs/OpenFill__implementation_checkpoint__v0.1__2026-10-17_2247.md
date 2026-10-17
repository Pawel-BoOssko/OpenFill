# OpenFill: checkpoint zmian w źródłach
Data: 2026-10-17 22:47 Europe/Warsaw
Stan: SOURCE + BUILD/TEST. Brak STAGED, DEPLOYED i LIVE-VERIFIED nowej wersji.
Plan: backlog v1.7, tylko część A. Bez edycji uzgodnionych tekstów OF-236/238.

1. Punkt 1: produkcja OpenFill 1.52 PID 17808, SessionId 1 (odczyt 22:21); MCP openfill_info odpowiedział, aktywne zadania=0. Git autosync/PBOX1 remote HEAD odczytany 22:44: 3247f009cae61af5d4e47b00cdac19ee9800c437. Nie jest dowodem publikacji tych nowych zmian.
2. OF-231: BeginAsync kolejkuje przy zajętych kartach zamiast przekraczać limit. Test: brak szóstej karty, start po zwolnieniu, brak zamknięcia aktywnych kart. Dodatkowa poprawka: nie gubi kart, jeśli zamknięcie ofiary zawiedzie, i nie tworzy wtedy karty ponad limit.
3. OF-233: nowy wewnętrzny tool report_verified_action w ToolRegistry, wymaga jednego odbiorcy i tekstu potwierdzenia zaobserwowanych równocześnie na bieżącej stronie. Dane zapisuje jako VerifiedActions w rekordzie MCP, z licznikiem confirmed_progress, total, remaining, last_confirmed_at, listą odbiorców i źródłem dowodu. Ten sam odbiorca nie liczy się ponownie. Nie mylić tego z model steps ani finalną gwarancją skutku zewnętrznego. ETA jest null. Progress budzi status, ale nie StartAsync.
4. OF-225/226: TabManager automatycznie wyszukuje zakończoną kartę po właścicielu requester/chat i znanej domenie. Reuse tylko dla tej samej pary; inny czat i inna domena nie przejmują karty. Gdy domena nowego zadania nie jest jednoznacznie znana, auto-reuse jest nadal niepełny.
5. OF-228/229: TabInfo przechowuje reason wyboru karty. IRunHost.TabDetailsOf / AppHost / McpTaskManager.Snapshot eksponują browser_tab: tab_id, domain, state, open, reuse_ready, reason, last_url (bez query). Nie deklaruje karty jako otwartej, jeżeli ją zwolniono.
6. OF-230: produkcyjny model releaseFinishedTabs true nadal może zwalniać fizyczną kartę; poprzednia próba jego zmiany była safety-denied, więc nie zmieniano tej funkcji. Wielogodzinne zatrzymanie strony jest nadal OPEN.
7. OF-212: wcześniejsza konkretna poprawka shutdown/rollback została safety-denied. Nie ponawiano jej przez inne narzędzie. OF-218/219 podlegają istniejącym testom syntetycznym; produkcyjny odbiór OPEN.

Dowody: pełny test Release (dotnet run OpenFill.Tests) 744 PASS, 0 FAIL, exit 0 dnia 17.10; log: Documents\OpenFill-Backups\Source-Patches\OpenFill_A3_fulltests__2026-10-17_2245.log. Wcześniejsze checkpointy: 716, 724, 728, 732, 736, 740 PASS / 0 FAIL. Skopiowane pliki pre-change i logi w Source-Patches.
Otwarty odbiór: test prawdziwej strony z realną usługową informacją o potwierdzeniu, zachowanie kart w interaktywnym GUI i po długim czasie, staging i rollback, realne wdrożenie 1.54. Produkcyjnego OpenFill 1.52 nie zatrzymano.