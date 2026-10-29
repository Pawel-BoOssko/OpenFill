# OpenFill — statusy zadań omówionych przy priorytetach
Data: 2026-10-29
Host: PBOX1
Zakres: OF-236 (16 punktów), OF-090, OF-231, OF-233, integracja Networking Radar (pełna A8 + OF-161), OF-238, OF-062.
Oceny oparte na kodzie SOURCE, testach, istniejących logach i danych LIVE; w tym audycie nie wykonywano rejestracji na zewnętrznych portalach ani nie modyfikowano produkcji.

Statusy (dokładnie cztery): **Do zrobienia** = nie rozpoczęto wskazanej pracy; **Rozpoczęte** = istnieje część implementacji, ale brak pełnego odbioru; **Zrobione** = wymagany zakres implementacji i test źródłowy potwierdzony; **Błąd** = wykryty konkretny błąd, niespełniony rezultat albo sprzeczny z obecną produkcją artefakt.
Uwaga: status Zrobione SOURCE nie jest automatycznie potwierdzeniem wielogodzinnego E2E LIVE. Instalacja OpenFill z 16:27, PID 10760, LIVE 1.57; po testach SOURCE hash OpenFill.Core.dll nie był identyczny z LIVE, więc nie przypisywać dowodu produkcyjnego wyłącznie na podstawie testów.

## Zwięzła lista

| Zadanie | Status | Zweryfikowany stan |
|---|---|---|
| OF-236, pełny pakiet 16 | Rozpoczęte | 15 pozycji w aktualnym kodzie/README; instrukcja testerska z pkt 16 nie występuje w SOURCE. Szczegóły poniżej |
| OF-090, trudne witryny, iframe, loginy | Błąd | README wprost potwierdza, że get_page nie widzi treści cross-origin iframe; screenshot/fallback istnieje, lecz nie zamknięto wskazanego audytu Trello |
| OF-231, limit 5 kart i kolejka | Zrobione | TabManager v0.7 ma Limit=5, bezpieczną ewikcję oraz while/Task.Delay gdy brak slotu. TabTests v0.9 sprawdzają czekanie szóstego zadania i brak szóstej fizycznej karty |
| OF-233, wiarygodny postęp serii | Rozpoczęte | McpTaskManager v0.21 ma confirmed/total/remaining/last_confirmed_at, ETA=null; McpTests sprawdzają 2/5 i persist. Brak odebranego realnego wieloetapowego wysyłania LinkedIn |
| OF-155, Radar -> OpenFill „Zapisz mnie” | Rozpoczęte | event_registration.py i openfill_mcp.py, idempotency_key, stany i testy; baza LIVE nie ma completed success |
| OF-156, realny odbiór rejestracji | Błąd | Tabela event_registrations: 9 rekordów, 6 done/partial i 3 unknown; 0 success. Brak potwierdzenia organizatora w wymaganym E2E |
| OF-157, ponowny klik i utrata odpowiedzi | Rozpoczęte | test_openfill_registration.py weryfikuje brak drugiego task_id po drugim kliknięciu; test_registration_retry.py unknown blokuje restart. Brak odebranego testu rzeczywistego zerwania odpowiedzi sieciowej |
| OF-158, weryfikacja read-only vs zapis | Rozpoczęte | event_verification.py oddzielony od event_registration.py, ale brak niezależnego E2E dwóch celów |
| OF-159, Calendar po potwierdzeniu | Rozpoczęte | Walidacja kalendarza, konta, URL oraz testy; w bazie wszystkie 5 pozycji Calendar status=not_attempted, 0 rzeczywiście zakończonych |
| OF-160, kompletność i rozdzielenie danych projektu | Rozpoczęte | Kod bierze profil, konto kalendarza, limit ceny, pola kontaktowe; brak pełnego audytu cross-project i zgodności danych na produkcji |
| OF-161, odmowa/oczekiwanie/limit/partial | Rozpoczęte | Stany ACTIVE/TERMINAL, rozróżnienie partial i obsługa retry są w źródłach; rzeczywiste przypadki nieodebrane |
| OF-162, sanitizowane screenshoty/playbooki | Do zrobienia | Wymagany raport z karty FV71I2uV nie został wykazany jako gotowy artefakt |
| OF-163, aktualny kontrakt techniczny | Błąd | Dokument openfill_radar_mcp_contract v0.4 opiera się na źródłach z 18.10 i LIVE 1.55; dzisiaj LIVE 1.57 |
| OF-164, transfer załącznika i potwierdzenia | Do zrobienia | MCP oferuje openfill_file_transfer, ale brak potwierdzenia scenariusza Radar upload/download wraz z hashem |
| OF-178, pełne Radar->organizator->Calendar | Błąd | 0 sukcesów w event_registrations i 5/5 Calendar not_attempted. Wymagane E2E nie osiąga sukcesu |
| OF-185, dokument funkcji Radaru | Błąd | Dostępny kontrakt v0.4 opisuje 1.55, nie aktualne 1.57; wymaga synchronizacji z rzeczywistym API/stanami |
| OF-238, nie wykonywać instrukcji ze stron | Rozpoczęte | AgentRunner v0.28 ma zasadę data-not-instructions, pomijanie i notkę, przekazanie na wyraźną prośbę. Nie wykazano pełnego testu profilowego z tekstem instrukcji AI |
| OF-062, Public Suffix List | Rozpoczęte | PublicSuffixResolver v0.1, 16507 wierszy PSL wraz z PRIVATE, IDN, wyjątki i wildcard; testy; nie znaleziono automatycznego aktualizowania snapshotu |

