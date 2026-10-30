# OpenFill — odbiór najnowszej karty i logów lifecycle
Data 2026-10-30, PBOX1, godziny EAT.
Zakres: wyłącznie OpenFill. Networking Radar nietknięty.

## Ustalenie przyczyny
MCP automatycznie znajdował starsze podobne zadanie przez FindSimilarFinished/SameJob i przekazywał jego task_id do RunOptions.ContinueFromKey. TabManager traktował to jak jawnie wybrane continue_task_id, zamiast preferować najnowszą istniejącą fizyczną kartę danego czatu i domeny. Stąd reason=previous_tab_closed_reopened_saved_url, mimo że brak dowodu rzeczywistego zamknięcia karty. Dotychczasowy MainForm tworzył TabManager(...releaseFinishedTabs:false), a limit maxTabs=5.
## Zmiany SOURCE
- RunOptions.AutomaticContinuation rozróżnia semantyczny wcześniejszy task od jawnego continue_task_id; MCP ustawia flagę tylko przy auto-dopasowaniu.
- TabManager.FindPrevious dla automatycznego dopasowania wybiera najnowszą fizycznie istniejącą zakończoną kartę z tego samego requester/chat i domeny; potem najnowszy zapisany URL. Jawne continue_task_id nadal wybiera dokładnie wskazaną historię. Inne czaty nie przejmują cudzych kart przy auto.
- Lifecycle Action<string> podłączone z MainForm do EventLog: akcje reuse, reopen_saved_url, new_tab, retain_finished, close (manual_user_close, capacity_limit_N), close_failed. Bez logowania URL/treści pól.
- Limit 5 kart, ustawienia i polityka releaseFinishedTabs=false bez zmian.
- Backup źródła: backups/tab_reuse_lifecycle_20261030 (6 plików).
## Testy SOURCE
- Full: Passed 1369, Failed 0; dotnet run OpenFill.Tests -c Release --no-restore, exit 0.
- Test automatycznego wyboru starszego podobnego task_id potwierdził wybór najnowszej fizycznej karty, a explicit continueTaskId pozostaje dokładnym wyborem. Testowano izolację czatów, ręczne zamknięcie i logowanie zamknięcia przy limicie oraz błąd zamknięcia.
## LIVE
- Backup przed wdrożeniem C:\Users\pboru\OpenFill\backups\pre-tab-lifecycle-20261030: 23 pliki, 0 różnic SHA256.
- Instalator tools/install__v0.14__2026-10-28_2108.ps1 exit0. Log C:\Users\pboru\AppData\Local\Temp\openfill_install_20261030_104139.log.
- PID OpenFill 21012 start 10:41:55 EAT. release_smoke v0.3: MCP OK, 0 aktywnych, FAILED_CHECK_COUNT=0, LIVE_SMOKE=PASS.
- Test LIVE trzy zadania ten sam requester/chat/domain whatsapp.com: OF-20261030-1042-cfa6, OF-20261030-1043-2744, OF-20261030-1043-eae8, wszystkie done/success; tab_id ta8d07117 dla wszystkich. Produkcyjny log app_20261030.ndjson: action=reuse reason=latest_same_chat_domain (10:43:10), action=retain_finished reason=keep_until_limit_or_manual_close (10:43:20). Zlecenia tylko odczytowe, bez wysyłek.
- Włączenie nowego logowania jest potwierdzone wpisami tab_lifecycle na LIVE. Długoterminowe zachowanie po wielu godzinach i rzeczywiste zamknięcia przez limit można oceniać na podstawie kolejnych logów, bez wymuszania zamknięć w działającej sesji.
- Git commit/push/reset nie wykonywano.
