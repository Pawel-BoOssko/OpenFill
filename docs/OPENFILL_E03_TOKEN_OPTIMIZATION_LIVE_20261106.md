# OpenFill: produkcyjna optymalizacja tokenów, 2026-11-06

Host: PBOX1. SOURCE: C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill.
Priorytet użytkownika: tylko niepotrzebne tokeny, nie czas narzędzi.

## Pomiar przed zmianami
Skrypt: tools/of_usage_prod_audit_20261106.py. Okno 30 godzin do 2026-11-06 16:15 UTC.
11 zakończonych zadań: 2 success, 9 partial. Wywołania modelu: 769. Input: 14 531 521 tokenów, w tym cached 13 156 853, uncached 1 374 668. Output: 86 371, w tym reasoning 53 326 (nie sumować reasoning drugi raz). Koszt 0,312221 USD.
Schematy narzędzi wysyłane w 769 wywołaniach: 33 260 673 znaków łącznie, średnio 43 252 znaki. To znaki JSON, NIE tokeny i nie oszczędność kosztowa.
Przebieg LH.pl runId 20261106_131116_bd27db, task OF-20261106-1411-a72d: 244 model calls, 0,11347 USD, outcome partial, 132 odczyty, 8 repeated reads. W app logs: 50 compactions, 41 search_logs, 50 navigate, 38 get_page. Nie uznawać wszystkich wywołań za zbędne bez identyfikacji postępu.

## Zmiana pierwsza: selektywny profil schematów (E03)
Pliki:
- src/OpenFill.Core/Agent/SingleHostToolProfile__v0.1__2026-11-06.cs (nowy)
- src/OpenFill.Core/Agent/AgentRunner__v0.29__2026-10-29_2314.cs
- Backup AgentRunner: .before_singlehost_20261106.
Gdy treść zadania zawiera wyłącznie URL jednego hosta, pomijane są narzędzia specyficzne dla niezwiązanych dostawców: airbnb_, linkedin_, whatsapp_ i google_meta_view_audit. Wzmianka o danym dostawcy zachowuje jego narzędzia. Zadanie bez URL, z różnymi hostami lub niejednoznaczne zachowuje pełen zestaw. Generic browser/file/auth/task-control bez zmian. Profil simple_url_read ma pierwszeństwo jak dotychczas.
Test w realnym ToolRegistry: 43 290 -> 32 436 znaków, tj. -25,07%; wszystkie testy graniczne PASS.
Pierwsze wydanie: DEV PID 24608, LIVE PID 19092, Core SHA C710A71B33B7B9CBB6925A961BB0CA2A7F413F59F2ED860DF55F5291051415E2, rollback C:\Users\pboru\OpenFill\app__rollback__before_release_20261106_172230, independent verify PASS.
Naturalny test produkcyjny: OF-20261106-1725-6a74, runId 20261106_162518_73feb4, outcome success, 4 calls, 0 tool errors, 32 994 input tokens (23 432 cached), 373 output tokens, 0,00137702 USD. App log: profile=single_host, exposedTools=39 z 52, schemaChars=32 476. Widok example.com zwrócił tytuł Example Domain i brak H1, zgodne z niezależnym odczytem HTML.
To dowodzi redukcji schematu i poprawnego uruchomienia, NIE ilości tokenów zaoszczędzonych na zadaniu (brak kontrfaktycznego testu tego samego zadania na starym profilu).

## Zmiana druga: compaction notes bez powtórzeń
Plik: src/OpenFill.Core/Agent/PromptHistoryCompactor__v0.1__2026-10-23_1054.cs; backup .before_search_note_20261106.
W checkpointach deduplikowane są identyczne wpisy search_logs, usuwane notatki wait i screenshot nieprzenoszące trwałych faktów; zachowane są wcześniejsze dowody akcji. Tekst checkpointu instruuje korzystanie z search_logs tylko w potrzebie, precyzyjnie i bez powtarzania tych samych zapytań bez nowych dowodów. Nie zmieniono algorytmu integralności model-call/tool-output ani progów compaction.
Test: tests/OpenFill.Tests/PromptHistoryTests__v0.1__2026-10-23_1100.cs, backup .before_search_note_20261106. W Program__v0.25__2026-10-29_2314.cs dodano tryb history-only, backup .before_historyonly_20261106.
Test historii 372/372 PASS; MCP 212/212 PASS; dotnet publish Release exit 0.
Drugi LIVE: DEV PID 23852, LIVE PID 20824, Core SHA256 79227A504FB58DD6E7737D47A38EEECF1148FAA82FD59F9499D1D5C5981A020B, MCP true, phase Running, staging SHA=LIVE SHA potwierdzone niezależnie, rollback C:\Users\pboru\OpenFill\app__rollback__before_release_20261106_173231.

## Ograniczenia i dalsza analiza
Brak jeszcze porównywalnych zadań produkcyjnych po wdrożeniu kompaktora. Nie deklarować spadku liczby wywołań, kosztu ani tokenów bez takiego pomiaru.
Monitorować kolejne runId osobno: inputTokens, cachedTokens, outputTokens, reasoningTokens, model calls, schemaChars, historyCompactions, search_logs count, repeatedPageReadRequests, outcome i realne zweryfikowane efekty.
Nie skracać timeoutów, nie uruchamiać zbędnych testów modelowych, nie powtarzać niepewnych efektów zewnętrznych.
D02 realny odbiór wysyłek, D04 publikacje i GUI Stop E2E pozostają poza zakresem tej zmiany. Airbnb D03 anulowane, wake of wyłączone.

## Uzupełnienie po analizie skutków wdrożenia, 2026-11-06 17:38 CET
- Produkcyjny runId 20261106_162338_41b071, task OF-20261106-1723-e793, requester Forum Subiekta: infrastruktura, zakończony outcome=interrupted, stopReason=upgrade_interrupted przy wydaniu o 17:32:34 CET.
- Przebieg ten zużył 120 calls, 2 228 191 input tokens (2 039 110 cached), 14 823 output tokens, 0,0467107 USD. Nie zakładać, że całość tokenów była bezpowrotnie zbędna, bo wcześniejsze działania mogły mieć trwałe skutki i kontekst można odzyskać.
- Decyzja na przyszłość wynikająca z priorytetu użytkownika tokeny > czas: przed NIEPILNYM wdrożeniem sprawdzać aktywne zadania i preferować okno bez aktywnej pracy, zamiast wymuszać upgrade_interrupted. Nie zmieniono istniejącego skryptu kanonicznego, który nadal celowo nie ma bramki idle. Każdy model wdrażający musi świadomie zdecydować, czy wydanie jest pilne.
