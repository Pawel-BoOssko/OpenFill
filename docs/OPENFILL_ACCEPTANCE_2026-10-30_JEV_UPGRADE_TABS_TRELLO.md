# OpenFill — odbiór JEV direct, aktywnego upgrade, kart i Trello
Data: 2026-10-30 EAT. Host SOURCE i LIVE: PBOX1. Networking Radar poza zakresem.

## 1. JEV DIRECT — DONE
- Stary skrypt tools/jev_direct_mcp_acceptance__v0.1__2026-10-29.ps1 nie uruchamiał się: ParserError/UnexpectedToken w linii 13 (zepsute kodowanie polskich znaków). Nowy skrypt v0.2 ASCII tools/jev_direct_mcp_acceptance__v0.2__2026-10-30.ps1: EXIT0, inbound dangerous score 1.0 -> rejected_rewrite_needed bez task_id, benign -> running z task_id OF-20261030-0832-71c9; PASS.
- Bezpośredni test rzeczywistych obu modeli TypeSafe przez HTTPS OpenRouter z lokalnym kluczem DPAPI (bez drukowania sekretu): tools/jev-direct-acceptance-20261030/ (csproj + Program.cs), log docs/OpenFill_JEV_direct_both_acceptance_20261030.log. INBOUND_RISK_P=1.000, INBOUND_BENIGN_P=0.000, OUTBOUND_RISK_P=1.000, OUTBOUND_BENIGN_P=0.000. OUTBOUND_WARNING=True, OUTBOUND_ORIGINAL_PRESERVED=True. DIRECT_API_TESTED=True, JEV_BOTH_CLASSIFIERS=PASS, PROBE_EXIT=0.
- W trakcie rzeczywistego upgrade wykryto fałszywe injection_warning na zaufanym komunikacie OpenFill o statusie interrupted. Poprawka w SOURCE src/OpenFill.Core/Mcp/JevOutputGuard__v0.2__2026-10-29_1415.cs: pomijany scoring tylko przy status interrupted + summary zaczynające się od stałego McpTaskManager.UpgradeNotice. Inny zewnętrzny tekst przy interrupted nadal badany. Dodano 4 testy do JevGateTests v0.4. Po wdrożeniu ponowny odczyt starego interrupted przez produkcyjne MCP bez injection_warning. Backup SOURCE backups/jev-maintenance-warning-20261030.

## 2. Real active upgrade — DONE dla running + restore drill
- LIVE backup 23 pliki C:\Users\pboru\OpenFill\backups\pre-active-upgrade-20261030, 0 różnic SHA-256. Testowy lokalny HTTP fixture uruchomiony na 127.0.0.1:60177 (opóźnienie 90 s); zadanie OF-20261030-0837-ee72 status running podczas wywołania instalatora. Instalator v0.14 natychmiast zażądał controlled shutdown, nie czekał na zakończenie zadania, exit0; log C:\Users\pboru\AppData\Local\Temp\openfill_install_20261030_083740.log.
- Po restarcie to samo task_id wraca z status interrupted, outcome partial, trwałym komunikatem 'Teraz nastąpi aktualizacja. Wróć do nas za kilka minut.', continue_task_id=OF-20261030-0837-ee72, informacją o niepewnych skutkach i bez ponawiania działania. LIVE_SMOKE=PASS, MCP OK.
- Osobny test odtworzenia kopii plików do staging: C:\Users\pboru\OpenFill\staging\rollback-restore-drill-20261030; 23 pliki, 0 różnic SHA-256, RESTORE_DRILL=PASS. NIE wykonano faktycznego rollbacku w LIVE, więc nie deklarować pełnego testu przełączenia wstecz. Fixture procesu testowego zatrzymana.
- Automatyczne testy MCP obejmują wielu równoległych klientów oraz running/needs_input, aktywnych long-poll, odmowę nowych zadań w maintenance, trwałość interrupted i SaveStrict failures. Żywy test dotyczył jednego running, nie testował osobno prawdziwego waiting_for_user ani needs_input.

