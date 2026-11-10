# OF-000045: E03, redukcja schematow narzedzi dla prostych odczytow URL, LIVE

Wake: OF-000045-A01-095102. Silnik: 2026-11-03 09:51:02 +01:00 (Warszawa).
Host SOURCE/DEV/LIVE: PBOX1. Lead OF-000040..049.

## Rozpoznany koszt
- Poprzedni LIVE OF-000044 runId 20261103_083522_c22944 (task OF-20261103-1135-ef76): na prostym odczycie example.com model otrzymywal wszystkie schematy narzedzi (w tym WhatsApp, LinkedIn, Airbnb, uslugi plikow i wysylki).
- Request pierwszej tury: 48702 znakow, 7957 tokenow input. Cale zadanie: 2 model calls, 17464 input tokens, 7954 cached, 121 output, costUsd 0.00109104, 1 pageReadRequest, 7 CDP, success.
- W SOURCE aktywny ToolRegistry.Build zawiera 49 narzedzi (nie 53). Wczesniejsze pelne listowanie z nazwy linii kodu nie bylo miarodajne; log runtime jest rozstrzygajacy.

## Zmiana SOURCE
- src/OpenFill.Core/Agent/AgentRunner__v0.29__2026-10-29_2314.cs: nowy profil simple_url_read z 18 narzedziami, tylko dla biezacego, jednoznacznego read-only polecenia rozpoczynajacego sie od odczytaj/przeczytaj/read/retrieve, o dlugosci <=900 znakow, dokladnie jednym URL HTTP(S), bez czasownikow skutkowych typu send/save/post/pay/delete/change.
- W kontynuowanych taskach bierze biezacy Task z separatora \n\nTask: , nie historyczny URL w preambule. Pozostale zadania, w tym D01 WhatsApp, D02 LinkedIn, operacje skutkowe i zlozone zadania dostaja pelny zestaw.
- W profilu pozostawiono navigate, read_view, get_page, read_text, view_digest, inspect_view, observe_query, file_query, page_capture, run_js, wait, screenshot, act, act_locator, verify_view, get_network, finish, ask_user. Wszystkie handlery lokalne pozostaly bez zmian.
- Event agent.tool_schema_profile raportuje profile, exposedTools, totalTools, schemaChars. Brak nowego MCP i brak dodatkowego odczytu DOM.
- Backup: ten sam plik .before_of000045.

## Publikacja
- dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000045 -v:q: exit 0, tylko dotychczasowe ostrzezenia kompilacji.
- Core SHA256: ACD0DF63EE81504CD55863D586111A95CC0FD1E2DF8EBE512473FD6BF6F99525.
- DEV: tools/of_000045_activate_dev.ps1 exit 0, PID 15660, backup staging/of_000045_backup_pre_dev_activation.
- CheckOnly: RELEASE_READY_NO_IDLE_GATE. LIVE: tools/of_imp_release_live_ALWAYS_20261101.ps1 exit 0, BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True. Nie czekano na bezczynnosc.
- LIVE po wdrozeniu: PID 24864, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, Core SHA zgodny, EXE SHA256 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52, OpenFill-open Running, MCP OK, working=0.
- Rollback: C:/Users/pboru/OpenFill/app__rollback__before_release_20261103_115447.

## Acceptance rzeczywistego LIVE
- Task OF-20261103-1155-a727; runId 20261103_085547_15e742; example.com; outcome success, poprawny tytul Example Domain i pierwszy akapit.
- Log agent.tool_schema_profile: profile=simple_url_read, exposedTools=18, totalTools=49, schemaChars=17086.
- Pierwszy request: requestChars 26814 (vs 48702), inputTokens 4731 (vs 7957). Tool navigate(view_mode=content, expected_view_type=detail, wait_ms=10000) wykonany w kroku 1; drugi krok finish. Brak niepotrzebnego get_page startowej strony.
- 2 model calls, 2 tool calls, toolErrors=0, 1 pageReadRequest, 0 repeatedPageReadRequests, 7 CDP, modelMs=6157, toolElapsedMs=1234; inputTokens=11039, cachedTokens=4728, outputTokens=105, requestChars=58167, costUsd=0.00073088.
- Porownanie z podobnym OF-000044: input tokens 17464->11039, redukcja 6425 (36,8%); costUsd 0.00109104->0.00073088, redukcja 0.00036016 USD (33,0%). Porownanie pojedynczych przebiegow, nie gwarantowana poprawa wszystkich zadan. CDP nie zwiekszylo sie: 7->7. Narzedzia 49->18 tylko w ograniczonym profilu.
- Potwierdzono rowniez dzialanie portu MCP LIVE i prawidlowej wersji biblioteki.

## Dalsza praca
- E03 nadal PARTIAL dla analizy roznorodnych zadan; dalsze korzysci potencjalnie ze skrocenia instrukcji systemowych w prostych odczytach, ale nie bez danych produkcyjnych.
- D01 WhatsApp, D02 LinkedIn: realne skutkowe send acceptance wciaz bez autoryzowanego odbiorcy i tresci; nie wykonywac przypadkowych wysylek.
- D03 Airbnb ANULOWANE 2026-11-02. Harmonogram wake bez zmian. Planowa rotacja po OF-000049.
