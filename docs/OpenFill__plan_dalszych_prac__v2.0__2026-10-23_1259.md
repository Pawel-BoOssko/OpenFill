# OpenFill: pełen plan dalszych prac v2.0

**Stan planu:** 2026-10-23, PBOX1.  
**Podstawa:** kanoniczny backlog docs/openfill_master_action_plan__v1.7__2026-10-17_1145.md (wyłącznie zatwierdzona część A), checkpoint wydania 1.56, checkpoint stagingu 1.57 oraz późniejsze zlecenia użytkownika dotyczące kart, cache i paska adresu.  
**Cel:** niezawodny agent przeglądarkowy realizujący samodzielnie wieloetapowe zadania, zachowujący strony płatności, prawdziwy postęp i ciągłość przez restarty, o mierzalnym koszcie.

## Stan wyjściowy, potwierdzony

| Warstwa | Stan |
|---|---|
| Produkcja | OpenFill 1.56; MCP odpowiadał 23.10 o 12:54; maintenance=false |
| Bieżąca praca | 1 zadanie running (n8n.cloud) przy odczycie; stan sprawdzić ponownie przed aktualizacją |
| Staging | OpenFill 1.57, opublikowany lokalnie w C:\Users\pboru\OpenFill\staging\1.57, **nie jest produkcją** |
| Testy stagingu | 1221 PASS, 0 FAIL; Release build 0 błędów, 1 znane ostrzeżenie MSB3277 |
| Karty | 1.56 pozostawia finished tabs fizycznie otwarte, ale limit pięciu nadal może wypierać zakończone sesje |
| Koszt modelu | 1.56 ma nową kompakcję historii; cel 90% cached tokens i <=0,05 USD/14 operacji nadal niezweryfikowany LIVE |
| Backup | Pełen isolated restore na SATANAMD: 8864 pliki, 0 niezgodności SHA-256 |
| Dane użytkownika | C:\Users\pboru\OpenFill\data\stable: nie nadpisywać ani nie usuwać w trakcie wydań |

## Bezwzględne reguły wykonania

1. **Bez automatyzacji ChatGPT**, godzinnych wznowień, zadań w tle ChatGPT i przypominajek. Bez ręcznych git commit/push i bez ręcznego wyzwalania Autosync. Istniejący Autosync pozostaje jedynym mechanizmem publikacji.
2. Pliki projektu: Remote Desktop Commander (read_file, read_multiple_files, start_search, edit_block, write_file, move_file). Kompilacje: start_process i read_process_output. LC MultiHost tylko do kontroli i operacji systemowych, a nie edycji plików OpenFill.
3. Odmowy narzędzi nie obchodzić innymi narzędziami ani przez dzielenie operacji. Każdą niewykonaną komendę odnotować w **logu niewykonanych komend** (narzędzie, operacja, wynik, przyczyna, dalszy krok), odróżniając błędy składni od odmów bezpieczeństwa. Kontynuować inne, niezależne zadania.
4. Nie zmieniać instrukcji Luny i tekstów MCP poza zadaniem, które wprost je obejmuje. OF-236 ma przypisanego wykonawcę Claude: w tym planie tylko odbiór i weryfikacja, bez samowolnego przepisania.
5. Każda zmieniona klasa: nowa nazwa z wersją i datą, Metadata w pierwszym wierszu, brak starego duplikatu. Po zmianie: test modułu, build Release, pełna regresja, raport SOURCE/TEST. Wydanie to osobny etap STAGED/DEPLOYED/LIVE.
6. **Nie restartować procesu, gdy karta czeka na ręczną płatność lub trwa zadanie o niepewnych skutkach.** Jeśli nie ma bezpiecznego okna na upgrade, wykonywać inne punkty. Przy wydaniu: kopia całego app, maintenance, normalne zamknięcie, instalator w sesji użytkownika, MCP smoke, rzeczywiste GUI i rollback. Folderu data/stable nie ruszać.
7. Pomysły z części B backlogu pozostają do zatwierdzenia. Żaden sukces nie może być deklarowany jedynie na podstawie mocka, liczby kroków agenta lub kliknięcia przycisku.

