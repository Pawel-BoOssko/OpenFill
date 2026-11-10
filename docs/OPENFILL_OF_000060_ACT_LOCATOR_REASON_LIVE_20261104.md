# OF-000060: act_locator - prawidlowy kod braku elementu, LIVE

Wake OF-000060-A01-004921: 2026-11-04 00:49:21 +01:00, nowy lead po 000059. Handoff z SATANAMD zawieral projekt of, stary chat oraz numer 59; checkpoint odczytano z PBOX1.

## Potwierdzone zdarzenie
W rzeczywistym runId 20261103_234140_4cdbf5 (Meta Business Suite) odnotowano dwa bledy tool act_locator, step 33 i 40, app log seq 491 i 531: reason_code=selector_not_unique, observed_count=0, action_attempted=false.
SOURCE ToolRegistry zwracal selector_not_unique zarowno gdy matches.length=0, jak i gdy >1. Przy 0 wynik kodu byl nieadekwatny do faktow; dotychczasowy lokalny preflight retry nie obslugiwal braku elementu.

## Zmiana SOURCE
Plik: src/OpenFill.Core/Agent/ToolRegistry__v0.22__2026-10-29_2314.cs
- matches.length=0 => selector_not_found
- matches.length>1 => selector_not_unique
- istniejacy bounded retry przy timeout_ms>0 obejmuje selector_not_found; retry jest dopuszczalny tylko gdy action_attempted=false.
Bez zmian w wysylce, deduplikacji, ledger i uprawnieniach.
Backup: ten sam plik z przyrostkiem .before_of000060.

## Kompilacja, DEV i LIVE
Release publish: staging/of_000060, exit 0, tylko wczesniejsze ostrzezenia kompilatora.
DEV: staging/of_imp_20261031, PID 21160, Core SHA256 EEF5981711E0C338DB08EBB05C4964E7B8209318DE4D7B8592F267F36CDC4194.
Skrypt of_000060_activate_dev.ps1 wypisal DEV_RELEASE_OK i DEV_ACTIVATION_COMPLETED, lecz zewnetrzna sesja zglosila exit 1 bez stderr. Kod skryptu nie resetowal LASTEXITCODE po robocopy, ktory moze zwrocic 1 dla poprawnego kopiowania. To jest wyjasnienie na podstawie kodu, nie bezposredni pomiar ostatniego robocopy. Niezalezny odczyt potwierdzil DEV PID i SHA. Skrypt uzupelniono o wyzerowanie LASTEXITCODE po sukcesie; nie wykonywano ponownego wdrozenia DEV.
CheckOnly: RELEASE_READY_NO_IDLE_GATE.
LIVE: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True; bez bramki idle.
Niezaleznie: PBOX1 PID 20632, C:\Users\pboru\OpenFill\app\OpenFill.exe, Core SHA256 EEF5981711E0C338DB08EBB05C4964E7B8209318DE4D7B8592F267F36CDC4194, exe SHA256 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52, phase Running, MCP openfill_info working=[].
Rollback: C:\Users\pboru\OpenFill\app__rollback__before_release_20261104_025430.

## Granice i dalsze zadania
Brak wymuszonego produkcyjnego E2E zerowego selektora po wydaniu; przy nastepnym naturalnym wystapieniu sprawdzic reason_code i bounded preflight probes. W tej sekwencji nie bylo ponownej wysylki, zadnego wznowienia i zmian na kontach zewnetrznych.
LocalPlan draft production E2E nadal niepotwierdzony po JEV rejection, nie obchodzic blokady. D01/D02 batch send nadal PARTIAL i wymagaja autoryzowanych celow; Airbnb D03 anulowane. Obserwowac realne stopReason/task-review/read-streak, nie tworzyc domniemanych problemow. Istniejacy wake i rotacja bez zmiany.