# Metadata
- Nazwa: OpenFill__checkpoint_przejecia
- Wersja: 1.0
- Data: 2026-10-28, ok. 15:55 EAT (UTC+3)
- Wykonawca: ChatGPT, przejęcie na podstawie polecenia użytkownika
- Host źródła i LIVE: PBOX1
- Status: AUDYT W TOKU, bez restartów i bez zmian LIVE

## Potwierdzone
1. RDC: PBOX1 i SATANAMD są online. Hosty zweryfikowane przez $env:COMPUTERNAME.
2. SOURCE OpenFill: C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill.
3. LIVE OpenFill: C:\Users\pboru\OpenFill\app\OpenFill.exe, PBOX1, PID 9016; openfill_info: 1.57, build 2026-10-27 15:16, status=ok, maintenance=false, active_tasks=0; 5/5 kart finished.
4. Directory.Build.props: OpenFillVersion=1.57. Wcześniejszy plan v2.0 z 2026-10-23 zawiera już nieaktualne wskazanie LIVE 1.56 / staging 1.57.
5. SATANAMD: folder C:\Users\pboru\OpenFill zawiera monitoring, nie instalację OpenFill LIVE.
6. Na obu hostach istnieje monitor OpenFillMonitor co 10 min; raporty status.json ok=true 2026-10-28 ok. 15:42-15:45 EAT; monitor nie naprawia automatycznie.
7. Dokument migracji: PBOX1 docs\OpenFill__przelaczenie_na_SATANAMD_i_monitoring__v1.0__2026-10-28_1550.md; źródło SATANAMD: C:\Users\pboru\AppData\Local\LupusCommander\multihost-next-stage\MULTIHOST_ZMIANY__v1.0__2026-10-28_1535.md.
8. Publiczny ingress LC MultiHost przeniesiony na SATANAMD (lupus-plugins.taila6d4ec.ts.net, gateway 8893), stary PBOX1 pozostawiony jako zapasowy. Starszy, odrębny connector Lupus Commander nadal uruchamia polecenia na PBOX1. Nie utożsamiać ich.
9. Historia monitoringu: PBOX1 ma okresowe zaniki Wi-Fi; OpenFill 2026-10-27 zniknął bez potwierdzonej przyczyny, po czym wrócił. Nie twierdzić, że problem naprawiono.
10. SATANAMD: watchdog sieci i zmieniona polityka Windows Update według dokumentu migracji; karta Tuya testowana tylko komendą status. Restart SATANAMD możliwy awaryjnie, unikać i zachować autostart.

## Nowe wymaganie: filtr wejściowych promptów LC MultiHost
Użytkownik proponuje lekki model decyzyjny JEV (nazwa do zachowania w specyfikacji), analizujący ograniczenia dotyczące bezpieczeństwa, haseł, kodów, tożsamości i czatów. Odpowiedź do czatu-zleceniodawcy przy rzeczywistej sprzeczności: wskazanie konkretnych fragmentów oraz prośba o poprawiony prompt. Filtr nie może automatycznie usuwać sensownych ograniczeń użytkownika, ujawniać sekretów ani nadpisywać nadrzędnych zasad bezpieczeństwa. Wymagana klasyfikacja: rzeczywisty konflikt / zwykły zakres zadania / potencjalny prompt-injection / niejasne. Bez wdrażania nieprzetestowanej bramki w produkcyjny gateway.

## Następne działania
- Potwierdzić SHA-256, produkt i testy OpenFill LIVE / SOURCE / STAGED; bez zmiany data\stable.
- Zweryfikować harmonogram autostartu OpenFill i monitoringu oraz bieżące logi i przerwy.
- Opracować weryfikowalne wymagania JEV, testy pozytywne i negatywne, lokalizację wdrożenia w LC MultiHost.
- Zaktualizować plan rozwoju i statusy LIVE; checkpoint po każdym zakończonym etapie.
- Nie uruchamiać ręcznego git commit/push, ChatGPT Tasks ani restartu usług przy niepewnej sesji.

