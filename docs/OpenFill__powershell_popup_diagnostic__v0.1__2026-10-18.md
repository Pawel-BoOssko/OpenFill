# OpenFill: migające okna PowerShell, raport diagnostyczny

Data: 2026-10-18, 14:43-14:45 CEST. Komputer: PBOX1. Kontrole bez usuwania plików lub modyfikowania profilu produkcyjnego.

## Przyczyna potwierdzona

Harmonogram Windows, zadanie `OpenFill_Docs_GDrive_Sync`. Akcja:
`powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\tools\sync_openfill_docs_gdrive__v0.1__2026-10-15_2343.ps1"`.

- Trigger: co 15 minut, `PT15M`; 18.10 ostatnie uruchomienie 14:31:31, następne 14:46:46; ostatni wynik 0.
- Principal: `pboru`, `LogonType=Interactive`, `Settings.Hidden=False`, brak argumentu `-WindowStyle Hidden`.
- W źródłowym skrypcie różna zawartość pliku lokalnego i istniejącego dokumentu H: powoduje `Write-Warning ("File version collision, not overwritten: " + file.Name)`. Dokument nie jest nadpisywany ani usuwany, tylko pomijany.
- Reprodukcja: `sync_openfill_docs_gdrive__v0.1__2026-10-15_2343.ps1 -DryRun`, kod wyjścia 0, `SYNC copied=0 unchanged=80 skipped=9 scanned=90 dryRun=True`. Dokładnie **9 ostrzeżeń o kolizjach**, 80 dokumentów identycznych, 1 nowy plik oznaczony WOULD_COPY.
- Różniące się dokumenty to: `openfill_autonomous_log__v0.1__2026-10-18.md`, `openfill_backup_integrity_checkpoint__v0.1__2026-10-17_1235.md`, `openfill_master_action_plan__v1.2__2026-10-16_1142.md`, `openfill_master_action_plan__v1.7__2026-10-17_1145.md`, `openfill_radar_mcp_contract__v0.1__2026-10-17_1424.md`, `openfill_radar_mcp_contract__v0.2__2026-10-18.md`, `openfill_technology_map_reference__v0.1__2026-10-17_1615.md`, `OpenFill__implementation_checkpoint__v0.6__2026-10-18_ChatGPT.md`, `OpenFill__log_niewykonanych_komend__v0.1__2026-10-18.md`.
- W chwili sprawdzenia nie znaleziono powiązanych zdarzeń w `Microsoft-Windows-TaskScheduler/Operational`. Podstawą rozpoznania są: akcja, wyzwalacz, źródło komunikatu, reprodukcja samego skryptu; brak konieczności interpretowania znikającego okna na oko.
- `Autosync repozytoriów` wykonuje się co 10 min z `-WindowStyle Hidden`, więc nie pasuje do tego ostrzeżenia. `OpenFill_Source_Backup_Daily` to osobny skrypt uruchamiany raz dziennie o 03:30 bez `-WindowStyle Hidden`, ale nie zawiera wskazanego komunikatu. Dawne zadania `OFci*` i `OFinst*` są jednorazowe i nie mają obecnej godziny następnego uruchomienia.

## Przygotowana poprawka

1. `tools/run_openfill_docs_sync__v0.1__2026-10-18_1440.ps1`: uruchamia dotychczasowy synchronizator bez zmiany polityki kolizji, zapisuje stdout/warnings/errors w dziennych logach `%LOCALAPPDATA%\OpenFillDocsSync\docs-sync-YYYY-MM-DD.log`.
2. Uruchomienie nowego launchera w trybie `-DryRun` zakończyło się bez błędu. Sprawdzono wpisy `START`, 9 kolizji, `SYNC ... dryRun=True` oraz `DONE` w rzeczywistym pliku `docs-sync-2026-10-18.log`.
3. Docelowe polecenie Harmonogramu: `powershell.exe -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File "C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\tools\run_openfill_docs_sync__v0.1__2026-10-18_1440.ps1"`.
4. Próba aktualizacji akcji przez `Set-ScheduledTask` z obecnych uprawnień sesji zdalnej zakończyła się `Odmowa dostępu` (exit 1). Sprawdzono `Get-ScheduledTask`: stara akcja jest nadal ustawiona. Nie próbowano obchodzić odmowy. Patrz `OF-UNEXEC-026` w logu niewykonanych komend.
5. Przygotowany pojedynczy krok administracyjny, bez uruchomienia przez agenta: `tools/apply_docs_sync_task_hidden__v0.1__2026-10-18.ps1`. Najpierw eksportuje XML istniejącego zadania do `Documents\OpenFill-Backups\Diagnostics`, potem zmienia wyłącznie akcję i weryfikuje argumenty; pozostawia częstotliwość co 15 minut, konto i zasady synchronizacji.
6. Ręczny krok właściciela: uruchomić **PowerShell jako administrator**, wykonać:
`& "C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\tools\apply_docs_sync_task_hidden__v0.1__2026-10-18.ps1"`
Następnie sprawdzić następne automatyczne uruchomienie i log `%LOCALAPPDATA%\OpenFillDocsSync\docs-sync-YYYY-MM-DD.log`.

## Ograniczenie i status

- PRZYCZYNA: potwierdzona, reprodukowana.
- LOGOWANIE: przygotowane i testowane w DryRun.
- LOGIKA SYNCHRONIZACJI: NAPRAWIONA w istniejącym skrypcie wywoływanym przez Harmonogram. Przy konflikcie starsza kopia Drive jest najpierw archiwizowana do `_previous_versions/<nazwa pliku>/`, sprawdzana SHA-256, a bieżący dokument aktualizowany i ponownie weryfikowany. Test syntetyczny: 11 PASS / 0 FAIL. Run Harmonogramu 18.10 o 15:01:01: 9 archiwów starszych wersji, 9 aktualizacji, LastTaskResult=0; kolejny przebieg ręczny bez zmian: 92 identyczne / 0 pominięć / 0 aktualizacji. Hash backlog v1.7 Drive=local.
- ZMIANA SPOSOBU WYŚWIETLANIA OKNA: NIEWDROŻONA (brak uprawnień zdalnej sesji). Harmonogram nadal uruchamia widoczną konsolę co 15 minut, mimo że nie ma już powtarzających się błędów kolizji. Jednorazowy krok administratora w istniejącym skrypcie pozostaje aktualny.
- Nie zmieniano `C:\Users\pboru\OpenFill\data\stable` ani produkcyjnego procesu OpenFill.
