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