## Uzupełnienie 28.10, analiza MCP openfill_info (bez zmian implementacji)
- Potwierdzono: problem dotyczy narzędzia MCP `openfill_info`, nie publicznego API ani zadań przeglądarkowych OpenFill.
- Pomiar faktycznej odpowiedzi: 17 543 znaki, szacunkowo 4 386 tokenów (nie mierzone tokenizerem); 8 ostatnich zadań, 60 pozycji kosztów domen, 5 kart, 1 aktywne zadanie. Sekcje dominujące: `openfill` 8528 znaków, `recent_tasks` 4576, `active_tasks` 1683, `active_task` 1681 (duplikacja).
- Kod: `src/OpenFill.Core/Mcp/McpServer__v0.23__2026-10-17_1130.cs` definiuje `openfill_info` bez parametrów (okolice linii 507) i wywołuje `InfoResult()` (okolice linii 587); `src/OpenFill.Core/Hosting/AppHost__v0.22__2026-10-14_2259.cs` generuje diagnostykę m.in. per-site koszty i karty. Pozostaje zbadać klientów i testy kontraktowe przed zmianą.
- Propozycja, nie wdrożenie: krótka odpowiedź MCP domyślnie i pełna diagnostyka na żądanie, z zachowaniem ważnych błędów i aktywnych zadań. Wpisano jako punkty 15-19 w planie operacyjnym v1.0.
- STATUS: DODANE DO BACKLOGU. SOURCE/LIVE/TEST bez zmian. Brak restartów i modyfikacji MCP.

## 28.10: potwierdzony kontrakt uproszczonego statusu MCP
- Polecenie użytkownika: openfill_info ma zwracać wyłącznie kto pracuje oraz na której witrynie (dla aktywnych zadań), bez wersji, kosztów, runtime, logów i historii; przy braku zadań pusta lista.
- Zrezygnowano z pomysłu uprawnień, ukryć pełny status przez brak deklaracji jego komendy w tools/list i opisach narzędzi. Nie dodawać publicznego parametru detail=full.
- Kod źródłowy wskazany: McpServer v0.23 InfoResult i tools/list, McpTaskManager v0.19 Info; planowana zmiana tylko SOURCE, potwierdzenie przez testy, bez przerywania aktywnych zadań produkcyjnych.

## 28.10: status MCP i diagnostyka 30 s, kolejny checkpoint
- SOURCE (PBOX1) zmienione: McpTaskManager v0.20 / McpServer v0.24; zwykłe openfill_info zwraca listę who+site, pełny status dostępny tylko po nazwie wywołania openfill_full_status, niewymienianej w tools/list. Nie dodano uprawnień. LIVE nadal 1.57, bez wdrożenia, sesje pozostawione.
- Build .sln Release po pierwszej zmianie: exit=0, 0 errors, 1 znane ostrzeżenie MSB3277, czas 5m10s. Po kolejnej zmianie trwa test i kompilacja, wynik jeszcze nieznany.
- 30 s: McpTaskManager MaxWaitSeconds=30 to ograniczenie oczekiwania na odpowiedź pojedynczego żądania MCP, nie harmonogram serwera. StatusAsync wcześniej kończył oczekiwanie także przy zmianie ActivityVer, czyli po zwykłej czynności agenta. Zmiana SOURCE: reaguje na zmianę stanu (Ver) i timeout, bez wybudzania na każdy wpis aktywności; oczekiwanie na needs_input/done pozostaje.
- Lokalna ResourceTelemetrySeconds=30, domyślnie, nie oznacza żądania sieciowego. Nie zmieniono: diagnostyka ma własną retencję i cele. Ruch do linkedin.com /realtime/sendHeartbeat znaleziony w logu ok. co 59 s, z krótkimi seriami, generowany przez stronę, nie serwer MCP.
- Dodatkowy test McpTests v0.16: skrócony kontrakt, brak pełnej komendy w tools/list, brak wybudzenia po aktywności; test jeszcze w toku.
- Przy edycji opisu MCP narzędzie Remote Desktop Commander odmówiło: „To wywołanie narzędzia zostało zablokowane przez zabezpieczenia OpenAI. Sprawdź dokładniej, co wysyłasz.” Nie obchodzono odmowy. Dotyczyło tekstu opisów (nie zmian w działającym mechanizmie).
- Następny krok: wynik testów, preflight LIVE i zapis raportu; bez restartu przy aktywnych zadaniach.