## Kamienie milowe i pakiety dostaw

| Milestone | Zakres | Dowód |
|---|---|---|
| M1 | Wdrożenie **1.57** z historią adresów | Wersja 1.57 w produkcji, działające GUI, fallback 1.56 |
| M2 | Karty zakupów, pięć kart, kolejka, powrót po godzinach | Checkout i formularz widoczne oraz odzyskane w tym samym procesie |
| M3 | MCP maintenance, zastępca podczas aktualizacji, restart i idempotencja | E2E upgrade/reconnect bez podwójnych działań |
| M4 | Wiarygodny postęp, cache, koszt i wynik częściowy | Potwierdzone liczniki i rzeczywiste rozliczenie API |
| M5 | Networking Radar: „Zapisz mnie” + potwierdzenie + kalendarz | Jeden pełny E2E z dowodem organizatora |
| M6 | Formularze, uwierzytelnianie, Technology Map, wydajność, dokumentacja | Testy LIVE GUI, wielogodzinny uptime, powtarzalny rollback |

Wersje po 1.57 będą ustalane podczas wydania, bez sztucznego rezerwowania numerów. Każdy numer poniżej to pojedynczy, konkretny krok. **Odbiór** stanowi mierzalny warunek zakończenia, nie deklarację wykonania.


## Etap 0: przygotowanie i źródło prawdy

**01. [OF-183] Odczytać exe produkcyjne, Directory.Build.props, staging, openfill_info, aktywne task_id, plan Autosync.**

- **Kryterium odbioru:** Tabela SOURCE/TEST/STAGED/DEPLOYED/LIVE z wersjami i godzinami.

**02. [OF-182, OF-183] Przejść backlog A1-A10 i dopasować statusy do wydania 1.56 oraz stagingu 1.57.**

- **Kryterium odbioru:** Każdy otwarty OF ma następny krok i konkretny dowód ukończenia.

**03. [OF-117, OF-187] Sprawdzić, czy testy i build zapisują wyłącznie do własnych temp/profili, a nie data/stable.**

- **Kryterium odbioru:** Raport ścieżek i wynik izolacji; nie powtarzać wcześniej odmówionego patcha.

**04. [OF-180] Ujednolicić szybkie testy modułowe, build Release, pełną regresję i raport po zmianie.**

- **Kryterium odbioru:** 0 FAIL albo wycofanie ostatniej zmiany; wynik każdej komendy zapisany.

**05. [OF-189] Przygotować szablon release report: lista plików, hash, testy, backup, rollback, smoke MCP i GUI.**

- **Kryterium odbioru:** Jeden wypełniony raport na każdą wersję.

**06. [OF-183] Prowadzić po każdym punkcie checkpoint i log niewykonanych komend; nie aktywować automatyzacji ani nie używać ręcznego push.**

- **Kryterium odbioru:** Historia postępów jest pełna, blokady i błędy mają osobne statusy.


## Etap 1: wersja 1.57 i pasek adresu

**07. [OF-242] Potwierdzić staging/1.57: wersję OpenFill.exe, liczbę plików, hash i zgodność ze źródłami.**

- **Kryterium odbioru:** Paczka kompletna, bez produkcyjnych sekretów/profilu.

**08. [OF-242, OF-180] Powtórzyć AddressHistoryTests (MRU, 200 pozycji, autouzupełnianie, sanitizacja URL, restart) i całą regresję.**

- **Kryterium odbioru:** Co najmniej 1221 PASS, 0 FAIL i build 0 błędów.

**09. [OF-230, OF-176] Przed restartem zbadać wszystkie aktywne karty, running/needs_input/waiting_for_user i ekrany płatności.**

