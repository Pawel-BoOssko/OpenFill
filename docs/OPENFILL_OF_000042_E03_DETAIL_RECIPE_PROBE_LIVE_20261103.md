# OF-000042: E03 optymalizacja weryfikacji receptur dla widokow detail/form, LIVE

Wake: OF-000042-A01-085050, 2026-11-03 08:50:50 +01:00 (Warszawa).
Host SOURCE, DEV i LIVE: PBOX1. Projekt: of. Lead kontynuowany po OF-000041.

## Stan zastany i OF-000041
- Zweryfikowano LIVE po OF-000041 skryptem tools/of_000041_live_verify.ps1: PID 27156, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, Core SHA256 F04AFEE31930B5C4EA517993883BDD5D8956352C7C0CE967EBD2C15D3E7A4482, phase Running, MCP OK.
- Acceptance OF-000041: task OF-20261103-1051-9349, runId 20261103_075133_bd7bb8, outcome success, modelCalls 3, toolCalls 3, total cdpReadCalls 11, pageReadRequests 2, costUsd 0.00114928. Navigate(view_mode=content) wykonalo 8 odczytowych CDP i zwrocilo tytul oraz wymagany akapit bez kolejnego odczytu po nawigacji.
- Wcześniejszy OF-000039 na tej samej stronie: 5 model calls, 3 odczyty strony z roznymi argumentami, marnowal kroki przez brak widocznego tekstu w kompaktowym widoku detail. Zmiana OF-000041 usuwa ten brak w jednym odczycie. Nie ma dowodu stalej redukcji kosztu modelu dla dowolnego zadania.

## Diagnoza OF-000042
- AutoViewReadPlanner.Choose dla view_type innego niz list/table/inbox zawsze zwraca get_page. Mimo to ToolRegistry.read_view dotychczas odpytywal CaptureRecipeLiveProbe.SelectAsync i rejestr receptur dla wszystkich widokow, rowniez detail/form.
- Dla detail/form nie bylo mozliwosci uzycia tych receptur do automatycznej selekcji drogi odczytu. Nadmiarowe operacje CDP byly zbedne w tej sciezce.

## SOURCE i publikacja
- src/OpenFill.Core/Agent/ToolRegistry__v0.22__2026-10-29_2314.cs: wywolanie CaptureRecipeLiveProbe.SelectAsync i skanowanie contentRecipes tylko gdy manifest view_type nalezy do list/table/inbox. currentHost nadal ustawiany dla wszystkich poprawnych URL. Routing, podpis, account scope i weryfikacja receptur dla widokow masowych zachowane.
- Kopia SOURCE przed zmiana: src/OpenFill.Core/Agent/ToolRegistry__v0.22__2026-10-29_2314.cs.before_of000042.
- dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000042 -v:q: exit 0, dotychczasowe ostrzezenia, bez nowych bledow kompilacji.
- Nowy OpenFill.Core.dll SHA256: 4A23A9B161134994E7488CBE624F22B90BB7EA85FED3CE96DAB999EAF407B011.
- DEV: tools/of_000042_activate_dev.ps1 exit 0, PID 23104, backup staging/of_000042_backup_pre_dev_activation.
- CheckOnly: RELEASE_READY_NO_IDLE_GATE. LIVE tools/of_imp_release_live_ALWAYS_20261101.ps1 exit 0: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True. Bez bramki idle.
- LIVE po wydaniu: PID 18904, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, Core SHA zgodny, EXE SHA256 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52, OpenFill-open Running, MCP OK, working=0.
- Rollback: C:/Users/pboru/OpenFill/app__rollback__before_release_20261103_105446.

## Realne acceptance LIVE
- Task OF-20261103-1055-ef8d, runId 20261103_075547_1b4207, example.com: outcome success. Zwraca poprawny tytul Example Domain i pierwszy akapit.
- 3 model calls, 3 tool calls, 0 tool errors, 2 pageReadRequests, 0 repeatedPageReadRequests, 9 cdpReadCalls, toolElapsedMs 2317, modelMs 32498, costUsd 0.00148284.
- Poczatkowy get_page na www.google.com: cdpReadCalls=2; navigate(view_mode=content) na example.com: cdpReadCalls=7, recipeCandidates=0, view_type=detail, output zawiera VISIBLE DETAIL TEXT, prawidlowy finalny wynik.
- Porownanie samej nawigacji: 8 odczytowych CDP przed OF-000042, 7 po zmianie, 1 mniej (12,5%). Calkowity licznik 11 -> 9, ale rozne strony startowe i warunki. Nie mozna wywodzic poprawy kosztu modelu z jednego przebiegu; koszt w teście po zmianie wzrosl z 0.00114928 do 0.00148284 USD.
- Nie testowano skutkowych wysylek ani operacji na kontach.

## Kontynuacja
- E03 pozostaje PARTIAL: fizyczne CDP licznik obejmuje okreslone metody CdpSession, nie wszystkie mozlwie sciezki; ocena zbednych nieidentycznych odczytow powinna opierac sie na rzeczywistych runach.
- Konkretny kandydat OF-000043: dodatkowy get_page na stronie startowej przed navigate do jawnie podanego URL (wywolany w obu ostatnich testach), co kosztuje ture modelu, czesc tokenow i odczyt CDP. Wymaga precyzyjnej zmiany orkiestracji, nie sztywnego limitu.
- D01 WhatsApp/D02 LinkedIn skutkowe acceptance nadal wymagaja autoryzowanego celu i tresci. D03 Airbnb anulowane 2026-11-02. Harmonogram obudzen bez zmian. Nastepny lead w rotacji OF-000050.
