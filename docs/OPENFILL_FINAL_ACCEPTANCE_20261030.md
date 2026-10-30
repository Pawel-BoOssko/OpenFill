# OpenFill: wykonanie czterech ustalonych obszarów, 2026-10-30

Host: PBOX1. Dotyczy tylko OpenFill. Networking Radar poza zakresem.

## 1. Trwałość i zamykanie kart
- OpenFill LIVE: proces PID 21012, start 10:41:55 EAT. Nie restartowany podczas tego cyklu.
- Karta WhatsApp ta8d07117 zachowana 10:43:56, zamknięta 12:34:54, po 1 h 50 min 58 s, reason=capacity_limit_5.
- Wszystkie 9 wpisów action=close w app_20261030.ndjson miało reason=capacity_limit_5. Brak potwierdzonej czasowej polityki zamykania w LIVE.
- Full RPC openfill_full_status potwierdził 5 fizycznych kart, limit 5, rozdział finished/running, requester/chat pozostają odizolowane.
- Długie zachowanie karty bez presji limitu nie zostało testowane w osobnym kilkugodzinnym, nieprzerywanym scenariuszu. Nie nazywać pełnym PASS tego specyficznego wariantu.

## 2. Actual upgrade while task is waiting
- SOURCE McpTests rozszerzone o równoczesne stany: running (2), needs_input (1), waiting_for_user (1).
- Przygotowanie shutdown oznacza wszystkie cztery jako interrupted, przerywa oczekiwanie, zapisuje partial i continue_task_id, bez zgubienia stanu po ponownym utworzeniu McpTaskManager.
- Dodatkowy test realnego HTTP MCP na localhost: cztery zadania, trzech klientów odczytujących status (w tym waiting_for_user), oznaczenie interrupted, utrzymanie stanu po ponownym odczycie z dysku.
- Pierwsza regresja nowego testu: 1376 PASS, 2 FAIL z powodu użycia StartAsync wait=0, który poprawnie zwraca stan running przed transition. Poprawiono dane testu na wait=5 s.
- Kolejna regresja: 1378 PASS, 0 FAIL.
- Końcowa pełna regresja SOURCE po rozszerzeniu testu HTTP: **1383 PASS, 0 FAIL, EXIT 0**.
- Log: docs/OpenFill_waitstates_HTTP_full_regression_20261030.log.
- Realne przerwanie oczekującego użytkownika w LIVE poprzez reinstalację nie było wykonywane, ponieważ nie wdrażano nowej biblioteki produkcyjnej. Test potwierdza zachowanie rzeczywistego MCP HTTP z symulowanym wykonawcą, nie interakcję człowieka z LIVE.

## 3. Stabilność CPU i pamięci
- 372+ próbek z procesu PID21012, ponad 3 godziny uptime, od 07:42 UTC.
- W okresie z 5 fizycznymi kartami i 0 aktywnych zadań, 93 próbki od 09:54:24 do 10:40:34 UTC (~46 min):
  * Working Set: pierwsze 20 średnio 865.08 MB, ostatnie 20 średnio 769.30 MB.
  * Private Bytes: 1451.26 -> 1469.41 MB (wzrost 18.15 MB, 1.25%).
  * CPU całej 4-logicznej maszyny: 8.26% -> 8.48%.
  * Procesy WebView2: 19 -> 19.
- Nie ma dowodu niekontrolowanego wycieku w tym oknie. Znaczny koszt CPU w bezczynności przy 5 kartach pozostaje obserwacją wydajnościową, nie diagnozą konkretnej usterki.
- Porównanie próbek z 1 i 5 kart bez kontroli domeny i obciążenia byłoby niepoprawne.

