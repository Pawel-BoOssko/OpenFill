# OpenFill: checkpoint v0.8, 2026-10-18 14:46 CEST

## Poprzedni stan
OpenFill 1.55 działa produkcyjnie od 2026-10-18 14:25, potwierdzony przez MCP. Poprzedni checkpoint v0.7. Katalog produkcyjnych danych `C:\Users\pboru\OpenFill\data\stable` pozostawiony bez modyfikacji.

## Nowy plan
Utworzono `docs\OpenFill__next_action_plan__v1.0__2026-10-18.md` (12 pakietów działań) na podstawie backlog v1.7, części A. Priorytet: diagnoza migających PowerShell, następnie kontrolowana poprawa widoczności i logowania oraz niezależne pakiety MCP/backup/testy/GUI/Radar.

## OF-240, migające okna PowerShell

- PRZYCZYNA POTWIERDZONA: zadanie Harmonogramu `OpenFill_Docs_GDrive_Sync` uruchamia w trybie `LogonType=Interactive` widoczne `powershell.exe` co 15 min. Ostatnio 14:31:31, następny termin w chwili audytu 14:46:46, LastResult=0.
- Źródło komunikatu: `tools/sync_openfill_docs_gdrive__v0.1__2026-10-15_2343.ps1`, `Write-Warning "File version collision, not overwritten: ..."`. Skrypt nie nadpisuje różniących się dokumentów.
- Realny read-only test `-DryRun`: 90 plików, 80 identycznych, **9 warningów o kolizji**, 1 oznaczony `WOULD_COPY`, exit 0. Pełny wykaz nazw w `docs/OpenFill__powershell_popup_diagnostic__v0.1__2026-10-18.md`.
- Przygotowano `tools/run_openfill_docs_sync__v0.1__2026-10-18_1440.ps1` z logiem dziennym `%LOCALAPPDATA%\OpenFillDocsSync\docs-sync-YYYY-MM-DD.log`. Launcher został wykonany z `-DryRun`, wynik i ostrzeżenia zapisane (START/SYNC/DONE), brak błędu.
- Windows `Set-ScheduledTask` przez LC MultiHost odmówiono: `Odmowa dostępu`, exit code 1; zadanie systemowe pozostało bez zmian. Zapisano jako `OF-UNEXEC-026` w `docs/OpenFill__log_niewykonanych_komend__v0.1__2026-10-18.md`.
- Przygotowano **jeden manualny krok admin** w `tools/apply_docs_sync_task_hidden__v0.1__2026-10-18.ps1`. Po uruchomieniu przez właściciela z podwyższonymi uprawnieniami eksportuje poprzednie ustawienia zadania i przestawia akcję na launcher z `-WindowStyle Hidden`, zachowując wyzwalacz co 15 min oraz politykę nienadpisywania. **Nie wykonano** tego kroku, stąd migające okno może się nadal pojawiać.
- Drugie potencjalnie widoczne zadanie `OpenFill_Source_Backup_Daily` uruchamia PowerShell raz dziennie o 03:30, lecz nie zawiera identycznego komunikatu o kolizjach; nie modyfikowano.
- Autosync wykonuje się co 10 min z `-WindowStyle Hidden`, inny mechanizm, nie jest źródłem tych konkretnych ostrzeżeń.

## Kolejne niezależne prace

Read-only audyt źródeł `McpTaskManager.PrepareShutdown`, `McpTaskManager.Complete` i instalatora, pod kątem OF-212 i wcześniejszego `stopped_by_user` po upgrade. Nie ustalono jeszcze jednoznacznej przyczyny przejścia stanu ani nie zmieniano kodu: sprawdzić w odizolowanym teście utrwalenie `interrupted` przed `_host.Stop` i brak nadpisania przez zakończenie runnera. Nie ponawiać wcześniej odmówionej operacji edycji.

## Status

1. Plan zapisany: TAK.
2. Diagnoza okna: UKOŃCZONA / REPRODUKOWANA.
3. Logowanie uruchomień: GOTOWE, dry-run PASS.
4. Harmonogram ukryty: JESZCZE NIE, wymaga jednorazowego kroku administratora Windows.
5. Kod źródłowy aplikacji: BEZ ZMIAN. Nowe wydanie: NIE WYKONYWANE. Produkcja 1.55 pozostaje bez restartu.
6. Backlog: dodano OF-240 jako nowe zgłoszenie użytkownika, status bez fałszywego DONE.
