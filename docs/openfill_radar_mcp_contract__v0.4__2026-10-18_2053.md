# OpenFill i Networking Radar, kontrakt MCP v0.4

Data: 2026-10-18, 20:53 CEST. Podstawa: kontrakty v0.2 i v0.3, kod `McpTaskManager__v0.19__2026-10-18_2049.cs`, testy `McpTests__v0.15__2026-10-18_2050.cs`. Produkcyjne `openfill_info` ostatnio potwierdziło OpenFill **1.55** z 18 października o 14:22. Te nowe zmiany są tylko SOURCE/BUILD/TEST.

## Kontrakt dla konsumentów

- MCP Streamable HTTP / JSON-RPC. Nie udostępniać prywatnego sufiksu trasy. Narzędzia: `openfill_start_task`, `openfill_task_status`, `openfill_reply`, `openfill_cancel_task`, `openfill_file_transfer`, `openfill_screenshot`, `openfill_info`, `openfill_maintenance`.
- Start: `task`, obowiązkowe `requester` i `chat`, opcjonalnie `idempotency_key` i `continue_task_id` (nie jednocześnie), `title`, `wait_seconds`. Klucz: od 1 do 128 znaków, pierwsza litera/cyfra, dalej litery/cyfry/kropka/dwukropek/myślnik/podkreślnik. Deduplikacja jest związana z połączonym `requester · chat`.
- Po tym samym kluczu i tej samej treści należy zwrócić oryginalne `task_id`. Inna treść przy tym samym kluczu oznacza `idempotency_conflict` i brak startu.
- W źródłach v0.19 **IO przy czytaniu historii jest stanem niepewnym i blokuje nową próbę**; błąd trafia do klienta. Nieczytelny plik nie jest dowodem, że nie ma wcześniejszego zlecenia. Po przywróceniu odczytu istniejący rekord `running` po restarcie przechodzi w `interrupted/partial`, ten sam task_id, bez ponawiania działań zewnętrznych.
- Uszkodzony syntaktycznie, niezależny JSON może być pominięty w istniejącej logice; nie oznacza to transakcyjnej gwarancji deduplikacji przy utracie pliku. Zachować backup i diagnostykę historycznych zapisów.
- `poll_wait_max_seconds=30` określa maksymalny long-poll, **nie ETA**. `estimated_completion_seconds=null` oznacza brak wiarygodnej prognozy. `steps` nie są liczbą dokonanych rejestracji. Zweryfikowany postęp jest odrębny i nie zastępuje dowodu organizatora.
- Przy `status=needs_input` dopuszczalne `openfill_reply`; `waiting_for_user` wymaga działania w oknie użytkownika. Po aktualizacji `interrupted` wymaga sprawdzenia skutków zewnętrznych przed użyciem `continue_task_id`.
- `browser_tab.can_reopen_saved_url` nie dowodzi zachowania niezapisanego formularza ani sesji. Czytanie faktów wydarzenia (OF-158) oddzielić od rejestracji. Kalendarz dopiero po potwierdzeniu organizatora; załączniki przez autoryzowany transfer z hashem, nie osadzone w JSON.

## Dowody i braki

- SOURCE/BUILD: McpTaskManager v0.19 i McpTests v0.15; build rozwiązania Release bez błędów, jedno znane ostrzeżenie MSB3277.
- SOURCE/TEST: v0.14: **3 × 821 PASS/0 FAIL**; po poprawce v0.19 i testach v0.15: **3 × 829 PASS/0 FAIL**. Dodano łącznie 16 asercji OF-219, w tym brak duplikatu przy niemożności zapisu lub odczytu wcześniejszego zadania.
- LIVE: odczytano `openfill_info` z OpenFill 1.55 (limit 3 równoległych zadań, maintenance=false). Nie przeprowadzano rzeczywistego E2E Radar->organizer->Calendar, nie wysyłano zgłoszeń.
- Otwarta pozostaje walidacja rzeczywistych rejestracji (OF-156/178), ponowne wywołania portalu, prawidłowa tożsamość, formularz, dowód organizatora i kalendarz, OF-230 wielogodzinna sesja karty, OF-239 zastępca MCP w czasie upgrade.

Wersja v0.4 jest dokumentem kontraktu integracyjnego, nie oznacza wydania nowej aplikacji.