- **Kryterium odbioru:** Nie utracono aktywnej sesji ani niewysłanego formularza.

**10. [OF-140, OF-210] Skopiować cały app 1.56 do app-1.56 (lub nazwy z timestampem), zweryfikować pliki i SHA-256 exe.**

- **Kryterium odbioru:** Pewna kopia do natychmiastowego cofnięcia; data/stable bez zmian.

**11. [OF-203-211, OF-140] Po bezpiecznym preflight: maintenance, normalny shutdown, install v0.13, start przez Harmonogram w SessionId 1.**

- **Kryterium odbioru:** Instalator exit 0, app 1.57 działa, endpoint MCP identyczny.

**12. [OF-242] GUI smoke: wpisać fragment domeny, wybrać sugestię strzałkami i myszą, Enter, kilka kart, restart i ponowne odczytanie historii.**

- **Kryterium odbioru:** Funkcja działa w realnym GUI, nie tylko w testach .NET.

**13. [OF-149, OF-176] MCP smoke example.com i właściwa wersja; wyłączyć maintenance; przy błędzie rollback app-1.56.**

- **Kryterium odbioru:** Udokumentowany LIVE PASS lub poprawny rollback.


## Etap 2: trwałe karty i dokańczanie zakupów

**14. [OF-225, OF-226] Zweryfikować tab_id/owner=requester/chat/domain przez kilka zadań i oddzielne czaty.**

- **Kryterium odbioru:** Ta sama para czat+domena ponownie używa fizycznej karty; inne czaty jej nie przejmują.

**15. [OF-229] Zdiagnozować utworzenie nowej karty mimo istnienia starej: niejawna domena, zajętość, redirect, utrata stanu.**

- **Kryterium odbioru:** Klient dostaje prawdziwy reason, brak fałszywego 'reused'.

**16. [OF-230] Test done/failed/stopped na otwartym formularzu i checkout: obserwować DOM, URL, cookies i fizyczną kartę.**

- **Kryterium odbioru:** Brak close/blank po zakończeniu; strona gotowa do ręcznego działania.

**17. [OF-231] Test limitu 5 kart i kolejki dla free/finished/manual/running/needs_input/waiting_for_user.**

- **Kryterium odbioru:** Szóste zadanie nie niszczy cudzej aktywnej karty.

**18. [OF-230, OF-231] Dodać ochronę zakończonej karty oczekującej na płatność lub użytkownika, także przy piątym/szóstym zadaniu.**

- **Kryterium odbioru:** Przy braku bezpiecznej ofiary nowe zadanie czeka, nie zamyka checkout.

**19. [OF-234] Przebieg 60 i 180 minut, kolejno 1/3/5 kart, wielokrotne powroty do domeny.**

- **Kryterium odbioru:** Raport tab_id, stanu strony, rendererów i czasu utrzymania.

**20. [OF-228] Ujednolicić raport końcowy: open, reuse_ready, domena, właściciel, tab_id, realny stan strony i zadania.**

- **Kryterium odbioru:** Komunikaty MCP i GUI pokrywają się z prawdziwym stanem WebView2.

**21. [OF-235] Odróżnić zachowanie żywej karty od reopen zapisanej lokalizacji po restarcie.**

- **Kryterium odbioru:** Żadne wznowienie URL nie jest błędnie przedstawiane jako zachowany niezapisany formularz.

**22. [OF-234, OF-235] Test zakupów na testowym portalu: koszyk -> przygotowana płatność -> done -> powrót po godzinie -> ręczne dokończenie.**

- **Kryterium odbioru:** Prawdziwe GUI PASS bez niezamówionej płatności.

**23. [OF-189] Wydać pakiet kart: kopia app, 0 FAIL, GUI smoke i rollback; aktualizować backlog.**

- **Kryterium odbioru:** M2 zaliczone na LIVE, a nie tylko w mocku.


