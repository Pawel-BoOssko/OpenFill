# OF-000054: audyt uniwersalnej infrastruktury i wydanie D01 readiness
Wake: OF-000054-A01-224916, 2026-11-03 22:49:16 +01:00 Warszawa. SOURCE, DEV, LIVE: PBOX1.

## Potwierdzony stan architektury (bez nadinterpretacji)
- LIVE: A01-A04 terminalowe CLI rg/jq/SQLite/FTS5, FileFilterService, PageCaptureService, SHA/manifest. A06-A09 automatyczny read_view, przechowywane capture_id, filtrowanie lokalne. A13-A21 act_many (do 40 lokalnych kroków), fused navigate i krótkie odczyty. A05/E02 podpisane receptury, rzeczywisty signed-account reuse LinkedIn. E03 liczba model/tool calls, cached/uncached tokens, koszt, pageReadRequests, CDP read-call w części metod, per-batch timings. B01-B03 SQLite/pamięć operacyjna, ledger. D01/D02 batch API exposed w LIVE jako candidate.
- PARTIAL: cache CurrentTextCaptureCache ograniczony do neutralnych stron, live exact DOM/SHA; brak przekrojowego produkcyjnego dowodu zaoszczędzonych calls. Katalog procedures/registry.json zawiera candidate, nie oznacza zweryfikowanych skutków. D01/D02 efekty rzeczywiste batch send bez zatwierdzonych odbiorców i wiadomości nie mają pełnego acceptance. D01 sidebar cold-start: poprzedni task zwrócił sidebar_not_loaded przy pierwszym odczycie, na tej samej karcie potem loaded.
- DEV, nie LIVE default: LocalPlanModelDevRunner jest w #if DEBUG, AgentRunner.RunPlannedDevAsync oraz RunEffectPlanDevAsync są DEV-only. Produkcyjny RunAsync nadal wykonuje iteracyjną pętlę model -> tools. LocalPlanExecutor i LocalPlanControlFlow, lokalny browser CDP, EffectJournal oraz bridge providerów istnieją, ale nie są ogólnym produkcyjnym routerem planów. Nie usuwać #if DEBUG bez odbioru bezpośredniej integracji.
- Planowane/niepotwierdzone: centralny worker kolejki planów, kompletne production routing of.plan.v1, uniwersalne wykonywanie skutków dla wielu domen. D03 Airbnb ANULOWANE, nie wznawiać.

## Dane liczbowe, osobno fakt od scenariusza
- Kontrolowany A/B z 2026-11-02 na lokalnym formularzu WebView2: AgentRunner legacy 5 model calls, 39 537 tokens, 17 763 ms; planner 1 call, 906 tokens, 5 468 ms; oba osiągnęły oczekiwany DOM. 80% mniej calls w JEDNYM teście, bez generalizacji.
- Audyt historii OF cost stage14: 76 566 rzeczywistych calls, 1550 zrekonstruowanych operacji, 41,459336 USD. Dla 1545 porównywanych operacji scenariusz referencyjny 5515 calls wobec 76 422 rzeczywistych; 92,8% to HIPOTETYCZNA redukcja według norm, NIE osiągnięta oszczędność.
- Raport E01: 1477 historycznych zadań; najwyższe sumy wg zaklasyfikowanych intencji obejmowały YouTube Shorts 5810, LinkedIn send_message 4343, Meta reels 2854, WhatsApp send_message 2749, LinkedIn invitations 2086 model calls. Ranking zużycia nie jest rankingiem ROI.

## Jedna zmiana SOURCE OF-000054
- W pliku src/OpenFill.Core/Browser/WhatsAppBrowser__v0.1__2026-10-29_2310.cs, metoda ReadVisibleNamesAsync zmieniono istniejące lokalne ReadReadyAsync(ct, 8000) na ReadReadyAsync(ct, 20000). Bez dodatkowego wywołania Luny. Obowiązujący poll w ReadReadyAsync co ~2500ms. Zachowano status sidebar_not_loaded w razie rzeczywistego timeoutu. Backup SOURCE .before_of000054.
- Dotnet publish -c Release -o staging/of_000054 exit 0, ostrzeżenia historyczne. DEV backup staging/of_000054_backup_pre_dev_activation, DEV_RELEASE_OK pid=13504. CheckOnly RELEASE_READY_NO_IDLE_GATE.
- LIVE release: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True, exit 0. Core SHA256 602870AA1F067D2F92D3D9148D65A76B237C8FFABE276E6663B9EDBAFBE78910.
- Niezależny LIVE: PID 8712, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, Core SHA zgodny, EXE SHA256 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52, OpenFill-open phase Running, OpenFill MCP openfill_info working=[].
- Rollback: C:/Users/pboru/OpenFill/app__rollback__before_release_20261104_005602 (lokalny zegar hosta ma inną strefę niż Warszawa).
- Brak dodatkowego rzeczywistego cold-start acceptance po release; oszczędność wywołań jest oczekiwana, a nie zmierzona.

## Diagnostyka dostępu w tym wybudzeniu
- LC MultiHost target=pbox1 najpierw target_unavailable/fetch failed; OpenFill MCP Connection timed out; Remote Desktop Commander file reads pending, list_devices PBOX1 Online z opóźnionym LastSeen. SATANAMD Test-Connection PBOX1 False, TCP445 pod nazwą PBOX1 False.
- Tailscale na SATANAMD BackendState=Running, peer openfill-pbox1 Online. Tailscale ping do 100.97.142.52 uzyskał pong 285ms; direct TCP 443=True, 445=True, 3389=False. Następnie LC MultiHost na PBOX1 wrócił i wszystkie prace wykonano bez alternatywnego mostu. Nie ustalono pierwotnej przyczyny chwilowej niedostępności narzędzi.

## Zamknięta lista prac po audycie (bez rozbudowy o wymyślone awarie)
1. Zdefiniować i zweryfikować produkcyjny opt-in routing 1-call planner do of.plan.v1 na obecnym BrowserController / realnym CDP, ograniczony najpierw do scenariuszy read-only/nawigacja/fill bez save/send.
2. Po osiągnięciu realnego acceptance produkcyjnego planera porównać te SAME cele legacy vs planner, liczyć model calls, tokeny, koszt i końcowy efekt, nie mnożyć benchmarków bez decyzji.
3. Dopiero potem obsługa produkcyjnych side-effects poprzez istniejący EffectJournal i weryfikację rzeczywistą, z deduplikacją effect_id.
4. D01/D02 realne batch-send acceptance dopiero przy dokładnie zatwierdzonych celach; D03 anulowane.
Wake/harmonogram/rotacja niezmienione.
