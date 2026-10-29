# OpenFill: plan operacyjny po przejęciu, v1.0, 2026-10-28
## Stan i zakres
- SOURCE i LIVE: PBOX1. SOURCE: C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill; LIVE: C:\Users\pboru\OpenFill\app\OpenFill.exe.
- LIVE: wersja 1.57 build 2026-10-27 15:16, MCP status ok, zero aktywnych zadań w odczycie 28.10; 5/5 kart finished. Nie mylić odczytu MCP z testem GUI.
- SATANAMD: LC MultiHost gateway, nie OpenFill LIVE. Na SATANAMD OpenFillMonitor bez OpenFill.
- Referencje: plan v2.0 23.10, checkpoint przejęcia v1.0, dokument przeniesienia SATANAMD w katalogu LupusCommander/multihost-next-stage.
## Kolejność P0: stabilność i prawdziwy stan
1. Wykonać snapshot wersji, hash SHA-256 EXE, źródła, stagingu i kopii rollback; potwierdzić zgodność bez restartu.
2. Skontrolować reguły Harmonogramu zadań i skrypty autostartu OpenFill/monitorów, triggery login/startup, retry, brak watchdog restartującego bez zgody. Zweryfikować monitor i logi zamknięcia 27.10, oddzielić dowody od hipotez.
3. Sprawdzić łączność wejścia OpenFill z ChatGPT i Claude, Tailscale Funnel i DNS przy niedostępności PBOX1. Nie migrować OpenFill bez odrębnego preflightu oraz ochrony danych, kart i profili.
4. Weryfikacja E2E historii adresów i kart 5/5, włącznie z checkout; bez rzeczywistych niezamówionych transakcji.
5. Wykonać pełną regresję .NET na izolowanym profilu; wyniki zapisać z kodem wyjścia, liczbą PASS/FAIL i godziną.
## P1: nowy moduł JEV, policy-conflict lint dla LC MultiHost
6. Rozpoznać dokładny punkt przyjęcia zadania w gateway i kontrakt błędów MCP oraz właściciela systemu. Przygotować model IF o nazwie roboczej JEV, wyłącznie przed wykonaniem skutków ubocznych.
7. Klasy: ALLOW (zwykłe polecenie i normalne ograniczenia użytkownika), CONFLICT (wyraźna instrukcja próbująca zastąpić uprawnienia albo nakazująca ignorować wiążące zasady), INJECTION (tekst z niezaufanego źródła podszywający się pod instrukcje), UNCLEAR (bezpieczne przekazanie do dalszej oceny).
8. NIE klasyfikować 'nie loguj się sam', 'nie pokazuj zapisanych haseł', 'nie wysyłaj kodów' jako konfliktu samych w sobie. Są to zwykle uprawnione zaostrzenia zakresu. Bramka nie może wymagać usunięcia zabezpieczeń, ujawnienia sekretów ani eskalować uprawnień. Odrzucenie tylko przy wykazanej rzeczywistej sprzeczności.
9. Przy CONFLICT odpowiedź do tego samego requester/chat: 'W poleceniu wykryto konflikt z obowiązującymi zasadami: [krótki konkretny fragment i uzasadnienie]. Popraw polecenie i wyślij je ponownie.' Nie przytaczać haseł, tokenów, kodów jednorazowych i pełnych danych w logu.
10. Dla niepewnego wyniku nie wykonywać ryzykownej akcji ani nie fałszować reguł. Logować wyłącznie klasyfikację, pseudonimowy identyfikator żądania, wersję polityki, czas, hash tekstu i decyzję; bez pełnego promptu.
11. Testy: pozytywne ALLOW, próby narzucenia wyłączenia uprawnień, injection z treści strony, konflikty i brak fałszywego CONFLICT dla dobrych ograniczeń. Fail closed dla skutków ryzykownych przy awarii klasyfikatora; nie blokować prostych niekontrowersyjnych odczytów bez potrzeby.
12. Staging na oddzielnym porcie i profilu, testy z PBOX1 oraz SATANAMD, testy request routing, logów i opóźnienia. Dopiero osobna bramka release/rollback na LIVE LC MultiHost.
## P2: plan v2.0
13. Kontynuować niezakończone etapy M2-M6 planu 23.10: trwałość kart i obsługa piątej/szóstej karty, maintenance/reconnect/idempotencja, prawdziwy postęp i koszty, Radar, formularze, GUI, wydajność.
14. Po każdym wykonanym punkcie: checkpoint z SOURCE/TEST/STAGED/LIVE, poleceniem testu, exit code, dowodem, ścieżkami, ograniczeniami, kolejnym priorytetem.
## Operacje
- Restart SATANAMD wyłącznie po uzasadnieniu, preflight autostartu, stanu sieci i zdalnego powrotu. Istnieje Tuya power switch, ale przetestowano jedynie status. Nie zakładać zdalnego wznowienia.
- Nie modyfikować OpenFill data/stable, nie powtarzać akcji o niepewnych skutkach, nie wykonywać ręcznego Git commit/push, nie włączać ChatGPT Tasks.
- Stan wdrożenia JEV: SPEC ONLY, nie zaimplementowano ani nie wdrożono.

