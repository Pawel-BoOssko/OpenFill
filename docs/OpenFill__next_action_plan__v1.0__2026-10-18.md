# OpenFill: plan dalszych prac v1.0

Data: 2026-10-18. Repozytorium źródeł na PBOX1: `C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill`.
Źródło zadań: kanoniczny `docs/openfill_master_action_plan__v1.7__2026-10-17_1145.md`, tylko część A. Poprzedni checkpoint: `docs/OpenFill__implementation_checkpoint__v0.7__2026-10-18_1430.md`.
Stan wejściowy: **OpenFill 1.55 wdrożony i potwierdzony przez MCP**, 813 PASS/0 FAIL, jeden znany warning MSB3277, kopia poprzedniej aplikacji `C:\Users\pboru\OpenFill\app-1.52`. `C:\Users\pboru\OpenFill\data\stable` to dane użytkownika, nie zmieniać w diagnostyce.

## Zasady wykonania

1. Czytanie/edycja plików repozytorium przez Desktop Commander: read_file/start_search, edit_block dla jednej małej zmiany, write_file dla nowych raportów/testów. Po zmianie klasy nowa nazwa `Nazwa__vX.Y__RRRR-MM-DD_GGMM.cs`, metadata w pierwszym wierszu, stary plik usunięty; aktualizować referencje, build i pełny test.
2. LC MultiHost: tylko czynności systemowe, w tym Scheduled Tasks, uruchomione procesy i Event Log. Kontrole najpierw read-only. Nie wywoływać skryptów instalacji/kill/restart w celu zdiagnozowania migającego okna.
3. Nowe niewykonane polecenia opisywać w `docs/OpenFill__log_niewykonanych_komend__v0.1__2026-10-18.md`: narzędzie, dokładne polecenie/operacja, skala, komunikat, zakres. Nie ponawiać zablokowanej operacji innym narzędziem. Błędy techniczne odróżniać od odmowy bezpieczeństwa.
4. Po każdym pakiecie zapisać krótki checkpoint; nie przedstawiać testów SOURCE jako rzeczywistego odbioru GUI ani produkcyjnego wdrożenia. Nowych wersji nie instalować automatycznie bez poprawnej kontroli stanu, kopii i testów; wersja 1.55 pozostaje punktem odniesienia.
5. Pracować na kilku niezależnych zadaniach A jednocześnie tam, gdzie nie kolidują; zachować dane produkcji i uzgodnione teksty Luny/OF-236/238.

## Harmonogram prac

