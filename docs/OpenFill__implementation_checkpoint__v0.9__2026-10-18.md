# OpenFill, checkpoint v0.9, 2026-10-18

## OF-240: usunięto przyczynę kolizji dokumentów

**Ustalenie**: lokalne dokumenty OpenFill są bieżącą wersją roboczą, a Google Drive (H:\\Mój dysk\\OpenFill\\docs) zawierał starsze kopie. Pierwotny skrypt `tools/sync_openfill_docs_gdrive__v0.1__2026-10-15_2343.ps1` synchronizował wyłącznie nowe pliki: dla istniejących różniących się plików emitował `Write-Warning File version collision, not overwritten` i nie robił aktualizacji. Cykliczny Harmonogram co 15 minut powtarzał te same ostrzeżenia, ale NIE nadpisywał tych dokumentów. Nie istnieje dowód, by pliki zewnętrzne były w tajemnicy nadpisywane.

**Zmiana w miejscu** w skrypcie wywoływanym bezpośrednio przez istniejące zadanie Harmonogramu: lokalny plik jest kanoniczny; przed wymianą różniącej się kopii Google Drive program archiwizuje starą wersję w `docs/_previous_versions/<nazwa-pliku>/` pod nazwą zawierającą skrót SHA-256, sprawdza hash starego dokumentu i archiwum, ponownie sprawdza oba źródła, aktualizuje plik Drive i sprawdza docelowy SHA-256. Następne uruchomienie pomija już identyczny dokument. Dla nowych plików pozostawiono create-only+hash, dla plików wykluczonych dawny filtr sekretów/rozmiaru. Testy mogą użyć izolowanych `-TestSource/-TestDestination` (obie wymagane) bez montowania H:.

- **Test izolowany**: `tools/test_sync_openfill_docs_gdrive__v0.1__2026-10-18.ps1`, 11 PASS / 0 FAIL: preview bez zmian, aktualizacja, kopia starej wersji, weryfikacja zawartości, drugi przebieg bez zmian, nowa rozbieżna wersja Drive też jest zachowana.
- **Dowód produkcyjny**: preview przed uruchomieniem: 9 `WOULD_UPDATE` i 1 `WOULD_COPY`. Harmonogram `OpenFill_Docs_GDrive_Sync` uruchomił poprawiony skrypt 18.10 o 15:01:01, LastTaskResult=0, utworzył 9 archiwów w 9 katalogach `_previous_versions`. Lokalny SHA-256 backlogu v1.7 jest identyczny z jego SHA-256 w Drive. Osobny ponowny przebieg: `SYNC copied=0 updated=0 archived=0 unchanged=92 skipped=0 scanned=92 dryRun=False`.
- **Publikacja Autosync**: odczytano zdalnie nowe pierwsze wiersze synchronizatora (metadata v0.2) na gałęzi `autosync/PBOX1` w GitHub, blob `eb1bbbe47fbb631c31fcd07ace14beec26f86183`.
- **Kompilacja .sln Release** po zmianie skryptu: 0 błędów, 1 znane ostrzeżenie MSB3277, exit 0.
- **Pełna regresja .NET**: 813 PASS / 0 FAIL, exit 0, PID 15608. Test skryptu dodatkowo 11 PASS / 0 FAIL.
- **Produkcja**: OpenFill 1.55 nadal działa, MCP read-only `openfill_info` potwierdził 1.55, maintenance=false; podczas kontroli działało jedno inne zadanie, którego nie zatrzymywano.
- **A10 read-only**: ostatnie 120 próbek telemetrii, 67 idle / 53 active, 0 waiting; okres 81,22 min, 3 zmiany PID, 2 luki, najdłuższa 994,85 s. To nie jest ciągły benchmark jednej instancji.
- **Brak zmian** w `C:\Users\pboru\OpenFill\data\stable` poza normalnym działaniem aplikacji i jej dotychczasowymi logami; pliki źródłowe aplikacji C# nie były zmieniane i nie wykonywano wydania.

## Oddzielny aspekt GUI / uprawnienia Windows

Samo zadanie Harmonogramu jest nadal interaktywne, nie ma `-WindowStyle Hidden`. Przed tą sesją próba zmiany `Set-ScheduledTask` odrzucona przez Windows (`Odmowa dostępu`, OF-UNEXEC-026). Nie ponawiano przez inną drogę. **Błąd funkcjonalny synchronizacji jest usunięty**, ale okno PowerShell może nadal mignąć co 15 min. Istniejący skrypt do jednorazowego wykonania z prawami administratora: `tools/apply_docs_sync_task_hidden__v0.1__2026-10-18.ps1`, eksportuje poprzednie ustawienia zadania i zmienia tylko pole akcji. Rozwiązanie tego aspektu nie wymaga kolejnego wydania OpenFill.

## Co dalej

Realizować plan `docs/OpenFill__next_action_plan__v1.0__2026-10-18.md` z priorytetem niezależnej diagnostyki OF-212 po upgrade oraz kart OF-230/231, ponadto OF-223/239 i A8/A9. Nie przedstawiać wcześniejszych odrzuconych patchy jako wykonanych. Bieżąca wersja produkcyjna pozostaje 1.55.