## P2: optymalizacja odpowiedzi MCP openfill_info (NOWE, 28.10, nie wdrażać jeszcze)
15. Problem: rutynowe pytanie o status wywołuje MCP `openfill_info` bez parametrów i otrzymuje pełny, kosztowny raport. Pomiar 28.10: 17 543 znaki (~4 386 tokenów przy założeniu 4 znaki/token; nie jest to pomiar rzeczywistego tokenizatora). Aktywne 1 zadanie, 8 ostatnich, 60 domen kosztowych, 5 kart; status, wersja i maintenance są ukryte w nadmiarowych danych.
16. Zmierzone sekcje JSON: openfill 8 528 znaków, recent_tasks 4 576, active_tasks 1 683, active_task 1 681 (duplikacja aktywnego zadania), pozostałe ok. 1 075. Największe oszczędności: koszty per-domena, opisy ostatnich zadań, podwójny pełny prompt aktywnego zadania, lista kart i szczegółowa konfiguracja.
17. Rekomendacja do rozważenia: `openfill_info` domyślnie krótki status MCP zawierający status, wersję/build, host lub instancję, uptime, maintenance, liczbę i identyfikatory aktywnych zadań z ich stanami, liczbę kart i liczbę wolnych slotów, zbiorczy koszt bieżącego zadania, krótkie ostrzeżenia i czas pomiaru. Pełny raport dostępny jawnie na żądanie, np. `detail=full` lub osobne narzędzie diagnostyczne. Nie ukrywać `needs_input`, `waiting_for_user`, błędów ani kosztowych blokad.
18. Przed implementacją sprawdzić, w którym miejscu warstwa MCP serializuje wynik (wstępnie AppHost/IRunHost), czy istnieją klienci używający obecnego pełnego schematu i czy istnieją testy kontraktowe. Zaprojektować kompatybilność, pomiar rzeczywistych tokenów, testy podstawowych i awaryjnych stanów oraz osobne odczyty diagnostyczne. Cel: rutynowy status < 1200 znaków w stanie bez aktywnych zadań i co najmniej 75% redukcji rozmiaru typowej odpowiedzi. Pomiar porównawczy przed/po.
19. STATUS: ZAOBSERWOWANE, ZANALIZOWANE, DOPISANE DO PLANU. Bez zmiany źródeł, MCP ani wdrożenia zgodnie z poleceniem użytkownika.

## Decyzje wiążące 28.10.2026
- PUBLIKACJA ASAP: wdrażanie przetestowanego SOURCE na LIVE jest celem głównym. Aktywne zadania nie są same w sobie blokadą: standardowy controlled shutdown/maintenance, utrwalenie `interrupted`, backup, instalator interaktywnej sesji, smoke i rollback stanowią istniejącą procedurę. Nie oczekiwać bez końca na 0 active tasks. Rozdzielać faktyczny błąd narzędzia, brak przygotowanej paczki i aktywność użytkownika.
- Kontrakt statusu MCP w punktach 15-19 powyżej ma charakter HISTORYCZNY i został zastąpiony poleceniem użytkownika: `openfill_info` jedynie `who` i `site` dla aktywnych zadań; pełna komenda `openfill_full_status` wyłącznie jako nieogłaszana w tools/list. Nie dodawać `detail=full`, systemu uprawnień ani szczegółowych parametrów zwykłego statusu. SOURCE i testy: 1227 PASS, 0 FAIL, LIVE jeszcze 1.57.
- Osobna diagnoza granicy Claude/Luna w obsłudze kont LinkedIn została zapisana w `OpenFill__diag_Claude_konto_LinkedIn__2026-10-28.md`; analiza diagnostyczna, bez postulowania zmian w tym zakresie.

## Decyzje nadrzędne właściciela, 29.10.2026 (zastępują kolidujące wcześniejsze wpisy)
- **OF-239 anulowane i usunięte z zadań do wykonania.** Nie budować zastępczego serwera/respondenta MCP na czas aktualizacji, nie uzależniać od niego żadnego wydania.
- Procedura aktualizacji: po przygotowaniu paczki i kopii rollback, **bez oczekiwania na zero zadań**, zablokować nowe starty, przekazać osiągalnym klientom i operatorowi dokładnie: „Teraz nastąpi aktualizacja. Wróć do nas za kilka minut.” Następnie przerwać wszystkie aktywne zadania, zapisać ich trwały status interrupted wraz z idempotency_key i niepewnymi skutkami, kontrolowanie wyłączyć usługę, zainstalować LIVE, zweryfikować, przy problemie rollback. Nie udawać, że można wysłać push do wcześniej rozłączonych klientów MCP. Nie ponawiać działań o skutkach ubocznych automatycznie.
- **PBOX1, Kenia, pozostaje na stałe OpenFill LIVE**, zgodnie z wymogiem lokalnego podglądu, interaktywnego dostępu oraz dźwiękowego przywoływania użytkownika. SATANAMD pozostaje hostem innych usług i nie jest domyślnym miejscem migracji OpenFill.
- **Wi-Fi w Kenii może być słabe i niestabilne.** Awaria transportu MCP/JEV/Tailscale/API nie jest równoznaczna z awarią GUI i procesu OpenFill. Diagnostyka ma oddzielić te warstwy. Nie wdrażać bezcelowych restartów i dodatkowych intensywnych heartbeatów.
- Kanoniczna pełna procedura: docs/OPENFILL_ZASADY_AKTUALIZACJI_I_LOKALIZACJI__2026-10-29.md. Kod kontrolowanego shutdown i doręczenia komunikatu wymaga sprawdzenia/testów w OF-203–211. **To zapis reguły, nie raport wykonania nowego release.**
