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
