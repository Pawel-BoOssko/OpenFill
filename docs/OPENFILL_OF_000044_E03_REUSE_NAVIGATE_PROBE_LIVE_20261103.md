# OF-000044: E03, ponowne uzycie struktury strony po navigate(content), LIVE

Wake: OF-000044-A01-093054; 2026-11-03 09:30:54 +01:00 (Warszawa).
Host: PBOX1, projekt OF, ten sam lead OF-000040..049.

## Problem
Dla navigate(view_mode=content, expected_view_type=detail) kod po poprawnym rozpoznaniu oczekiwanego widoku przez SemanticViewInspector.Script uruchamial niezaleznie read_view(), ktore odpytywalo ten sam skrypt ponownie. W poprzednim LIVE OF-000043 wykonanie OF-20261103-1117-a152 (runId 20261103_081707_1a2dfd) mialo 8 cdpReadCalls dla takiej nawigacji z jednym viewProbe i poprawnym oczekiwanym widokiem.

## SOURCE
- Plik: src/OpenFill.Core/Agent/ToolRegistry__v0.22__2026-10-29_2314.cs.
- Kopia: ten sam plik z sufiksem .before_of000044.
- navigate(content) zapamietuje ostatni JsonObject z lokalnego sprawdzenia oczekiwanego widoku, o ile view_type jest zgodny.
- Wewnatrz tej samej operacji przekazuje go do read_view przez prywatny argument _fused_structure (nieobecny w zewnetrznym JSON Schema modelu).
- read_view uzywa DeepClone tej struktury bez ponownego RunJsAsync(SemanticViewInspector.Script). Gdy typ nie zostal potwierdzony albo nie ma expected_view_type, standalone read_view nadal pobiera swieza strukture.
- Istniejacy event view_read.read_completed zawiera reusedNavigateProbe: true/false.
- Brak zmian w odczytach zapisanych capture_id, filtrach, domenach, akcjach piszacych i wysylkowych.

## Publikacja
- dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000044 -v:q: exit 0, dotychczasowe ostrzezenia, bez bledow.
- Core SHA256: A2A91EC88ADE612B56C50238CB5366E47CE22FB99D5DD1DB5E0D53EC2A254BDE.
- DEV: tools/of_000044_activate_dev.ps1, exit 0, PID 23304; backup staging/of_000044_backup_pre_dev_activation.
- LIVE: CheckOnly RELEASE_READY_NO_IDLE_GATE; tools/of_imp_release_live_ALWAYS_20261101.ps1, exit 0; BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True. Nie czekano na idle.
- LIVE po release: PID 12588, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, Core SHA zgodny, EXE SHA256 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52, faza OpenFill-open Running, MCP OK, working=0.
- Rollback: C:/Users/pboru/OpenFill/app__rollback__before_release_20261103_113431.

## Realny LIVE acceptance
- Test 1: OF-20261103-1135-ef76, runId 20261103_083522_c22944; success, 2 model calls, 1 pageReadRequest, 7 CDP, costUsd 0.00109104. Model wybral navigate(content) bez expected_view_type, zatem reusedNavigateProbe=false. Test potwierdzil brak regresji.
- Test 2: OF-20261103-1135-6fc0, runId 20261103_083557_dbc7c0; success. Model wywolal navigate(url=https://example.com/, view_mode=content, expected_view_type=detail); OBSERVED=detail, READY=True, viewProbes=1, reusedNavigateProbe=true, widoczny tytul i pierwszy akapit prawidlowe.
- Test 2: 2 model calls, 2 tool calls, toolErrors=0, pageReadRequests=1, repeatedPageReadRequests=0, cdpReadCalls=7, costUsd=0.00104948, toolElapsedMs=872, modelMs=9271.
- Porownanie tej samej sciezki expected_view_type=detail z OF-000043: CDP 8 -> 7, jeden mniej (12,5%). Kosztu/wall-clock nie interpretowac jako efektu przyczynowego z pojedynczych przebiegow o roznych warunkach.

## Kontynuacja
- E03 pozostaje czesciowo otwarte dla pomiarow unikalnych/niepotrzebnych DOM reads w innych rzeczywistych operacjach. Nie trzeba powtarzac fixture example.com.
- D01 WhatsApp i D02 LinkedIn: skutkowy send acceptance wciaz bez autoryzowanych adresatow i tresci. D03 Airbnb ANULOWANE 2026-11-02, nie wznawiac.
- Harmonogram wake i rotacja bez zmian; nastepny lead po OF-000049.
