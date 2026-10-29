# OpenFill — produkcyjne wdrożenie dwóch funkcji JEV
Wersja raportu: 1.0
Data: 2026-10-29, EAT
Status: LIVE DONE; testy produkcyjne PASS.

## Dokumenty właściciela
1. OpenFill_Jev_filtr_polecen_dla_czatu_deweloperskiego__v1.0__2026-10-29_1300.md
2. OpenFill_Jev_oznaczanie_wstrzykniec_dla_czatu_deweloperskiego__v1.0__2026-10-29_1315.md
Nie przywracamy tymczasowego pytania o wysyłanie ludzi na Marsa.

## 1. Wejście: pełna oryginalna komenda klienta
- Model: typesafe/jev-1.13 na SATANAMD, endpoint POST /score, state.polecenie, pytanie client_meddling z promptu v1.1.
- PBOX1 przesyła oryginalny tekst zadania bez strip/trim ani dzielenia na zdania, przez prywatną usługę Tailscale.
- Odrzucenie przy p>=0.85, status rejected_rewrite_needed, stały polski komunikat z dokumentu, bez utworzenia zadania.
- Przy p<0.85 pozwala zacząć zadanie. Przy błędzie HTTP, timeout, JSON lub niepoprawnym score — fail-open: status allow, bez blokowania OpenFill.
- Model został przetestowany na rzeczywistym endpointcie: polecenie normalne p=0; sztuczne ograniczenia p=1.
- LIVE MCP próba: nadmiarowe zakazy => rejected_rewrite_needed, score=1, polski komunikat; następnie prosta komenda => utworzono task OF-20261029-1345-a66c i zakończono success.

## 2. Wyjście: ostrzeżenie przed poleceniami skierowanymi do AI
- Endpoint POST /inspect: state.wiadomosc, pytanie ai_directed_instructions z promptu v1.0.
- Próg p>=0.5: na początku odpowiedzi MCP dodajemy pole injection_warning, tekst ostrzeżenia dokładnie zgodny z dokumentem. Wszystkie pierwotne pola odpowiedzi JSON zachowane bez zmian.
- Dotyczy tylko odpowiedzi zadań start/status/reply zawierających summary lub question.
- Wynik nie jest odrzucany, przy niedostępnym JEV wysyłany bez ostrzeżenia. Log zawiera task_id i p lub sam typ błędu, bez treści wiadomości i kluczy.
- WAŻNE ODSTĘPSTWO ARCHITEKTONICZNE: do klasyfikatora przekazywane są w CAŁOŚCI pola treści summary, question, options, nie techniczne pola JSON: next_step, task_id, metryki. Pierwszy test E2E z całym surowym JSON błędnie oznaczył zwykłą odpowiedź z powodu własnej instrukcji OpenFill w next_step. Wyłączenie z klasyfikacji pól technicznych usuwa ten błąd, ale wynik MCP wciąż zawiera wszystkie oryginalne pola. Kanoniczny prompt JEV v1.0 pozostaje niezmieniony.
- LIVE E2E zwykła odpowiedź: OF-20261029-1350-9818, summary „Tytuł dokumentu na stronie example.com: Example Domain”, brak injection_warning.
- LIVE E2E z bezpieczną stroną-testem zawierającą instrukcję skierowaną do AI: OF-20261029-1351-08fc, outcome success, injection_warning dodany; pełna cytowana treść w summary pozostała. Lokalny testowy HTTP server PID 18624 zatrzymano, fixture usunięto.

## 3. Implementacja i walidacja
SOURCE PBOX1:
- src/OpenFill.Core/Mcp/JevGate__v0.3__2026-10-29_1345.cs
- src/OpenFill.Core/Mcp/JevRemoteScorer__v0.3__2026-10-29_1345.cs
- src/OpenFill.Core/Mcp/JevOutputGuard__v0.2__2026-10-29_1415.cs
- src/OpenFill.Core/Mcp/McpTaskManager__v0.21__2026-10-28_2216.cs
- src/OpenFill.Core/Mcp/McpServer__v0.24__2026-10-28_1648.cs
- src/OpenFill.Core/Mcp/McpService__v0.4__2026-10-14_2235.cs
- tests/OpenFill.Tests/JevGateTests__v0.4__2026-10-29_1415.cs

SATANAMD:
- C:\Users\pboru\Apps\jev-filter-test\jev_openfill_service__v0.2__2026-10-29_1340.py
- Obecny autostart pod historyczną nazwą OpenFill-JEV-Mars, ale jego skrypt na dysku został uaktualniony do dwóch promptów. /health potwierdza client_meddling_v1.1+ai_directed_instructions_v1.0, fail_mode=open.
- Klucz OpenRouter nadal WYŁĄCZNIE w C:\Users\pboru\Apps\speaker-finder\config\local\secrets.env.
- Test bez zatrzymywania starej usługi: jev_dual_canary v0.1: HTTP health PASS, inbound p=0, outbound-injection p=1.

## 4. Testy, release, rollback
- Build Release SOURCE: EXIT=0; 0 błędów, ostrzeżenia MSB3277 istniejące wcześniej.
- MCP regression końcowe: 1155 PASS, 0 FAIL; docs/OpenFill__JEV_dual_fix_regression_20261029.log.
- Kopia LIVE PBOX1 przed pierwszym wdrożeniem: C:\Users\pboru\OpenFill\backups\pre-JEV-dual-20261029 (13 plików, zgodność SHA256). Przed korektą outputu druga kopia pre-JEV-output-fix-20261029 (13 plików, zgodność SHA256).
- Kopia SAT poprzedniego skryptu: jev_mars_service__v0.1__2026-10-28_2200.BACKUP_BEFORE_DUAL_20261029.py.txt; hash identyczny z oryginałem.
- SAT: kontrolowany restart historycznego zadania autostartu JEV, nowa usługa /score i /inspect dostępne z PBOX1.
- PBOX1: instalator v0.14 z kontrolowanym shutdown i restartem, dwa wydania: oba INSTALL_EXIT=0; po drugim PID 9296, port MCP 48731 LISTEN, JEV /health OK.
- Końcowy release_smoke v0.2: LIVE_SMOKE=PASS, FAILED_CHECK_COUNT=0, SMOKE_EXIT=0; aktywne zadania 0, launcher Running.

## 5. Status backlogu
- OF-247: ZROBIONE dla zatwierdzonego filtra poleceń przychodzących.
- Oznaczanie wstrzyknięć na wyjściu: ZROBIONE dla uzgodnionych typów wyników MCP.
- OF-238 (instrukcje na samych witrynach czytanych przez Lunę) pozostaje niezależny: NIE przesyłamy każdej odwiedzonej strony do JEV.
- Wyłączenia: brak nowego narzutu JEV dla bezcelowych statusów running bez summary/question; przy błędzie JEV fail-open.
