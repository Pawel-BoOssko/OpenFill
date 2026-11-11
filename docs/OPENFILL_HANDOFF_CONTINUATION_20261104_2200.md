# OpenFill: kontynuacja zamkniętego backlogu, 4 listopada 2026

Stan: PBOX1. Dokument uzupełnia docs/OPENFILL_A27_DEFAULT_CACHE_LIVE_20261104.md.
Źródło prawdy dla obecnej wersji LIVE: niezależny Get-FileHash oraz raporty release. Nie traktować kodu SOURCE jako automatycznie wdrożonego.

## A27, wykonane i zamknięte
- Poprawiono integrację CurrentTextCaptureCache z verified_live_view w PageCaptureService.
- Dwa pliki SOURCE, kopie *.before_a27_default_cache_20261104.
- Wydanie LIVE PID 19828, CORE SHA256 713BBD7655410CF0518C9BA12DEB13B86C79F1D9AD09297A5F94470991D266FC, rollback C:/Users/pboru/OpenFill/app__rollback__before_release_20261104_214424.
- Smoke 6/6, test Chromium karty Ask 12/12.
- Naturalna produkcja: OF-20261104-2144-116f, ten sam capture_id, reused_snapshot=true, cache_verification=exact_live_dom_text_sha256, use_best=true.
- Naturalna invalidation: OF-20261104-2145-c8b9 (Version A reuse) i OF-20261104-2146-2129 (Version B, nowy capture_id), lokalny serwer zatrzymany.
- Wniosek: A27 technicznie i odbiorowo DONE, nie ponawiać.

## D04, rzeczywiste testy interfejsu
- Audyt YouTube Studio OF-20261104-2146-42ae. Kanał UCWV43It6GQtfdahvAEA_gjg, Skakanka – Centrum Terapii Warszawa, odczyt URL scope bez niezależnego uwierzytelnienia.
- Rzeczywisty test OF-20261104-2152-c13d: przesłano 3-sekundowe pionowe wideo testowe jako szkic, tytuł OF QA TEST DO NOT PUBLISH. Rozpoznano tytuł (input#title-textarea), widoczność, kalendarz, godzinę, strefę czasową. Szkic usunięto i potwierdzono brak tego tytułu w odświeżonej liście Shorts. Nie wykonano publikacji ani harmonogramu.
- Audyt Meta OF-20261104-2147-715e, Facebook asset_id 1298713136667586, business_id 2177777413149708, połączony IG skakanka_centrum_terapii. URL scope bez niezależnego potwierdzenia konta.
- Rzeczywisty test Meta OF-20261104-2152-ce98: plik nie został dodany, ponieważ po kliknięciu Dodaj film otwiera się zablokowane natywne okno wyboru pliku, a DOM nie udostępnia pola input[type=file]. Test partial. Szkic testowy NIE powstał; istniejących opublikowanych/zaplanowanych pozycji nie zmieniono. Dalsza ścieżka planowania nadal niezweryfikowana.
- Techniczne procedury D04 nadal candidate. Brak implementacji i realnego odbioru adapterów publikacyjnych. Nie oznaczać jako DONE.
- Materiał testowy: C:/Users/pboru/OpenFill/shared/d04_acceptance_20261104/OF_QA_TEST_DO_NOT_PUBLISH_3s_vertical.mp4, SHA A8A7F29DD029A63182A50EF8E06F2543209C577A60D99844417F5DD15F2DC12B.

## E03, audyt i zmiana SOURCE bez potwierdzonego wydania
- Audyt utworzył docs/OPENFILL_EFFICIENCY_AUDIT_HANDOFF_20261104.md. Na ostatnich 100 kind=task: 40 success, 27 partial, 3 failed, 15 legacy cancel, 7 interrupted, 0 missing outcomes, 3757 model calls, koszt 1.511 USD, 48 kandydatów do przeglądu.
- Raport miał wynik REPORT_OK. Osobny wrapper omyłkowo zgłosił E03_AUDIT_FAILED przez sprawdzanie nieaktualnego $LASTEXITCODE po wywołaniu skryptu PowerShell. Plik raportu istnieje i zawiera kompletne liczby, więc audyt się wykonał.
- Konkretny problem z logów: WhatsApp OF-20261104-1602-5fa9, runId 20261104_150235_3a5297, 111 calls, 11 act_locator target_text_mismatch (każdy action_attempted=false), 18 pageReadRequests, 24 cdpReadCalls, outcome partial, koszt 0.038444 USD. Model zgadywał selektory i expected_text bez otrzymania faktycznego tekstu trafionego elementu.
- SOURCE: src/OpenFill.Core/Agent/ToolRegistry__v0.22__2026-10-29_2314.cs poprawiono tak, by act_locator zwracał ograniczone observed_text (max 180 znaków) i observed_count=1 przy target_text_mismatch, nadal bez wykonania akcji. Uzupełniono opis narzędzia. Backup: ten sam plik + .before_e03_act_locator_mismatch_20261104.
- Node test rzeczywistego embedded JS: E03_ACT_LOCATOR_EMBEDDED_JS_7_CHECKS_OK. Test dotyczy zachowania skryptu z mockowanym DOM, nie rzeczywistego WhatsApp.
- Test Playwright/Chromium zaczął się, lecz sesja PBOX1/LC MultiHost zawiesiła się i została odtworzona. Nie potwierdzono PASS tego testu. Oryginalna sesja LC utraciła łączność, potem Remote Desktop Commander pozwolił odczytać SOURCE i wykonać test Node.
- Uruchomiono próbę dotnet publish Release do staging/of_e03_act_locator_20261104, ale w chwili tego zapisu nie ma potwierdzonego zakończenia, artefaktu ani kodu wyjścia. NIE WOLNO twierdzić, że E03 jest wdrożone LIVE. Następny wykonawca powinien zweryfikować status publish i dopiero wykonać DEV/LIVE kanonicznym skryptem, testy i niezależny SHA.
- Nie wykazano jeszcze zmniejszenia realnych kosztów/wywołań po tej zmianie. E03 pozostaje otwarte odbiorowo.

## Pozostałe ograniczenia
- D01 i D02: kod candidate w LIVE, ale brak konkretnych zatwierdzonych wsadów odbiorców i treści do rzeczywistych prób. Brak evidence send w tej kontynuacji.
- E02: mechanizm fixture PASS, ale brak dwóch niezależnych, rzeczywiście zweryfikowanych wsadów, brak awansu produkcyjnego.
- LocalPlan: wąski single-filter działa, pojedyncze A/B wcześniej potwierdzone; próba E2E klasy dwupolowej przez bezpośrednie MCP w tej sesji została zablokowana przez OpenAI security, nie uruchomiono testu ani nie zmieniono routera.
- Stop GUI: rzeczywisty user_stop nadal bez naturalnego acceptance.
- Ask Enter: Chromium DOM 12/12 PASS, ale naturalna karta w WebView2 nadal bez odbioru.
- Airbnb D03 anulowane; wake projektu of celowo wyłączone. Bez resetowania repo, bez reaktywacji tych obszarów.

## Stan LIVE
- Ostatni niezależny odczyt w tej kontynuacji przed awarią kanału zdalnego: LIVE CORE SHA 713BBD7655410CF0518C9BA12DEB13B86C79F1D9AD09297A5F94470991D266FC, PID 19828.
- Odpowiedź OpenFill MCP openfill_info była dostępna podczas problemów z LC MultiHost. Nie przesądza to SHA z przyszłości.
