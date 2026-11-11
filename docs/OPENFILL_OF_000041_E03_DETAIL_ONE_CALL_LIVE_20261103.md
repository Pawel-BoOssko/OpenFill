# OF-000041: E03 detail content in one call, LIVE
Wake OF-000041-A01-083051, 2026-11-03 08:30:51 +01:00, Warszawa. Projekt OpenFill, SOURCE/DEV/LIVE PBOX1.

## Przyczyna
W runId 20261103_065702_9b9f9f (OF-000039) navigate(content) i read_view(detail) zwracaly dla example.com tylko tytul i linki. Dopiero kolejny read_view(auto,text_regex) dal pierwszy akapit: 5 wywolan modelu i 5 narzedzi. W runId 20261103_072331_e73367 (OF-000040) bylo 3 wywolania modelu i 3 narzedzia, bo po navigate(content) wymagano osobnego read_text. Poprzedni licznik cdpReadCalls pomijal wewnetrzne odczyty navigate.

## SOURCE
- ToolRegistry__v0.22__2026-10-29_2314.cs: read_view dla automatycznego widoku detail i target get_page dolacza do juz istniejacej projekcji kontrolnej widoczny tekst strony, read_text max_chars=1800, w tym SAMYM wywolaniu narzedzia. Form controls-only niezmienione.
- AgentRunner__v0.29__2026-10-29_2314.cs: navigate(view_mode=content/structure) liczy fusedNavigationRead i obejmuje licznik CdpReadCallScope. pageReadRequests zawiera teraz rowniez implicit read nawigacji.
- SOURCE backup: oba pliki .cs.before_of000041. Brak zmian w wysylkach, ledger i procedurach skutkowych.

## Wydanie i rollback
- dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000041 -v:q: exit 0, zero bledow.
- Core SHA256 F04AFEE31930B5C4EA517993883BDD5D8956352C7C0CE967EBD2C15D3E7A4482.
- DEV PID 7056, backup staging/of_000041_backup_pre_dev_activation, tools/of_000041_activate_dev.ps1 exit 0.
- LIVE release tools/of_imp_release_live_ALWAYS_20261101.ps1: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True, exit 0, bez oczekiwania na idle.
- Rollback LIVE C:/Users/pboru/OpenFill/app__rollback__before_release_20261103_103534.
- LIVE PID 27156, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, Core SHA zgodny, EXE SHA 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52, phase Running, MCP True, CheckOnly LIVE_ALREADY_CURRENT exit 0.
- LC MultiHost chwilowo zwrocil target_unavailable (fetch failed), RDC PBOX1 wykonalo weryfikacje, potem LC MultiHost dzialal. Przyczyna przerwy transportowej nieustalona.

## Acceptance real LIVE
- Task OF-20261103-1037-e09b, runId 20261103_073738_e9c309, https://example.com/; outcome success.
- navigate(content,expected_view_type=detail) zwrocil juz tytul i pierwszy angielski akapit w VISIBLE DETAIL TEXT, nastepna tura modelu od razu finish.
- LIVE usage_202611.ndjson kind=task: calls 2, toolCalls 2, toolErrors 0, pageReadRequests 1, repeatedPageReadRequests 0, cdpReadCalls 9, inputTokens 17437, cachedTokens 7927, modelMs 26709, costUsd 0.00109077.
- OF-000040 podobne zadanie: 3 calls, 3 tools, 24717 inputTokens, costUsd 0.00111632. To porownanie orientacyjne, nie kontrolowany benchmark.
- cdpReadCalls=9 oznacza wybrane requests CdpSession, NIE dziewiec unikalnych odczytow DOM. Wczesniejsze cdpReadCalls=1 pomijalo fused navigation.
- Brak wysylek i dzialan skutkowych.

Status: SOURCE DONE, DEV ACTIVE, LIVE ACTIVE, real acceptance PASS. E03 pozostaje czesciowo otwarte. D01/D02 skutkowe acceptance bez autoryzowanego celu pozostaje otwarte. D03 Airbnb ANULOWANE. Wake/rotacja OF-000040..049 bez zmian.
