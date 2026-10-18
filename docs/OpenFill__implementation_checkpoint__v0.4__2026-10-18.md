# OpenFill: checkpoint wykonania v0.4

Data: 2026-10-18, po 07:29 (Europe/Warsaw). Plan: `openfill_master_action_plan__v1.7__2026-10-17_1145.md`, wyłącznie sekcja A. Poprzedni checkpoint: v0.3.

## Dowody w tej sesji

- PBOX1 dostępny przez Remote Desktop Commander; źródła zmieniano jedynie `edit_block`, nową dokumentację `write_file`.
- `dotnet run --project tests/OpenFill.Tests/OpenFill.Tests__v0.3__2026-10-14_2259.csproj --configuration Release --no-restore`: **785 PASS, 0 FAIL, exit code 0**, proces Desktop Commander 27504. Wcześniejsze pełne przebiegi po kolejnych poprawkach: 777/0 (PID 20704), 780/0 (PID 27748).
- `dotnet build src/OpenFill.App/OpenFill.App__v0.3__2026-10-12_2034.csproj --configuration Release --no-restore --verbosity quiet`: **0 errors, 1 warning MSB3277, exit code 0**, proces Desktop Commander 23224. Ostrzeżenie WindowsBase 4.0/5.0 związane z Microsoft.Web.WebView2.Wpf.dll jest nieusunięte; nie blokuje aktualnego kryterium "0 błędów".
- Produkcyjny `openfill_info` odpowiedział 18.10 o 07:29:19: OpenFill **1.52**, uruchomiony od 17.10 23:51:07, aktywne zadania: 0 w chwili odczytu. W tej sesji nie restartowano produkcji.

## Zmiany SOURCE + TEST

1. **OF-231, manualne karty:** `TabManager.BeginAsync` i `MakeRoomAsync` liczą wszystkie fizycznie istniejące karty, również `State=user`, zamiast wyłączać ręczne karty z limitu. Nie zamykają kart użytkownika automatycznie. Gdy wszystkie pięć slotów jest zajętych, szóste zadanie czeka, a anulowanie nie tworzy karty.
2. **OF-231, nieaktualne wpisy:** tylko istniejąca fizycznie karta ukończonego zadania może być ofiarą zwolnienia miejsca; stary `TabId` po zamknięciu przeglądarki nie zwalnia limitu i nie przerywa kolejki. Brak karty możliwej do bezpiecznego zamknięcia nie uruchamia szóstej karty.
3. **OF-231, `UserCloseAsync`:** najpierw wykonuje operację zamknięcia przeglądarki, a dopiero po jej sukcesie usuwa przypisanie `TabId`. Jeżeli zamknięcie wyrzuci wyjątek, wpis karty użytkownika pozostaje do ponowienia.
4. Testy w `tests/OpenFill.Tests/TabTests__v0.6__2026-10-13_1035.cs`: sześć asercji manualnych kart, trzy asercje przy nieaktualnej karcie, pięć asercji nieudanego/zakończonego zamknięcia. Łącznie 14 nowych PASS względem 771 w v0.3.
5. **OF-163/185**: zaktualizowano wyłącznie dokument kontraktu SOURCE `docs/openfill_radar_mcp_contract__v0.2__2026-10-18.md`. Zawiera `confirmed_progress`, `browser_tab`, rozróżnienie ETA od long-poll i warunki faktycznego potwierdzenia rejestracji. Integracyjne E2E z Radarem nadal OPEN.

## Log niewykonanych komend

Kanoniczny plik: `docs/OpenFill__log_niewykonanych_komend__v0.1__2026-10-18.md`. Dopisano **OF-UNEXEC-016**: Desktop Commander `start_search` zwrócił zero dopasowań dla istniejących plików w docs. Był to nieudany odczyt, nie odmowa bezpieczeństwa. Treść poprzednich wpisów została zachowana.

## Pozostające bramki

- OF-223: pełny deszyfrowany, zweryfikowany hashami test odtworzenia rzeczywistych danych na SATANAMD NIE WYKONANY.
- OF-239: zastępczy MCP responder pod tym samym endpointem podczas aktualizacji NIE WYKONANY i poprzednia konkretna próba source replacement była safety-denied; nie ponawiano jej.
- OF-230/234/235: brak wielogodzinnego rzeczywistego testu zachowania sesji i pełnego GUI/live-parity; `releaseFinishedTabs` nie zmieniano.
- OF-176/224: brak stagingu nowego wydania, próby rollbacku, instalacji i produkcyjnego smoke; decyzja RELEASE = **NO-GO**.
- Autosync: mechanizm jest przewidziany do publikacji zmian, ale najnowszy HEAD zawierający checkpoint v0.4 i dzisiejsze źródła nie został jeszcze w tej sesji sprawdzony.

## Następne dozwolone kroki

Kontrola synchronizacji konkretnego pliku v0.4; sprawdzenie samej poprawki OF-231 w realnym GUI po przygotowaniu bezpiecznego stagingu, gdy warunki wydania są spełnione; niezależny audyt kontraktu Radar, diagnostyka MSB3277 bez destabilizacji produkcji. Nie powtarzać wcześniej zablokowanych operacji innym narzędziem.