## Etap 3: niezawodność zadań i zastępca MCP

**24. [OF-212] Test SaveStrict/interrupted podczas zamykania z wymuszonym błędem dysku i odmową zapisu.**

- **Kryterium odbioru:** Shutdown nie niszczy zadania, jeśli nie dało się go bezpiecznie utrwalić.

**25. [OF-218] Test osieroconych running/needs_input/waiting_for_user po restarcie procesu.**

- **Kryterium odbioru:** Odtwarzają się jako interrupted/partial, ten sam task_id i continue_task_id.

**26. [OF-219] Przetestować retry przy braku odczytu/zapisu historii i tym samym idempotency_key.**

- **Kryterium odbioru:** Nie wysyła się drugiej płatności, rejestracji ani wiadomości.

**27. [OF-203, OF-204] Ustandaryzować maintenance, blokadę start_task i wiarygodną informację o przewidywanej przerwie.**

- **Kryterium odbioru:** Nowe starty nie wykonują działań podczas maintenance.

**28. [OF-205-209] Test trzech aktywnych zadań, powiadomień w żywych long-poll, shutdown, restart, statusów po wznowieniu.**

- **Kryterium odbioru:** Spójny stan bez fałszywego obiecywania push rozłączonemu klientowi.

**29. [OF-239] Zaprojektować kontrakt respondera MCP na czas aktualizacji na podstawie istniejącego audytu watchdog.**

- **Kryterium odbioru:** Start zwraca maintenance; status po <=30 s maintenance i pozostałe minuty.

**30. [OF-239] Zbudować testowy responder na osobnym porcie/profilu, bez zajmowania produkcyjnego MCP.**

- **Kryterium odbioru:** Test protokołu PASS, produkcja nietknięta.

**31. [OF-239, OF-143] Przetestować przekazanie wspólnego portu i wyłączenie zastępcy po starcie nowego OpenFill.**

- **Kryterium odbioru:** Brak kolizji portu; nie ponawiać wcześniej odrzuconej implementacji alternatywnym narzędziem.

**32. [OF-105, OF-108] Test utraty sieci, tunelu i ponownego połączenia ChatGPT/Claude z aktualnym tools/list.**

- **Kryterium odbioru:** Po reconnect status task_id nadal dostępny i inni klienci działają.

**33. [OF-211, OF-149] Pełny test 3 stanów zadań, long-poll, upgrade i idempotencji.**

- **Kryterium odbioru:** Nie ma podwójnych skutków zewnętrznych, po restarcie prawdziwe interrupted.

**34. [OF-176, OF-189] Wydać pakiet maintenance/MCP z kopią app, smoke GUI i sprawdzonym rollbackiem.**

- **Kryterium odbioru:** M3 LIVE PASS, niezależnie od powodzenia testów jednostkowych.


## Etap 4: postęp, długie zadania i koszty

**35. [OF-232] Oddzielić 30 s long-poll od ETA, wyświetlać ETA nieznane, gdy brak danych.**

- **Kryterium odbioru:** Brak fikcyjnej prognozy 30 s.

**36. [OF-233] Wprowadzić confirmed_progress dla seryjnych czynności, liczonych po potwierdzeniu odbiorcy.**

- **Kryterium odbioru:** Przykład 2/5 oznacza dokładnie dwa potwierdzone działania.

**37. [OF-233, OF-161] Rozróżnić success/partial/failed, timestamp postępu i osobno skutki niepewne.**

- **Kryterium odbioru:** Czytelny stan w GUI i MCP, bez zamiany kliknięć na sukces.

**38. [OF-241] Zmierzyć inputTokens/cachedTokens/cacheFraction, checkpointy i firstChangedRequestByte na zadaniach read-only.**

- **Kryterium odbioru:** Raport per krok bez logowania pełnych promptów i sekretów.

