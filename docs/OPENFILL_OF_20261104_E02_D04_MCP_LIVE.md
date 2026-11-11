# OpenFill, 2026-11-04: E02 learning, D04 read-only, MCP stop-origin
## Zakres zrealizowany
1. E02: ProcedureLearningStore, trwałe rekordy per procedure id+version, idempotencja per batch ID, atomowy zapis, candidate -> verified po 2 różnych wsadach, z których każdy ma dowody przy każdej pozycji. Po 3 różnych w pełni nieudanych wsadach status suspended. Niepełne i uncertain nie podnoszą zaufania. Suspended nie wraca automatycznie do verified. Dane składowane: liczniki, hash dowodów, bez tekstów wiadomości/DOM.
2. ProcedureCatalog.FromEmbedded korzysta z istniejącego skompilowanego rejestru, status może być odczytany z procedure-learning. Nie instaluje nowego wykonywalnego kodu z danych. WhatsApp i LinkedIn używają katalogu z trwałym stanem. ProcedureExecutor zapisuje status uczenia po batch, ale błąd dodatkowej telemetrii uczenia nie nadpisuje faktycznego wyniku zewnętrznej operacji.
3. D04: DomainReadAuditService z lekkim read-only przeglądem YouTube Studio oraz Meta Business. Jawny provider, intencja, identyfikator oczekiwanego zasobu. Walidacja rzeczywistej domeny HTTPS, route i identyfikatora w URL; odrzucone obce domeny, sprzeczne ID. Zwraca same nagłówki, nazwy kontrolek, podpis widoku, widzialny zakres. Nie eksportuje wartości textboxów. URL match nie dowodzi tożsamości zalogowanego użytkownika. Żadnej publikacji ani zapisu.
4. LIVE wewnętrzne narzędzie Luny google_meta_view_audit, 4 wpisy candidate w procedures/registry.json. Intencje oparto na E01 z OPENFILL_INTENTS_ACTIONABLE_20261101.md: YouTube Studio audit_channel_readonly/upload_schedule_short, Meta Business audit_page_readonly/reel_editor_workflow. Dwie intencje publikacyjne to tylko preflight, nie adapter wykonywania skutków.
5. MCP regression: FakeRunHost ma odrębne kanały StopFromRequester/StopForMaintenance. Test sprawdza requester_cancel/requesting_model i upgrade_interrupted oraz trwałość i powiadamianie. Zaktualizowano wcześniejsze założenie liczby MCP tools: >=9 z wymaganymi nazwami, bez ograniczania rozszerzeń.

## Testy
- E02 fixture: E02_PROCEDURE_LEARNING_10_CHECKS_OK.
- D04 fixture: D04_DOMAIN_READ_16_CHECKS_OK.
- D01 WhatsApp batch: OF025_WHATSAPP_BATCH_19_CHECKS_OK.
- D02 LinkedIn batch: D02_LINKEDIN_BATCH_21_CHECKS_OK.
- LocalPlan narrow autorouting: LOCALPLAN_AUTO_ROUTING_16_CHECKS_OK.
- MCP łącznie: Passed 212, Failed 0. Początkowy FAIL 211/1: test przestarzałego stałego count==9 tools, naprawiono na obecność wymaganych names; ponowienie 212/0.
- Suma zaliczonych kontrolek 293, lecz kilka zestawów są symulatorami i nie stanowią rzeczywistej wysyłki.

## Wydanie
- Publish staging/of_final_e02_d04_20261104: exit 0, tylko starsze ostrzeżenia kompilacji.
- DEV staging/of_imp_20261031, PID 21936, PASS.
- LIVE: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True bez czekania na idle, exit 0.
- Rollback: C:/Users/pboru/OpenFill/app__rollback__before_release_20261104_200502.
- Niezależnie: LIVE PID 3756, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, OpenFill-open Running, MCP openfill_info working=[].
- Core SHA256: C7632AF5A4CE7A691C0CDD827A298DEEE71C8CFEEC6E6F3F4E41BF1DC46223B1.

## Odbiór i rzeczywiste ograniczenia
- Realne D01/D02 batch sends: bez zatwierdzonych odbiorców, kont i dokładnych tekstów nie wykonano skutkowych wysyłek i nie można oznaczyć ich verified. Testy techniczne PASS.
- YouTube/Meta publikacja: adaptery publikujące nie istnieją, nie dostarczono mediów, kont i harmonogramów; obecne D04 jest read-only preflight candidate.
- E02 ma techniczny cykl i testy, ale bez rzeczywistych wsadów nie ma jeszcze produkcyjnych przesłanek do promocji candidate.
- Cache rzeczywisty hit w WebView2, UX Enter w naturalnej karcie ask, GUI Stop użytkownika: brak zarejestrowanego naturalnego acceptance w tym wydaniu, nie oznaczać DONE.
- LocalPlan single-filter autoroute LIVE z poprzedniego wydania, E2E na lokalnym CRM i jedno A/B potwierdzone historycznie; brak podstaw do automatyzacji skutkowych klas.
- Airbnb D03 anulowane, nie uruchamiać. OpenFill wake na SATANAMD wyłączone decyzją użytkownika, nie ruszać. GitHub autosync nie jest zatwierdzone.