## 4. Wi-Fi, reconnect i monitor
- Dziennik WLAN-AutoConfig/Operational: 11:27:05 EAT event 8003, rozłączenie sieci ZIWA przez sterownik Intel Dual Band Wireless-AC 8265. O 11:27:11 event 8001, ponowne połączenie. Przerwa ~6 s.
- System Netwtw06: event 7003 Roam Complete o 11:24:47 i 11:26:01 oraz liczne eventy 6000 BSS missed beacons od 11:25:35 do 11:26:55. To wskazuje na utratę ramek beacon i roaming przed rozłączeniem; dokładna przyczyna radiowa/AP/sterownika pozostaje nieustalona.
- Poprzedni OpenFillMonitor v0.4 błędnie zaliczał event 11004 (przerwanie ustanawiania zabezpieczeń) jako rozłączenie obok eventów 8002/8003. Licznik wifi_flaps i alarmy były zawyżane.
- SOURCE/LIVE: OpenFill_monitor__v0.5__2026-10-30_1345.ps1 wylicza wyłącznie 8002/8003, testowy katalog test-v05, parser PowerShell 0 błędów. SOURCE i LIVE SHA256 zgodne.
- Izolowany TestMode: MCP true, wifi flaps 0, brak alarmu. Rzeczywisty LIVE monitor: MCP true, JEV reachable true, flaps 0, problems empty; event problem_end wifi_flaps zapisany.
- Scheduled Task OpenFillMonitor nadal działa co ~10 minut, launcher run_monitor_hidden.vbs wskazuje v0.5, LastTaskResult=0. Backup launchera: monitor/run_monitor_hidden__backup_before_v05_20261030.vbs.
- Bez restartu OpenFill, bez modyfikacji Tailscale/routera/sterownika. Po incydencie proces pozostał stabilny i zadania OpenFill ponownie kończyły się sukcesem.

## Post-release smoke
- OpenFill PID21012, MCP_STATUS_OK=True, ACTIVE_TASK_COUNT=0, JEV_LOCAL_DPAPI_KEY_PRESENT=True, JEV_DIRECT_API_TRANSPORT_REACHABLE=True, LAUNCH_TASK=Running, FAILED_CHECK_COUNT=0, LIVE_SMOKE=PASS, SMOKE_EXIT=0.
- HTTP GET 404 z endpointu API JEV jest testem osiągalności transportowej; ocena modelowa była osobno potwierdzona wcześniej.

## Stan i dalsze działania
- Zrobione: analiza zamknięć kart, źródłowa i HTTP-MCP regresja czterech stanów aktualizacji, pomiar 3 h zasobów, rozpoznanie przyczyny radiowej, poprawka monitora v0.5 i LIVE smoke.
- Nie w pełni odebrane w LIVE: fizyczna karta bez presji limitu po wielu godzinach; upgrade w prawdziwym waiting_for_user z realną interakcją człowieka.
- Nie ruszano produkcyjnej biblioteki OpenFill. Bez git commit/push/reset. Networking Radar nietknięty.

## 5. Próba rzeczywistej izolowanej instancji LIVE
- Zidentyfikowano i zweryfikowano obsługę `--instance qa_upgrade_20261030` w Program.cs, oddzielny root/profil/konfigurację/Mutex.
- Uruchomiono dodatkowy OpenFill.exe w interaktywnej sesji 1, PID 28256. QA port 48742 LISTEN, osobny mcp-secret.txt istniał, mcpTunnel=false; produkcyjny OpenFill PID 21012 pozostał uruchomiony.
- Próba wykonania lokalnego QA MCP RPC przez zdalne narzędzie została zablokowana jeszcze na etapie wywołania narzędzia: `This tool call was blocked by OpenAI's safety checks. Please double check what you are sending.`
- Brak odpowiedzi z QA MCP, a więc NIE uruchomiono zadania `waiting_for_user` na rzeczywistym QA OpenFill. Źródło odmowy jest w warstwie narzędzia; nie ustalono, która część żądania ją spowodowała. Nie zgadywać awarii OpenFill MCP.
- Wykonano kontrolowane `CloseMainWindow()` QA: zwróciło True; po 5 s PID 28256 nie istniał. Tymczasowy Python HTTP fixture PID 6380 zatrzymany. Katalog `data/qa_upgrade_20261030` usunięty po weryfikacji PID, bez naruszania stable; potwierdzono LIVE PID21012 nadal działa.
- Wniosek: węższy test integracyjny REAL HTTP MCP z symulowanym wykonawcą PASSED (1383/0), natomiast UI LIVE human-wait pozostaje otwarte z powodu konkretnej odmowy narzędzia. Nie należy oznaczać tego przypadku DONE.

## 6. Dodatkowy dowód utrzymania karty po ponad godzinie
- Karta `te5ae7409` zakończona 12:53:20 EAT (09:53:20Z) została zamknięta 14:01:29 EAT (11:01:29Z) z reason=capacity_limit_5, a więc po 68 min 9 s.
- Wraz z kartą `ta8d07117` (~111 min) daje to dwa niezależne przykłady retencji ponad 1 h do chwili presji limitu; żaden nie potwierdza arbitralnego timeoutu.
- Nadal brak testu wielogodzinnego w warunkach bez presji limitu 5 kart.