**39. [OF-241] Wykonać lokalny test 80-150 tur po kilku kompaktowaniach.**

- **Kryterium odbioru:** Agent pamięta celu, nie osieroca narzędzi i nie dubluje wykonanych działań.

**40. [OF-241] Małe rzeczywiste zapytania modelowe read-only, ocena procentu cached tokens poza checkpointami.**

- **Kryterium odbioru:** Cel >=90% potwierdzony danymi API lub jawny FAIL.

**41. [OF-241] Porównać koszt równoważnego zadania 14 działań z historycznym przypadkiem bez wysyłania niezamówionych zaproszeń.**

- **Kryterium odbioru:** Cel <=0,05 USD potwierdzony rzeczywistym kosztem lub jawny FAIL.

**42. [OF-074, OF-128] Weryfikować zakończenie po stronie serwera/ekranie potwierdzenia, nie po submit click.**

- **Kryterium odbioru:** Brak fałszywego done, sensowne partial.

**43. [OF-180, OF-189] Dodać wyniki kosztu/progresu do regresji i wydać sprawdzony pakiet.**

- **Kryterium odbioru:** M4 LIVE PASS oraz przejrzyste raportowanie kosztu.


## Etap 5: Networking Radar i potwierdzone rejestracje

**44. [OF-155, OF-163, OF-185] Sprawdzić kontrakt MCP z realną wersją OpenFill i aktualnym Radar, tools/list oraz schema.**

- **Kryterium odbioru:** Zgodne argumenty, requester/chat, idempotency i statusy.

**45. [OF-160] Skontrolować mail, dane osoby, projekt, oficjalny URL, cenę, datę i pola wymagane przez wydarzenie.**

- **Kryterium odbioru:** Brak brakujących danych podstawowych i błędnej tożsamości.

**46. [OF-158] Rozdzielić technicznie zadanie odczytu informacji od rejestracji.**

- **Kryterium odbioru:** Jeden read-only task nie jest traktowany jako zgłoszenie.

**47. [OF-157, OF-219] Test dwukliku, retry po timeout i restartu z idempotency_key.**

- **Kryterium odbioru:** Jedno zgłoszenie i oryginalne task_id.

**48. [OF-161] Obsłużyć odmowę, częściową rejestrację, oczekiwanie na e-mail i wynik niepewny.**

- **Kryterium odbioru:** Outcome i dowód jednoznaczne, brak fikcyjnej rejestracji.

**49. [OF-156, OF-178] Wykonać jedną rzeczywistą intencjonalną rejestrację na konkretnym wydarzeniu.**

- **Kryterium odbioru:** Potwierdzenie organizatora i poprawne dane uczestnika.

**50. [OF-159] Po potwierdzeniu dodać/uzgodnić wydarzenie w Google Calendar.**

- **Kryterium odbioru:** Jeden poprawny wpis; zero wpisów za niepotwierdzone rejestracje.

**51. [OF-164, OF-162] Przetestować upload/download, SHA-256 i sanitizowane playbooki błędów formularzy.**

- **Kryterium odbioru:** Przesłany plik ma poprawną integralność i nazwę, bez payloadu pliku w JSON.

**52. [OF-178, OF-189] Pełne E2E Radar -> OpenFill -> organizator -> Calendar -> raport.**

- **Kryterium odbioru:** M5 PASS z dowodem, a nie tylko testem FakeRunHost.


## Etap 6: formularze, loginy i bezpieczeństwo treści

**53. [OF-054, OF-057] Test właściwego konta CyberFolks i kilku loginów/tenantów, także dwuetapowych formularzy.**

- **Kryterium odbioru:** Brak pomylenia originów, serwerowa odmowa odróżniona od braku poświadczeń.

**54. [OF-090] Zebrać błędy DOM/screenshot/CDP przy iframe, modal, ukrytym loginie i nietypowych kontrolkach.**