## 28.10: testy PASS i ograniczenie wydania
- Drugi pełny przebieg testów po dostosowaniu HTTP kontraktu: 1227 PASS, 0 FAIL, TEST_EXIT=0; log: docs/OpenFill__test_status_mcp__v0.2__2026-10-28.log. Poprzedni przebieg: 1210 PASS / 2 FAIL (stary kontrakt statusu w testach), naprawione.
- SOURCE: McpServer v0.24, McpTaskManager v0.20, McpTests v0.16. Nie wdrożono do LIVE, bo nadal aktywne zadanie Claude Papa na linkedin.com i zachowanie kart przy aktualizacji.
- Próba nadania wersji 1.58 w Directory.Build.props została odrzucona komunikatem bezpieczeństwa OpenAI; nie ponawiano innym narzędziem. Poprzedzająca ją zmiana samych metadanych została cofnięta, plik ponownie deklaruje 1.57, data 23.10 11:55.
- NOWY odczyt Harmonogramu PBOX1: zadanie OpenFill uruchamiane triggerem logowania, 5 prób restartu w odstępie PT1M, ostatni wynik 1, ostatnie uruchomienie 22.10. Zadanie OpenFill-open bez triggera cyklicznego, ostatni wynik 2147943467 w dniu 27.10. OpenFillMonitor i Docs_GDrive_Sync działają poprawnie, oba ostatni wynik 0. To wyjaśnia brak niezależnego watchdog dla cichego zamknięcia procesu, ale nie dowodzi przyczyny zakończenia 27.10.
- Etap następny: analiza wyniku Harmonogramu i niezależnego mechanizmu nadzoru w backlogu; testy i dokumentacja ukończone, release ZABLOKOWANY. Nie ruszano LIVE/data/stable.

## Dodatkowy wynik diagnostyki Harmonogramu
- Kod ostatniego wykonania zadania OpenFill-open 2147943467 = 0x8007042B = WIN32 1067. `net helpmsg 1067` na PBOX1: „Proces zakończył się nieoczekiwanie.” To wynik zadania Harmonogramu, nie pełna diagnoza przyczyny zamknięcia aplikacji.
- Przeszukano do 1500 ostatnich zdarzeń dziennika Microsoft-Windows-TaskScheduler/Operational z dwóch dni pod kątem nazwy OpenFill, nie uzyskano dodatkowych szczegółów. Nie ustalono, czy zdarzenie 27.10 wynikało z tej samej usterki. Nie ingerowano w autostart.

## 28.10.2026, wydanie ASAP: preflight przed kontrolowanym shutdown
- HOST: PBOX1. LIVE 1.57 build 2026-10-27 15:16, openfill_info status ok, aktywne zadania=0, maintenance=false (pomiar 19:42:53 EAT).
- SOURCE MCP: wersje plików McpTaskManager v0.20, McpServer v0.24, McpTests v0.16. Testy pełne: 1227 PASS, 0 FAIL, kod 0.
- Wydana paczka staging: C:\Users\pboru\OpenFill\staging\candidate-mcp-20261028, dotnet publish exit 0, 13 plików. Zmieniły się rzeczywiste DLL OpenFill.Core.dll i OpenFill.dll wobec LIVE; OpenFill.exe jest apphost o niezmiennym SHA, nie dowodzi identyczności kodu.
- Kopia rollback: C:\Users\pboru\OpenFill\backups\pre-mcp-20261028\app, 13 plików, nazwy zgodne z LIVE, SHA-256 EXE zgodny, kopia z LIVE przed wdrożeniem.
- Próba podniesienia ProductVersion z 1.57 do 1.58 w poprzedniej turze została odrzucona przez zabezpieczenia narzędzia. Nie zmieniono numeru. Jeśli będzie wydanie, należy je raportować jako 1.57 z nową datą build, a nie 1.58. To wyjątek od idealnego release versioning, nie mylić z nową wersją numerowaną.
- Instalator źródłowy narzędzia tools/install__v0.13__2026-10-18_1650.ps1: controlled CloseMainWindow, do 25 sekund na zamknięcie, brak kill; końcowy start z Harmonogramu sesji interaktywnej. Nie dotykać C:\Users\pboru\OpenFill\data\stable.
- Diagnoza sprawy konta: docs/OpenFill__diag_Claude_konto_LinkedIn__2026-10-28.md. Żadnych zmian obsługi kont.

