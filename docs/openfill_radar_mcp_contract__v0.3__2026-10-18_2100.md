# OpenFill / Networking Radar, kontrakt MCP v0.3

Data: 2026-10-18, 21:00 CEST. Poprzedni kontrakt: `openfill_radar_mcp_contract__v0.2__2026-10-18.md`. Wydanie produkcyjne sprawdzone przez `openfill_info`: **OpenFill 1.55**, data wersji 2026-10-18 14:22, build 14:25. Zmiany testów bieżącego cyklu pozostają SOURCE/TEST, nie są osobną instalacją.

## Kontrakt

- MCP: Streamable HTTP JSON-RPC 2.0, uwierzytelniona ścieżka serwera; adresu z poufnym sufiksem nie publikować. Dostępne: `openfill_start_task`, `openfill_task_status`, `openfill_reply`, `openfill_cancel_task`, `openfill_info`, `openfill_screenshot`, `openfill_file_transfer`, `openfill_maintenance`.
- `openfill_start_task` przyjmuje `task`, obowiązkowe `requester` i `chat`; opcjonalne `title`, `wait_seconds`, `continue_task_id`, `idempotency_key`. Klucz spełnia `^[A-Za-z0-9][A-Za-z0-9._:-]{0,127}$`. Nie łączyć `continue_task_id` z `idempotency_key`.
- Stały klucz rejestracji jest związany z jednym wydarzeniem i uczestnikiem. Ponowne zgłoszenie tej samej treści przez tego samego `requester · chat` zwraca **pierwotne `task_id`**; inne zadanie z tym samym kluczem daje `idempotency_conflict`. Inny requester/chat nie może przejąć cudzej deduplikacji.
- W wyniku `openfill_task_status` wartość `poll_wait_max_seconds=30` oznacza czas długiego odpytania, a nie ETA zakończenia. Brak szacunku = `estimated_completion_seconds=null`. `steps` to kroki modelu, nie liczba rejestracji.
- `status=running/needs_input/waiting_for_user` to trzy różne stany; `needs_input` obsługuje `openfill_reply`, `waiting_for_user` wymaga działania w interaktywnym oknie. Po utracie procesu zarejestrowany status w pamięci dyskowej jest odzyskiwany jako `interrupted`, `outcome=partial` oraz `continue_task_id`. Żadnej automatycznej ponownej rejestracji.
- Przy `confirmed_progress` liczyć wyłącznie widoczne, zweryfikowane potwierdzenia, nie same kliknięcia, kroki modelu ani tekst możliwy do pomylenia z potwierdzeniem organizatora. Dla odmowy i niepewnego rezultatu nie oznaczać wydarzenia jako zarejestrowanego.
- `browser_tab.can_reopen_saved_url` nie gwarantuje zachowania niewysłanego formularza, cookies czy interaktywnej sesji. Nie traktować wznowienia URL jako dowodu kontynuacji stanu strony.
- Radar może uruchomić osobno read-only weryfikację wydarzenia (OF-158), osobno intencjonalną rejestrację. Wpis Google Calendar dopiero po dowodzie od organizatora; nie jest dowodem rejestracji. Transfer plików przez `openfill_file_transfer` z weryfikacją SHA-256, nie przez osadzanie bajtów w JSON.

## Dowody i otwarte odbiory

- **LIVE:** odczyt produkcyjnego `openfill_info` 18.10: wersja 1.55, limit 3 równoległych zadań, maksymalnie 90 kroków, maintenance=false. Nie wykonywano prób rejestracji na zewnętrznym portalu ani zmian w kontach użytkownika.
- **SOURCE TEST:** `McpTaskManager__v0.18__2026-10-18_0834.cs` wraz z `McpTests__v0.14__2026-10-18_2045.cs`. Pełne testy Release: **821 PASS, 0 FAIL** (poprzednio 813). Osiem nowych asercji OF-219: odmowa zapisu odzyskiwanego zadania, brak ponowienia zewnętrznego działania, zachowanie ID i późniejsze prawidłowe odzyskanie po przywróceniu możliwości zapisu. Użyto wyłącznie sztucznych rekordów w unikalnym katalogu tymczasowym.
- **BUILD:** rozwiązanie Release 0 errors, znane 1 warning MSB3277.
- **Brak rzeczywistego E2E:** OF-156/157/159/164/178 nadal wymagają celowo wybranej rejestracji, dowodu organizatora, zgodności z aktualnym backendem Radaru, testu transferu i kalendarza. Nie deklarować tych rzeczy jako zrobionych tylko na podstawie FakeRunHost.
- **OF-239:** zastępczy responder MCP podczas aktualizacji jeszcze niezrealizowany. OF-230: wielogodzinne zachowanie sesji karty nadal wymaga sprawdzenia GUI.

Stan: kontrakt aktualny do SOURCE oraz odczytu LIVE 1.55; brak nowego wydania aplikacji.
