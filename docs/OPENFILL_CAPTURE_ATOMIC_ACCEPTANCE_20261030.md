# OpenFill: naprawa niekompletnych eksportow PageCaptureService

Data: 2026-10-30, PBOX1. Zakres: SOURCE i izolowane STAGING. Bez wdrozenia LIVE.

## Przyczyna i zmiana
- Wczesniejszy SaveSnapshotAsync zapisywal pliki .txt, .filtered.txt, .elements.json, .html bezposrednio do katalogu docelowego.
- Wyjatek przy pozniejszym pliku, np. HTML > 10 000 000 znakow, mogl pozostawic czesciowy eksport z wczesniejszych plikow.
- SOURCE: src/OpenFill.Core/Agent/PageCaptureService__v0.1__2026-10-30.cs.
- Kazdy wynik jest zapisywany najpierw pod nazwa .pending, walidowany i haszowany, a po udanym zakonczeniu zestawu przenoszony pod nazwy docelowe.
- Przy wyjatku usuwane sa pliki tymczasowe i ewentualnie czesciowo opublikowane. Nie zmieniono formatu manifestu ani docelowych nazw.
- Pozostaje bardzo krotkie okno miedzy kolejnymi File.Move; jest to transakcyjne sprzatanie po niepowodzeniu, a nie jednoczesna atomowa publikacja calego zestawu.

## Dowody
- SOURCE pelna regresja: 1461 PASS, 0 FAIL, exit code 0 (poprzednio 1457 PASS). Log: docs/SOURCE_CAPTURE_ATOMIC_FULL_20261030.log.
- Nowa sekcja testowa odtwarza blad limitu rozmiaru w kolejnym pliku po wczesniejszym zapisie i sprawdza 0 pozostalych plikow.
- Nowa sekcja potwierdza poprawne udostepnienie 3 kompletnych plikow oraz brak *.pending.
- Fizyczny WebView2 w osobnej instancji QA: PASS. 4 pliki MCP, 3/3 natywne pobrania Completed, MultipleAutomaticDownloads Allow, requester/chat isolation PASS.
- Test form login: przed zalogowaniem authentication_required i 0 plikow; po zalogowaniu saved i 4 pliki.
- Dowod fizyczny: docs/OPENFILL_CAPTURE_ATOMIC_PHYSICAL_QA_20261030.json.
- Publikacja STAGING: exit 0 po koncowym republish. Log: docs/SOURCE_CAPTURE_ATOMIC_STAGING_REPUBLISH_20261030.log.
- STAGING: C:\Users\pboru\OpenFill\staging\capture-atomic-20261030.
- SHA256 OpenFill.Core.dll: SOURCE Release net10.0 = STAGING = 9B3B539262C65B4B4CF55CC7CD3673470DED2D7F9DE9CCA5A61573963F683FE3.
- LIVE Core DLL (niezmieniona): C4FE4897927EF0F40370F4A7F26372A1F82B5D553B1065F035A4B01F997A2E10.
- Tymczasowy katalog QA usunieto po skopiowaniu JSON z wynikami. Nie uruchamiano produkcyjnej instalacji QA rownolegle w jej profilu.

## Otwarty odbior
- REAL-LINKEDIN-AUTHENTICATED-E2E: PENDING. Izolowana QA nie miala sesji LinkedIn; rzeczywiste /messaging/ przekierowywalo na /login/ i zwracalo authentication_required, 0 plikow. Nie ma dowodu poprawnego eksportu zalogowanej strony.
- Rzeczywiste upgrade podczas oczekiwania na interakcje czlowieka oraz wielogodzinna retencja tej samej fizycznej karty bez limitu: otwarte od poprzednich raportow.
- Nie wdrazac nowej architektury do LIVE przed zakonczeniem brakujacego warunku LinkedIn.
- Networking Radar pozostaje poza zakresem; bez git commit/push/reset.

## Stan LIVE
- OpenFill 1.57, PID 21012, start 2026-10-30 10:41:55 EAT. Po QA nadal jedyny proces OpenFill na PBOX1.
- Bez restartu i bez zmiany plikow LIVE.
