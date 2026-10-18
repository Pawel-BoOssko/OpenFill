# OF-163 / OF-185: Kontrakt OpenFill - Networking Radar

Wersja: 0.2, 2026-10-18. Zakres: źródła OpenFill na PBOX1, **nie** aktualna produkcja 1.52.
Poprzedni audyt: `openfill_radar_mcp_contract__v0.1__2026-10-17_1424.md`.

## Wersja i źródła prawdy
- SOURCE: `McpServer__v0.23__2026-10-17_1130.cs`, `McpTaskManager__v0.17__2026-10-14_2235.cs`, `IRunHost__v0.10__2026-10-12_1810.cs`, `AppHost__v0.22__2026-10-14_2259.cs`, `ToolRegistry__v0.21__2026-10-17_1130.cs` i `TabManager__v0.6__2026-10-12_2200.cs` (nazwy plików są metadanymi historycznymi, zawartość była później modyfikowana).
- MCP: Streamable HTTP/JSON-RPC 2.0, POST na ścieżce zawierającej poufny sufiks; lokalny nasłuch 127.0.0.1, port konfigurowalny. Nie zapisywać sekretnego adresu w raporcie.
- Zdefiniowane narzędzia: `openfill_start_task`, `openfill_task_status`, `openfill_reply`, `openfill_file_transfer`, `openfill_screenshot`, `openfill_info`, `openfill_cancel_task`, `openfill_maintenance`.
- Wywołania startu wymagają `requester` i `chat`; łączna tożsamość jest kontekstem zadania.

## Start, idempotencja i status
- `openfill_start_task`: `task`, `requester`, `chat`, opcjonalnie `title`, `wait_seconds`, `continue_task_id`, `idempotency_key`.
- Dla `idempotency_key` obowiązuje dokładnie 1-128 znaków `[A-Za-z0-9][A-Za-z0-9._:-]{0,127}`. Klucza nie można łączyć z `continue_task_id`.
- Powtórzenie tej samej komendy z tym samym kluczem i tożsamością ma zwrócić istniejące `task_id`; inna treść z tym samym kluczem ma zwrócić konflikt.
- `openfill_task_status` zwraca status istniejącego `task_id`. `poll_wait_max_seconds=30` to limit długiego odpytywania, **nie ETA** ukończenia. `estimated_completion_seconds=null` oznacza brak prognozy.
- `steps` oznacza kroki agenta i NIE jest liczbą wysłanych zaproszeń lub dokonanych rejestracji.
- `needs_input` może być obsłużone przez `openfill_reply`; `waiting_for_user` dotyczy własnego okna aplikacji i nie jest odpowiednikiem `needs_input`.
- Po przerwaniu / restarcie odczytać status i rzeczywisty zewnętrzny rezultat przed użyciem `continue_task_id`. Nie powtarzać w ciemno operacji powodujących skutki poza aplikacją.

## Potwierdzony postęp zadań seryjnych (nowe SOURCE)
- `confirmed_progress` pojawia się, jeśli są dane o partii: `confirmed`, `total`, `remaining`, `last_confirmed_at`, `recipients`, `source`, `estimated_completion_seconds`.
- `source=service_page_text_verified`: w źródłach wykonawca porównuje nazwę odbiorcy i tekst potwierdzenia widoczny w jednym krótkim wierszu bieżącej strony; odrębne wiersze nie wystarczają.
- W obrębie zadania odbiorca jest deduplikowany bez rozróżniania wielkości liter. Potwierdzeni odbiorcy są utrwalani i mogą zostać przeniesieni przy wznowieniu tej samej tożsamości.
- Sama zgodność tekstu na stronie **nie jest kryptograficznym ani transakcyjnym dowodem potwierdzenia organizatora**. W Radarze rozróżnić: obserwację interfejsu, potwierdzenie z serwisu i rzeczywistą rejestrację wydarzenia.

## Informacja o karcie przeglądarki (nowe SOURCE)
- `browser_tab` może zawierać `tab_id`, `domain`, `state`, `open`, `reuse_ready`, `reason`, `last_url`, `requester`, `can_reopen_saved_url`.
- `open=true` oznacza, że karta fizycznie istnieje według menedżera; `can_reopen_saved_url=true` nie oznacza przetrwania niezapisanego formularza ani sesji interaktywnej.
- Automatyczny wybór poprzedniej zakończonej karty jest zależny od tożsamości czatu i rozpoznanej domeny. Nie gwarantuje ponownego użycia strony, jeśli domena nie została ustalona.
- Limit źródeł: pięć kart, trzy zadania równoległe w dotychczasowej konfiguracji. Fizyczne karty użytkownika też zajmują slot limitu, nie są usuwane przez automatyczny mechanizm zwalniania.
- Aktualne SOURCE może zwalniać fizyczną kartę po zakończeniu zadania (`releaseFinishedTabs`); zachowanie na kilka godzin / następny dzień w GUI nadal oczekuje na osobny odbiór OF-230/234/235.

## Zasady integracji Radar
1. Czytaj oficjalne dane wydarzenia osobnym zadaniem read-only (OF-158), nie oznaczaj tego jako RSVP.
2. Dla zamierzonej rejestracji użyj stałego `idempotency_key` powiązanego z konkretnym wydarzeniem, uczestnikiem i próbą. Przy utracie odpowiedzi odczytaj wcześniejsze `task_id`.
3. Wynik agentowy `done/success`, `confirmed_progress` lub wpis w Google Calendar nie zastępuje jednoznacznego potwierdzenia rejestracji od organizatora (OF-156/178).
4. Do Google Calendar dodawaj potwierdzoną rejestrację dopiero po jej weryfikacji (OF-159).
5. Transfer plików odbywa się przez mechanizm `openfill_file_transfer` i HTTP PUT/GET, bez umieszczania zawartości dużych plików w argumentach JSON. Sprawdź nazwę i hash załącznika.
6. Podczas aktualizacji `openfill_maintenance` w źródłach może blokować nowe zadania, ale zastępczy responder MCP na tym samym porcie (OF-239) **nie został jeszcze zweryfikowany**.

## Poziomy potwierdzenia
- SOURCE: odczytane i zmienione według checkpointów v0.2/v0.3 oraz bieżącej sesji.
- TEST: 780 PASS / 0 FAIL w pełnej regresji Release przed ostatnią edycją `UserCloseAsync`; wynik kolejnego przebiegu należy zapisać osobno.
- BUILD: aplikacja OpenFill.App Release kompilowała się poprzednio z 0 błędów i ostrzeżeniem MSB3277. Dzisiejszy build po ostatnich edycjach pozostaje do wykonania.
- STAGED / DEPLOYED / LIVE-VERIFIED: nowe źródła jeszcze nieobjęte akceptacją; produkcyjna aplikacja to nadal ostatnio potwierdzone 1.52.
- Otwarte bramki: rzeczywisty test rejestracji, dokładne dopasowanie API Radar, walidacja kalendarza i załączników, OF-223 restore, OF-239 maintenance responder oraz OF-235 produkcyjny smoke.
