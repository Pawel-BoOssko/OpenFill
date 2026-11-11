# OpenFill: GitHub przez konektor i Enter w karcie pytania Luny
Data: 2026-11-04. Host SOURCE i LIVE: PBOX1.

## Zakres zgodny z korektami użytkownika
1. NIE zakładać nowego repo GitHub ani nie uruchamiać autosync w ramach tej decyzji. Konto GitHub Pawel-BoOssko posiada już repozytorium OpenFill, w local SOURCE git origin wskazuje na to samo. Użytkownik wyraźnie nie zlecał modyfikacji repo: celem jest to, aby modele zlecające obsługiwały operacje GitHub swoim connector API, nie przeglądarką OpenFill/Luną. Repo i remotes pozostawione bez zmian.
2. Gdy Luna pyta osobę o problem/dane/decyzję i pokazuje kartę showAsk, pole textarea.note: Enter wysyła odpowiedź z uwagą, Shift+Enter wstawia nową linię. Zakres dotyczy karty pytania, NIE zwykłego pola uruchamiania zadań. W oknie wymagającym jawnego Yes/No decyzja nie jest automatycznie podejmowana przez Enter.
3. Zachować wyjątek dla własnego odczytu przez Lunę poczty z kodem/linkiem logowania w celu przeprowadzenia autoryzowanego procesu. Standardowe maile nadal przez native API/MCP modelu zlecającego.

## Zmiany SOURCE i kontrola
- Assets/panel__v0.18__2026-10-12_2034.html: zmieniono placeholder; keydown dla .note w showAsk wysyła przy Enter bez Shift i bez kompozycji IME; Shift+Enter zachowuje normalny newline; preventDefault zapobiega wstawieniu Enter po wysyłce; repeat blokuje duplikaty. Także pojedyncze pole answer input ma preventDefault i ochronę przed repeat. Istniejący c.dataset.sending blokuje wielokrotne wysłanie; istniejąca withNote dołącza wieloliniową uwagę do odpowiedzi.
- Mcp/McpServer__v0.24__2026-10-28_1648.cs: instrukcje initialize i tool description start task ustawiają GitHub connector-first, szczególnie repo, commits, branches, files, PRs, issues, status. Nie używać OpenFill jako fallback dla GitHub operations. Wyjątek przy literalnym życzeniu pracy w graficznym GitHub UI.
- Mcp/McpTaskManager__v0.21__2026-10-28_2216.cs: fail-fast dla operacji repo GitHub w nowym tasku przed stworzeniem record, browser tab lub modelem. Zwraca status use_native_connector z next_step do użycia konektora GitHub. Jawny browser UI task dopuszczony.
- Backup source: *.before_enter_github_20261104 dla dwóch plików, *.before_github_routing_20261104 dla McpTaskManager.

## Wydanie i dowody
- dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_github_route_enter_20261104: exit 0, stare ostrzeżenia; Core SHA256 09E32B0594EB084357E894CAC687EF6FFE07E0EE5409BF8E10F68DC3FAE11AB8.
- Pierwsza diagnostyka pliku SHA dla nieistniejącego OpenFill.App.dll: komunikat Resolve-Path Cannot find path ...; przyczyna: rzeczywista nazwa to OpenFill.dll i OpenFill.exe. Poprawiono diagnostykę, Core SHA potwierdzono, build bez problemów.
- DEV activator tools/of_github_route_enter_20261104_activate_dev.ps1: DEV_RELEASE_OK PID22368, backup staging/of_github_route_enter_20261104_backup_pre_dev_activation.
- CheckOnly: RELEASE_READY_NO_IDLE_GATE, SHA zgodny.
- LIVE: tools/of_imp_release_live_ALWAYS_20261101.ps1, exit0, BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True. Niezależne sprawdzenie EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, PID21020, OpenFill-open Running, Core SHA jak wyżej, EXE SHA256 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52.
- LIVE rollback C:/Users/pboru/OpenFill/app__rollback__before_release_20261104_024111.
- Syntaktyczna weryfikacja jedynego inline JS w panel.html przez node --check: exit 0.
- Rzeczywista kontrola MCP: wywołanie openfill_start_task dla testowego zadania utworzenia issue na GitHub zwróciło status=use_native_connector i komunikat No OpenFill browser task was started. Nie utworzono issue ani tasku. Nie testowano rzeczywistej karty pytania z wpisem użytkownika, gdyż wymagałoby sztucznego zadania Luny. Kod i syntaktyka poprawne, akceptacja na kolejnej naturalnej karcie.
- Wtyczki mogą buforować stare tool descriptions; mechanizm fail-fast jest po stronie samego serwera LIVE, działa także dla klienta ze starą definicją.
 
## Kontynuacja
- Bez powtarzania ukończonego. Monitorować naturalne answer cards, czy Enter+ShiftEnter działa; monitorować odrzucenia GitHub oraz czy model faktycznie użył natywnego konektora.
- D01/D02 batch send acceptance nadal partial; produkcyjny LocalPlan E2E nieudowodniony, nie uruchamiać kampanii testowej. Pracować w istniejącym wybudzaniu OF, nie tworzyć drugiego mechanizmu. D03 Airbnb anulowane.
