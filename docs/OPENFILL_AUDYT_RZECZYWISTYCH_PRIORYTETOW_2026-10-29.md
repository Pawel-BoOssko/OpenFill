# OpenFill: audyt rzeczywistych priorytetów
Data: 2026-10-29
Źródła: kanoniczny backlog, działający LIVE na PBOX1, kod ControlledUpgradeDialog / MainForm / McpTaskManager / instalator, obserwacje użytkownika.
Cel: realizować tylko faktyczne usterki, decyzje właściciela i brakujące funkcje. Historyczny wpis [DO ZROBIENIA] nie dowodzi sam w sobie usterki.

## A. Bieżące prace
1. **P0: zgodność wdrożeń z zasadą z 29.10.2026.** Stan przed zmianą: instalator już wywoływał CloseMainWindow przy aktywnych zadaniach, a McpTaskManager.PrepareShutdown oznaczał je interrupted, zapisywał trwale i powiadamiał aktywnie oczekujących. Rzeczywisty brak: komunikat po angielsku, udawany ETA 7/12 minut w testach, dialog operatora pytał o przewidywany czas i instalacyjna ścieżka nie pokazywała na wierzchu prostego komunikatu. Zmiana SOURCE: jeden tekst „Teraz nastąpi aktualizacja. Wróć do nas za kilka minut.” w managerze, dialogu i widocznym banerze MainForm. Testy i publikacja LIVE: w trakcie. OF-239 zastępczy MCP pozostaje ANULOWANE.
2. **P1: OF-247, docelowa polityka JEV.** JEV technicznie działa w LIVE na PBOX1->SATANAMD, filtr Mars jest wariantem tymczasowym. Do wykonania dopiero konkretne kryteria/prompt rzeczywistej oceny poleceń, gdy użytkownik je określi; nie rozszerzać samodzielnie zakresu i nie zmieniać progu >85%.

## B. Sprawy NIEBĘDĄCE obecnymi zadaniami rozwojowymi
- OF-054/055: nie ma zgłoszenia, że dobór kont jest błędny. Nie przeprowadzać projektu testowego na cudzych kontach.
- Izolacja czatów: istniejący przydział kart do requester/chat zapobiega kolizjom równoległych poleceń, nie jest nową funkcją do budowania. Nie izolować użytkownika od jego kont.
- Koszty: użytkownik potwierdził, że działają; nie rozpoczynać optymalizacji/testów kosztowych bez sygnału o problemie.
- Dźwiękowe przywoływanie: działa. Nie planować jego implementacji od zera.
- Wi-Fi PBOX1 w Kenii: chwilowe problemy z łącznością są oczekiwane. Monitor zdrowia i obsługa timeoutów istnieją. Nie tworzyć wielkiego projektu odporności ani testów awarii bez konkretnego nowego incydentu.
- Trwałość zadań: kontrolowany shutdown i zapis statusu interrupted istnieją i mają testy, więc nie otwierać osobnego wielotygodniowego programu testowego bez powodu.
- Duży katalog starych „DO ZROBIENIA” i historycznych „LIVE PENDING” wymaga aktualizacji statusu, a nie automatycznej implementacji.

## C. Reguła selekcji backlogu
NOWE P0/P1 tylko gdy: (a) właściciel wyraźnie zlecił funkcję, (b) odtworzono błąd w LIVE albo wiarygodnym logu, (c) istnieje mierzalna blokada bieżącej pracy lub wydania. Drobne testy regresyjne są częścią wdrożenia rzeczywistej zmiany, nie osobnym celem projektu.
PBOX1 zostaje hostem LIVE, SATANAMD pozostaje pomocniczy. Przed aktualizacją NIE czekać na 0 zadań, ale zachować backup, kontrolowany shutdown, interrupted, sprawdzalny rollback i zdrowe uruchomienie.

## Wynik wykonania P0 z 29.10
- Przeanalizowano rzeczywisty kod: instalator v0.14 już wywoływał kontrolowany CloseMainWindow bez wymagania 0 zadań; McpTaskManager już persystował interrupted i budził long-poll. Brak dotyczył starej angielskiej wiadomości z fikcyjnym ETA oraz niewidoczności komunikatu operatora podczas zamknięcia przez instalator.
- SOURCE zmieniono: McpTaskManager.UpgradeNotice = „Teraz nastąpi aktualizacja. Wróć do nas za kilka minut.”; Summary i maintenance używają tej treści, nie podają ETA. ControlledUpgradeDialog pokazuje tę treść i nie pyta o czas. MainForm przy OnFormClosing nakłada widoczny komunikat na okno również przy zamknięciu przez instalator; usuwa go, jeżeli anulowany zostanie shutdown z powodu braku trwałego zapisu. MCP tools/list nie zachęca do oczekiwania na bezczynność ani zastępczego serwera.
- Zaktualizowano testy dla przerwania 3 równoległych zadań, utrwalenia interrupted, long-poll oraz dokładnego komunikatu. MCP 1151 PASS / 0 FAIL, dotnet build Release exit0, 0 errors i 1 istniejące warning MSB3277. Instalator v0.14 wykonał LIVE release exit0, bez oczekiwania na 0 zadań.
- Preflight: backup 23 plików C:\Users\pboru\OpenFill\backups\pre-upgrade-notice-20261029; EXE i główne DLL hash SHA256 zgodne z wcześniejszym LIVE.
- Przed zamknięciem aktywne były 2 zadania cudzych czatów (YouTube i WhatsApp). Nie czekano na nie: maintenance reason ustalony dokładnie, następnie instalator wywołał controlled shutdown; oba zadania po restarcie mają status interrupted, outcome partial, zachowany task_id i continue_task_id. Nie ponowiono żadnego.
- Uwaga wdrożeniowa: te dwa rekordy mają jeszcze stary angielski komunikat i ETA, ponieważ zamykał je POPRZEDNI proces OpenFill, przed podmianą DLL. To prawidłowo ustalona przyczyna, nie podmieniać historii. Porównanie binariów OpenFill.Core.dll: obecny LIVE zawiera nowy polski tekst, nie zawiera starego „Expected return in approximately”. Następne kontrolowane zamknięcie będzie już wykonywać nowy kod.
- LIVE nowy PID 15232, lokalny MCP 48731. Końcowy release_smoke__v0.2 exit0: MCP_STATUS_OK, activeTasks0, JEV_HEALTH_OK/HTTP200, launcher Running, FAILED_CHECK_COUNT0. Jedno krótkie rozłączenie Remote Desktop PBOX1 podczas odczytu, samo odzyskało połączenie; brak podstaw do restartu Wi-Fi lub OpenFill.
- OF-247 pozostaje planowany dopiero przy określeniu docelowego promptu przez właściciela. Nie otwierać ponownie już działających kont, kosztów, dźwięków ani hipotetycznych projektów testowych bez rzeczywistego błędu.