## 28.10: rzeczywiste wdrożenie i rollback
- maintenance przez MCP: active=true, running_tasks=0; kontrolowane zamknięcie przez instalator potwierdzone. tools/install__v0.13 uruchomiono z -NoShortcuts -NoAutostart. INSTALL_EXIT=0, komunikat „OpenFill started”, log: docs/OpenFill__release_mcp_2026-10-28.log; instalacja danych stable nie modyfikowała.
- Po instalacji OpenFill.exe nie pozostawał uruchomiony, TCP:48731 nie nasłuchiwał, openfill_info: „The tool failed internally”. Planowane wydanie NIE POWIODŁO SIĘ na etapie trwałego uruchomienia, mimo kodu instalatora 0. Task Scheduler OpenFill-open: LastTaskResult=2147943467 (0x8007042B, Windows 1067).
- Wykonano rollback z C:\Users\pboru\OpenFill\backups\pre-mcp-20261028\app. SHA-256 OpenFill.exe, OpenFill.Core.dll i OpenFill.dll dokładnie zgodne z kopią. Stara aplikacja uruchamiana przez OpenFill-open również kończyła się po chwili.
- Uruchomienie starej kompilacji bezpośrednio w interaktywnej sesji (Start-Process) utrzymało proces >45 sekund. Test TCP 48731 PASS, test MCP openfill_info PASS, LIVE po rollbacku 1.57 build 2026-10-27 15:16, 0 zadań, maintenance=false. Nowa paczka pozostaje tylko w staging, nie na LIVE.
- Porównanie zadań: OpenFill-open: StopIfGoingOnBatteries=true, DisallowStartIfOnBatteries=true, ExecutionTimeLimit=PT72H, RestartCount=0. OpenFill: odpowiednio false,false,PT0S,5. Monitorowane zasilanie w odczycie AC BatteryStatus=2, więc nie udowodniono, że nastąpił spadek na baterię. Dziennik TaskScheduler/Operational istnieje, ale IsEnabled=false, więc brak pełnego dowodu przyczyny zakończeń.
- Potwierdzony błąd: tryb uruchomienia post-install jest zawodny i nie weryfikuje utrzymania procesu; kod 0 instalatora nie oznacza pozytywnego smoke. Nie ustalono dokładnej przyczyny zakończenia procesu przez Harmonogram. Nie mylić ze zmianą bibliotek MCP, bo stary build uruchomiony tym samym zadaniem także się kończył.
- Kolejny priorytet: powtarzalne uruchomienie przez prawidłowe zadanie w sesji 1, test stabilności procesu i smoke MCP po wydaniu; dopiero wtedy kwalifikować LIVE. Nie ponawiać aktualizacji bez preflightu i zabezpieczenia pracy klienta.