- **Kryterium odbioru:** Raport reprodukcji i lokalny test poprawki.

**55. [OF-238] Test treści strony i notatek udających polecenia AI.**

- **Kryterium odbioru:** Prompt-injection traktowany jak dane; zwykła treść nie jest tracona.

**56. [OF-237] Odświeżyć paczkę testerską i instrukcję względem aktualnego kodu.**

- **Kryterium odbioru:** Kompletne ZIP/README bez prywatnych sekretów, smoke PASS.

**57. [OF-236] Odebrać uzgodnione z Claude'em zmiany tekstów Luny i MCP zgodnie z 16 punktami specyfikacji.**

- **Kryterium odbioru:** Raport zgodności; bez samodzielnego przepisywania instrukcji.


## Etap 7: interfejs, panel technologii i wydajność

**58. [OF-001-007, OF-030] Rzeczywisty GUI test Technology Map w SessionId 1 i DPI 100/125/150%, także w małym oknie.**

- **Kryterium odbioru:** Widoczny panel, brak zasłoniętych kart i paska adresu; screenshoty.

**59. [OF-010-029, OF-168] Sprawdzić manifest 41 tematów/7 kategorii, wyszukiwarkę, expand, copy.**

- **Kryterium odbioru:** Zgodność z kodem i 0 sekretów w panelu.

**60. [OF-184, OF-188, OF-201] Uaktualnić mapę architektury i diagramy przepływu ChatGPT/MCP/WebView2, Firefox/WinCred, Radar/Calendar.**

- **Kryterium odbioru:** Spójny dokument techniczny, realne wersje i zastosowania.

**61. [OF-031-034] Audyt zbierania PID, CPU, WS, Private Bytes, pamięci .NET, WebView2 dzieci i stanu zadań.**

- **Kryterium odbioru:** Brak mylenia procesów i metryk, jawne luki próbek.

**62. [OF-032, OF-038, OF-039] Pomiary przy 1/3/5 kart i 0/1/3 zadaniach, idle 30/60 min oraz strona lekka/ciężka.**

- **Kryterium odbioru:** Porównywalne dane kilku wersji, z jednostkami.

**63. [OF-048, OF-177] Test stabilności 3-6 h oraz powrót po godzinie bezczynności.**

- **Kryterium odbioru:** Brak nieoczekiwanych restartów i utraty formularzy; raport RAM/CPU.

**64. [OF-046] Wprowadzić optymalizacje pamięci tylko po weryfikacji wpływu na żywe sesje płatności.**

- **Kryterium odbioru:** Pomiar A/B i brak regresji kart.

**65. [OF-120, OF-126] Uczytelnić panel zleceń: task_id, requester/chat, domena, potwierdzony postęp, koszt i outcome.**

- **Kryterium odbioru:** GUI i MCP zgodne, bez danych innych kont.


## Etap 8: finalizacja procesu wydawania i dokumentacja

**66. [OF-140, OF-143] Jedna ścieżka wydania: build -> staging -> backup -> maintenance -> SessionId 1 -> health.**

- **Kryterium odbioru:** Automatyczna bramka logiczna, żadna aplikacja GUI nie startuje w sesji 0.

**67. [OF-149, OF-176] Praktyczny rollback na izolowanym profilu i kontroli starego exe.**

- **Kryterium odbioru:** MCP potwierdza cofniętą wersję, data/stable nienaruszony.

**68. [OF-223] Utrzymać zaszyfrowany backup/restore; odnotować wynik 8864 pliki / 0 niezgodności SHA.**

- **Kryterium odbioru:** Udokumentowana i sprawdzona procedura bez upubliczniania klucza.

**69. [OF-187] Dokończyć audyt repo, plików bak/log, sekretów i binarek poza bin/obj.**

- **Kryterium odbioru:** Bezpieczny spis artefaktów, bez kasowania historii Git.