## 3. Tab reuse — DONE w real LIVE w tym samym czacie/domenie
- Reprodukcja przed poprawką: zadanie OF-20261030-0840-2e07 -> tab tc2ec57c8; zadanie OF-20261030-0841-0f72 -> tab t7b43d6b1, reason site_domain_unknown, mimo tego samego czatu i domeny. Przyczyna: AppHost.InitialTaskDomain nie rozpoznawał polecenia zaczynającego się od 'Na https://...'.
- SOURCE AppHost__v0.23__2026-10-29_0807.cs: obsługa fraz początkowych 'Na/On/At https://...'; TabTests v0.9: 3 nowe asercje, nie łapać URL tylko wspomnianych w treści wiadomości. SOURCE backup backups/tab-domain-prefix-20261030.
- Tab-only 186 PASS/0 FAIL. Końcowa pełna regresja SOURCE: 1359 PASS/0 FAIL, TEST_EXIT=0, docs/OpenFill_JEV_tabs_full_regression_20261030.log.
- LIVE test po wdrożeniu: OF-20261030-0847-de32 tab t9482b104, reason previous_tab_closed_reopened_saved_url po restarcie; OF-20261030-0848-1500 tab t9482b104, reason existing_session_reused; oba done/success, bez ponownego otwierania fizycznej karty. Długotrwały test 1–3 h/overnight nie był wykonywany.

## 4. Final LIVE deployment — DONE
- Backup LIVE: C:\Users\pboru\OpenFill\backups\pre-jev-tab-fixes-20261030, 23 pliki, SHA mismatch 0.
- Standardowy instalator v0.14 exit0, log C:\Users\pboru\AppData\Local\Temp\openfill_install_20261030_084645.log, OpenFill.exe PID 17576 start 08:46:59 EAT, MCP OK, ACTIVE_TASK_COUNT=0, JEV local key present, JEV API reachable. release_smoke v0.3: exit0, FAILED_CHECK_COUNT=0, LIVE_SMOKE=PASS. GET /api/alpha/decisions HTTP404 tylko transport, rzeczywista klasyfikacja wykazana osobnym testem DIRECT.
- Nie wykonywano git commit/push/reset. Networking Radar nietknięty.

## 5. OF-090 real Trello (bez Networking Radar) — DONE dla wskazanego przykładu
- Karta Lupus Consulting https://trello.com/c/ZKS9zb2P. Początkowe zadanie OF-20261030-0849-84a5: DOM i obraz zgodne, widok prośby o rejestrację zamiast karty; 5 HTTP401 i 7 HTTP400, 0 iframe w bramce.
- Kontynuacja OF-20261030-0851-93b9 na tej samej karcie t80397f95; po wykorzystaniu istniejącego uwierzytelnienia i logowania przez SSO dostęp przywrócony. Status done/success, karta i jej tytuł widoczne w DOM, 22 przyciski + textarea + 4 linki (27 elementów), 1 iframe, HTTP200 na dokumencie i danych karty, brak 4xx. Bez modyfikowania kart i bez poufnych treści w raporcie.
- Nie jest to gwarancja działania dowolnej strony i każdego iframe, tylko rzeczywisty odbiór jednej karty.

## 6. Dalsze otwarte sprawy
- Autoryzowane rzeczywiste zaproszenia LinkedIn (OF-233) wymagają konkretnych odbiorców; brak takiej listy w tym zleceniu. Mechanizm zliczania 5/5 był wcześniej potwierdzony na realnej Lunie i lokalnej stronie testowej.
- Specjalny test 1–3 h / następny dzień dla zachowania kart pozostaje nieprzeprowadzony; bez ponawiania już zaliczonych krótkich testów.
- Realny test aktywnego upgrade dla osobnych procesów needs_input i waiting_for_user oraz rzeczywistego przełączenia LIVE na rollback (bez naruszania działającego użytkownika) pozostaje do odbioru, chociaż automatyczne testy pokrywają te stany.
- Test dostarczenia WhatsApp do odrębnego odbiorcy nie był wymagany ani przeprowadzony; wysyłka do własnego konta potwierdzona w poprzednim checkpointcie.