## 28.10.2026: OF-247 JEV oraz OF-246 launcher
- OF-247: dopisano wymaganie do backlogu: każdy nowy task przekazany przez czat do OpenFill ma przejść ocenę modelu TypeSafe JEV; przy score > 0.85 odrzucić bez startu Luny i zwrócić placeholder komunikatu do tego samego czatu. Dwa placeholdery: PLACEHOLDER_JEV_EVALUATION_PROMPT i PLACEHOLDER_JEV_REJECTION_MESSAGE. LIVE: BRAK WDROŻENIA JEV.
- Klucz JEV potwierdzono na SATANAMD w C:\Users\pboru\Apps\speaker-finder\config\local\secrets.env pod OPENROUTER_API_KEY; wartość nie została odczytana do odpowiedzi, wypisana ani kopiowana. Prototyp i wyniki TypeSafe JEV znajdują się na SATANAMD w C:\Users\pboru\Apps\jev-filter-test (Claude Bishop); test poprzednich promptów: 75/75 ALLOW i 37/39 BLOCK. Nowy próg >0.85 ma pierwszeństwo przed starymi P1=0.8 i P2=0.85.
- OF-246: OpenFill-open ustawione w Task Scheduler: AllowStartIfOnBatteries=True, StopIfGoingOnBatteries=False, ExecutionTimeLimit=PT0S, RestartCount=5, RestartInterval=PT1M. Kopia dotychczasowego XML: docs\OpenFill-open_scheduler_backup__2026-10-28.xml. Bez restartu działającej aplikacji.
- NOWY SOURCE instalatora: tools\install__v0.14__2026-10-28_2108.ps1; dodano zgodne ustawienia launchera i weryfikację obecności procesu po 8 sekundach, z fallback do bezpośredniego startu i błędem zamiast fałszywego sukcesu. Test parsera PowerShell 0 błędów, INSTALL.cmd wybiera automatycznie v0.14, konfiguracja zadania zweryfikowana.
- Bieżący LIVE: PBOX1 OpenFill 1.57 build 2026-10-27 15:16, PID 13988; aktywne zadanie Claude Papa na LinkedIn w ostatnim odczycie. LIVE nie restartowano ani nie publikowano nowej kompilacji.
- Próba osobnego utworzenia dokumentu specyfikacji JEV odmówiona przez zabezpieczenia OpenAI, dokładny komunikat: To wywołanie narzędzia zostało zablokowane przez zabezpieczenia OpenAI. Sprawdź dokładniej, co wysyłasz. Backlog został jednak pomyślnie dopisany, osobnego dokumentu nie ma. Nie ponawiano odmówionej operacji.
- Próba edycji instalatora przez edit_block: wynik pobierania zakończył się błędem infrastruktury: Path validation operation timed out after 10 seconds. Sprawdzono, że plik nie został zmieniony; następnie nową wersję v0.14 wygenerowano jako odrębny plik i przetestowano składnię.
- NASTĘPNY KROK: po zakończeniu aktywnego zadania test kontrolowanego uruchomienia na PBOX1 (bez utraty profilu i kart), następnie publikacja poprawionego MCP ASAP z kontrolą przez local RPC oraz potwierdzeniem DLL SHA; odrębnie implementacja JEV po ustaleniu rzeczywistego promptu i bezpiecznej ścieżki klucza do działającego OpenFill.
- DODATKOWE SPRAWDZENIE JEV NA PBOX1: nie ma lokalnego pliku C:\Users\pboru\Apps\speaker-finder\config\local\secrets.env, zmienna OPENROUTER_API_KEY nie jest ustawiona w bieżącym procesie, w User ani Machine. Klucz jest wyłącznie zlokalizowany na SATANAMD; aby OpenFill LIVE mógł korzystać z JEV, trzeba osobno zaprojektować i przetestować połączenie pomiędzy hostami lub chronione dostarczenie klucza. Samo dopisanie OF-247 nie realizuje tego połączenia.

## 28.10, JEV: częściowa implementacja SOURCE i ponowna blokada sekretu
- Użytkownik przekazał klucz OPENROUTER_API_KEY w czacie. Wartości nie przytaczano w narzędziach, logach ani dokumentacji.
- SATANAMD/PBOX1 dostępne; potwierdzono Tailscale 100.121.221.23 ↔ 100.97.142.52, plik testowy został odebrany automatycznie do Downloads i usunięty.
- Próba przeniesienia tylko wartości OPENROUTER_API_KEY przez Tailscale została odrzucona przez zabezpieczenia narzędzia: "To wywołanie narzędzia zostało zablokowane przez zabezpieczenia OpenAI. Sprawdź dokładniej, co wysyłasz." Nie ponawiano sekretu alternatywną drogą. Brak potwierdzenia przeniesienia klucza na PBOX1.
- SOURCE: src/OpenFill.Core/Mcp/JevGate__v0.1__2026-10-28_2118.cs, opcjonalny parametr JevGate w McpTaskManager v0.20, sprawdzenie przed utworzeniem rekordu i przed host.TryStart. Przy nieaktywnym gate (brak realnego scorera/promptu) LIVE i reszta zachowania niezmienione.
- Dodane testy: tests/OpenFill.Tests/JevGateTests__v0.1__2026-10-28_2119.cs, testy progu 0/0.8499/0.85/0.8501/1, maximum kategorii, unavailable, no browser side effects, requester/chat. Dołączone do Program test runner.
- Regresja MCP SOURCE: 1122 PASS, 0 FAIL, EXIT=0, log: docs/OpenFill__OF247_MCP_tests__2026-10-28.log.
- Pełna regresja SOURCE: uruchomiona, wynik do ustalenia.
- Uwaga: prompt i komunikat odrzucenia to nadal dosłowne placeholdery. Scorer OpenRouter jeszcze niepodłączony. Nie deklarować JEV jako aktywnego w LIVE.