**70. [OF-182, OF-183] Odświeżyć README/INDEX/handoff do realnej produkcji, stagingu i testów.**

- **Kryterium odbioru:** Jedno aktualne źródło stanu, stare raporty historyczne.

**71. [OF-189] Do każdego wydania dołączać changelog, listę plików, hash, testy, rollback i live GUI/MCP.**

- **Kryterium odbioru:** Dowody kompletne, nie mylić SOURCE z LIVE.

**72. [OF-117, OF-180] Pełna regresja na osobnym profilu z 3 klientami i 5 kartami.**

- **Kryterium odbioru:** 0 FAIL i 0 zapisów testowych do produkcyjnych danych.

**73. [OF-240] Jednorazowo ukryć migające okno PowerShell zadania synchronizacji, bez wyłączania synchronizacji; wymaga uprawnień administratora.**

- **Kryterium odbioru:** Brak wyskakującego okna, nadal istnieją logi; nie obchodzić wcześniejszej odmowy.

**74. [OF-149, OF-235] Odbiór pełnego cyklu zakup/formularz, powrót po godzinie, manualna płatność, zadania i retry.**

- **Kryterium odbioru:** Realny test GUI/MCP z potwierdzonym wynikiem i bez podwójnych działań.

**75. [OF-183, OF-189] Zamknąć wszystkie pozycje A1-A10 dowodami lub pozostawić OPEN ze znanym blokerem.**

- **Kryterium odbioru:** Końcowa tabela OF/status/wersja/test/pozostały krok, bez udawanych sukcesów.


## Realna kolejność wykonywania, bez biernego oczekiwania

- Najpierw **01-08**; 09-13 wykonać, gdy zamknięcie 1.56 nie zniszczy aktywnej sesji zakupowej.
- Niezależnie od okna instalacji realizować **24-26, 35-40, 44-46 i 58-60** na danych testowych.
- Po 1.57 priorytet użytkowy: **14-23**, bo zabezpiecza pozostawione strony płatności przy limicie pięciu kart.
- Dalej: **24-34** (MCP i restart), **35-43** (postęp i koszt), **44-52** (Radar), **53-65** (strony, GUI, wydajność).
- **66-75** są kryteriami jakości stosowanymi także po każdym wcześniejszym wydaniu, nie dopiero w ostatnim dniu.
- Nie dopisywać niezatwierdzonych pomysłów z części B do prac wykonawczych. Jeżeli pojawi się nowa propozycja architektoniczna, zapisać ją w B jako propozycję do decyzji, nie wdrażać samowolnie.

## Dowód ukończenia każdego zadania

W lokalnym checkpointie po każdym punkcie zapisać: numer, identyfikator OF, zmienione pliki i nowe wersje, wynik testu modułowego i pełnej regresji, stan SOURCE/BUILD/TEST/STAGED/LIVE, wersję produkcji, ograniczenia dowodu, ewentualną odmowę z logu niewykonanych komend, następny numer. Po wydaniu: PID/SessionId, MCP health, realny GUI smoke, backup app i hash rollbacku.

## Dokumenty bazowe

- docs/openfill_master_action_plan__v1.7__2026-10-17_1145.md
- docs/OpenFill__deployment_1.56__2026-10-23_1135.md
- docs/OpenFill__address_history_checkpoint__v0.1__2026-10-23_1158.md
- docs/OpenFill__issue_cache_cost_resolution__v0.1__2026-10-23_1110.md
- docs/openfill_autonomous_log__v0.1__2026-10-18.md
- docs/OpenFill__log_niewykonanych_komend__v0.1__2026-10-18.md

**Status pliku: PLAN, nie raport wykonania.** Prace już wykonane pozostają oznaczone jako takie tylko w źródłowych checkpointach; każdy kolejny krok wymaga świeżego potwierdzenia. Zadania użytkownika mają pierwszeństwo przed pomysłami niewprowadzonymi do zatwierdzonej części A.