## OF-236 — wszystkie 16 zmian, osobne statusy

| Punkt | Status | Dowód |
|---:|---|---|
| 1 | Zrobione | AgentRunner v0.28: jeden punkt „Solve every problem yourself first ...” zamiast dawnych list wstrzymań |
| 2 | Zrobione | AgentRunner: „If you cannot tell which account to use, ask with ask_user.” |
| 3 | Zrobione | AgentRunner: confirm_irreversible tylko przed płatnością lub niezamówionym final submission |
| 4 | Zrobione | Instrukcje haseł tymczasowych pozostawione (sekcja Creating accounts and temporary passwords) |
| 5 | Zrobione | ToolRegistry v0.21: ask_user wyjaśnia missing information, model-first, human-second, grupowanie pytań i Choose file; stare zdania nie występują |
| 6 | Zrobione | ToolRegistry v0.21: opis confirm_irreversible zgodny z ustalonym tekstem |
| 7 | Zrobione | McpServer v0.24: usunięto zdanie If a CAPTCHA or interactive login requires human action |
| 8 | Zrobione | McpServer: AUTHENTICATION AND CREDENTIALS ma krótkie wymagane trzy zdania |
| 9 | Zrobione | McpServer: skrócony punkt (5) o uwierzytelnieniu i hasłach tymczasowych |
| 10 | Zrobione | McpServer: waiting_for_user nie zawiera starej listy captcha/verification |
| 11 | Zrobione | McpServer: usunięto „Do not poll indefinitely without progress” |
| 12 | Zrobione | McpServer: usunięto „Operational diagnostic information may contain sensitive metadata” |
| 13 | Zrobione | McpServer: opis cancel nie zawiera słowa „captcha,” w starej liście |
| 14 | Zrobione | README v0.18: w Limitations nie ma już osobnego punktu o CAPTCHA |
| 15 | Zrobione | README v0.18: akapit Task-oriented autonomy i tabela Irreversible steps mają nowy model zgód |
| 16 | Błąd | W aktualnym SOURCE brak tools/tester/INSTRUKCJA... i katalogu tools/tester. Nie można odebrać poprawki tekstu w nieobecnym artefakcie; trzeba odnaleźć właściwą paczkę testerską albo odtworzyć ją z poprawnym tekstem |

## Dane kontrolne i priorytety

- SOURCE: build z 29.10 oraz MCP-only regression 1159 PASS/0 FAIL (bez osobnych rzeczywistych prób rejestracji).
- LIVE: OpenFill 1.57, PID 10760, start 16:28.
- Radar LIVE: odczyt SQLite mode=ro tabel event_registrations i event_registration_calendar; odpowiednio 9 i 5 rekordów; 0 success, 5 not_attempted.
- Nawet przy obecności kodu nie podnosić statusu Radar E2E na „Zrobione”, dopóki organizator i właściwy Calendar nie są potwierdzone.
- Zalecana kolejność usuwania problemów: (1) OF-156/178 rzeczywisty przebieg, (2) OF-090 trudne UI, (3) OF-163/185 kontrakt, (4) OF-236 pkt16 tester, (5) OF-233 E2E, (6) OF-062 automatyczna aktualizacja PSL.