| Nr | Pakiet | Konkretne czynności | Dowód ukończenia |
|---|---|---|---|
| 1 | Diagnostyka migających okien PowerShell (NOWE zgłoszenie) | Odczytać harmonogram PBOX1, wyzwalacze i akcje z PowerShell/cmd/OpenFill/Autosync; logi Task Scheduler Operational, PowerShell, aplikacji, Autosync, instalatora. Ustalić polecenie, częstotliwość, źródło komunikatu o nadpisywaniu. Nie zgadywać. | Raport z nazwą zadania/procesu, linią poleceń, czasami uruchomień, dowodem z logu i sposobem usunięcia migania |
| 2 | Uspokojenie okien + obserwowalność (NOWE zgłoszenie) | Jeżeli źródłem jest własny harmonogram lub autosync, zaproponować najmniejszą poprawkę: niewidoczne uruchamianie lub logowanie do pliku zamiast konsoli bez utraty diagnostyki; test ręcznego startu i powtórzenia przez scheduler. Nie wyłączać synchronizacji. | Brak zbędnych okien przy zachowaniu działania zadania, log wyników i dowód triggeru |
| 3 | OF-212/218/219/206-211, ciągłość zadań | Wyjaśnić po wdrożeniu 1.55 przypadek aktywnego task_id, który przeszedł na `stopped_by_user` zamiast `interrupted`; sprawdzić kontrolowane shutdown i trwałość idempotency, restore/orphan, dodać niezależne testy bez ponawiania uprzednio odmówionej edycji. | Powtarzalny test po zamknięciu i starcie oraz dokumentacja stanu task_id |
| 4 | OF-230/225-235, karty/GUI | Test rzeczywistego reuse tego samego czatu i domeny, pięć kart z kolejką, utrzymanie URL/sesji po godzinach, bieżący tekst progresu. Nie nazywać reopen URL zachowaniem niezapisanego formularza. Wcześniejsze odrzucone patchy tylko opisać. | Jawne obserwacje GUI, tab_id, godzin i kolejnych zadań; regresja pełna |
| 5 | OF-239, zastępczy responder MCP | Audyt aktualnego watchdog i `openfill_watchdog_mcp_gap`; model protokołu dla status/start_task, brak podszywania się pod aktywny proces; bez produkcyjnego wdrożenia i bez ponawiania wcześniej odrzuconego konkretnego patcha. | Test protokołu w izolacji i opis punktów nadal otwartych |
| 6 | OF-223, odtwarzanie backupu | Potwierdzić zaszyfrowaną kopię, manifest SHA-256 i możliwość odizolowanego restore na SATANAMD bez eksportu klucza. Nie modyfikować `data\stable`. | Raport restore ze sprawdzonymi hashami albo precyzyjny wykaz nieukończonych kroków |
| 7 | OF-117 i testy niestabilne | Odczytać wyniki 3 przebiegów, oddzielić niestabilny start Chromium/timeout testu OF-231, audyt testowych ścieżek; wcześniej odmówionych operacji nie ponawiać. | Próbki i przyczyna, trzy kolejne pełne przebiegi 0 FAIL po dozwolonej zmianie |
| 8 | OF-187/140/143/149/176, dostarczanie | Audyt artefaktu release, brak danych profilowych i sekretów, autosync, podpisy/hashe, interaktywna sesja Windows, bezpieczny rollback i smoke. | Protokół SOURCE/BUILD/STAGED/DEPLOYED/LIVE, znane ostrzeżenia i dowód przywrócenia |
| 9 | A8 Networking Radar: OF-155-164/178/185 | Weryfikacja rzeczywistego potwierdzenia rejestracji, idempotencja, brak dubli, Calendar dopiero po dowodzie, kontrakt z OpenFill. Bez niezamówionych zgłoszeń. | E2E i status każdej rejestracji z wiarygodnym dowodem |
| 10 | A9 Technology Atlas: OF-029/030/168/184 | Kontrola 41 tematów/7 kategorii, banera, rozdzielczości/DPI, wyszukiwania i kopiowania w prawdziwym GUI. | Test źródła + obserwacja w sesji użytkownika |
| 11 | A10: OF-031-034, telemetria | Zbieranie CPU/WS/Private Bytes, PID/timestamp i stanu zadań; rozdzielenie okresów idle/active i luk, porównanie tej samej liczby kart. Bez pochopnych wniosków o wycieku pamięci. | Zapisane próbki i raport jakości, bez zmian stanu produkcji |
| 12 | Synchronizacja i raport końcowy | Zaktualizować backlog A, log niewykonanych komend, checkpoint; sprawdzić faktyczne pliki na `autosync/PBOX1`. Pozostające do zrobienia zachować, nie oznaczać sztucznie jako ukończonych. | Raport: zmienione pliki, testy, wydania, synchronizacja, otwarte punkty |

## Priorytet tej sesji

Najpierw 1 i 2 (migające PowerShell). Równolegle read-only punkt 3 i gotowość punktów 6/8, następnie niezależne kroki części A zgodnie z wynikami. Każdą zmianę źródła testować. Jeśli problem wymaga działań destrukcyjnych wobec aktualnie pracującej produkcji, najpierw zgromadzić pełny dowód i procedurę cofnięcia.

## Status wykonania

- 1: W TRAKCIE
- 2-12: DO WYKONANIA / według dostępnych dowodów z checkpoint v0.7.
