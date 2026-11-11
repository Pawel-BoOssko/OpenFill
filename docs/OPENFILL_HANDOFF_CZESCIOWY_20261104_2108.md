# OPENFILL: KOMPLETNY HANDOFF PROJEKTU
Stan: 2026-11-04, ok. 21:10 CET (Warszawa). Host wykonawczy: PBOX1.
Przeznaczenie: przejęcie projektu bez odtwarzania poprzednich czatów i bez powtarzania zakończonych zmian. Rozdzielono fakty, testy techniczne i niepotwierdzone działania zewnętrzne.

## 1. Źródła prawdy i pierwszeństwo
- Repo SOURCE: C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill
- LIVE: C:\Users\pboru\OpenFill\app\OpenFill.exe
- Aktualny checkpoint: docs\OPENFILL_OF_IMP_20261031_EXECUTION_CHECKPOINT.md. Dokument narasta chronologicznie, najnowsze wpisy mają pierwszeństwo.
- Plan scalony: docs\OPENFILL_SCALONY_PLAN_WDROZENIA_20261031.md. Historyczne sekcje STATUS nie są bieżącym backlogiem.
- Raport najnowszego wydania: docs\OPENFILL_OF_20261104_E02_D04_MCP_LIVE.md
- Poprzednie wydanie: docs\OPENFILL_OF_20261104_1717_DELIVERY.md
- Audyt: docs\OPENFILL_EFFICIENCY_LIVE_20261104.md
- Stop i kody z poczty: docs\OPENFILL_STOP_REASON_AUTHMAIL_LIVE_20261104.md oraz docs\OPENFILL_TODO_STOP_REASON_AUTH_MAIL_20261104.md
- LocalPlan: docs\OPENFILL_LOCALPLAN_PRODUCTION_OPTIN_LIVE_20261104.md
- E01 intencje: docs\OPENFILL_INTENTS_ACTIONABLE_20261101.md i .json; docs\OPENFILL_INTENTS_REFINED_20261101.md
- Analiza historyczna: docs\OPENFILL_TOKEN_WASTE_AUDIT_20261031.md
W razie nowszej równoległej publikacji decyduje aktualny SHA pliku LIVE i najnowsze wpisy checkpointu, nie ten historyczny PID.

## 2. Stan PBOX1 LIVE, niezależnie sprawdzony 4 listopada
| Element | Wynik |
| --- | --- |
| LIVE proces | PID 3756, C:\Users\pboru\OpenFill\app\OpenFill.exe |
| Harmonogram | OpenFill-open: Running |
| Core SHA256 | C7632AF5A4CE7A691C0CDD827A298DEEE71C8CFEEC6E6F3F4E41BF1DC46223B1 |
| EXE SHA256 | 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52 |
| MCP | openfill_info({}) zwróciło {"working":[]} |
| Staging publish | staging\of_final_e02_d04_20261104 |
| DEV | staging\of_imp_20261031, po aktywacji PID 21936, Core SHA zgodny |
| LIVE rollback | C:\Users\pboru\OpenFill\app__rollback__before_release_20261104_200502 |
| Poprzedni LIVE SHA | 98F12574463BA124956C26B7210F8157A5B58226B850CE875C27CA9E67377609 |
PID jest zmienny, a MCP working=[] potwierdza działający kanał, nie pełną poprawność akcji biznesowych.

## 3. Obowiązujące decyzje użytkownika
1. Priorytet: szybkie SOURCE -> build -> DEV -> LIVE z rollbackiem. Wydawać od razu nawet przy aktywnych taskach, bez czekania na idle.
2. Prace prowadzić na zamkniętej liście. Nie dopisywać hipotetycznych problemów co przebieg i nie budować prewencyjnych mechanizmów ratunkowych dla jednego użytkownika.
3. Statusy attempted/uncertain/verified/skipped są odrębne. Przy uncertain najpierw reconcile, nigdy ślepe ponowienie wysłania, zapisu lub płatności.
4. Stop w GUI użytkownika jest innym zdarzeniem niż cancel od zlecającego. Kody: user_stop, requester_cancel, upgrade_interrupted, crash_interrupted, timeout, model_finish, model_partial. Nie przypisywać modelowego cancel człowiekowi.
5. Dla zwykłych operacji preferować już dostępne natywne API/MCP zamiast przeglądarki. Wyjątek: Luna w toku logowania może sama otwierać pocztę w przeglądarce i pozyskiwać kody/linki/dane uwierzytelniające; nie wymagaj od modelu zlecającego przesyłania tych wiadomości przez jego connector.
6. GitHub connector/autosync to luźna możliwość, bez wymuszonego routingu, bez zakazu OpenFill i bez automatycznego zakładania repo.
7. Airbnb D03 jest ANULOWANE. Nie realizować.
8. Wybudzanie projektu of na SATANAMD jest wyłączone (Wake.Enabled=false, wcześniej zweryfikowano dwa razy). Nie reaktywować bez nowej decyzji.
9. Nie duplikować PageCapture, SQLite, SemanticView, act_many, istniejącej obsługi WA/LinkedIn ani Live/Dev mechanizmów.
10. Symulacja/fixture PASS nie dowodzi rzeczywistego wysłania wiadomości ani publikacji w zewnętrznym serwisie.

## 4. Architektura
- OpenFill.App: aplikacja Windows i WebView2, wydania DEV/LIVE.
- OpenFill.Core.Hosting: AppHost, TaskRun, lifecycle, anulowania, usage i raporty.
- OpenFill.Core.Mcp: McpServer, McpTaskManager, requester/chat, izolacja, idempotencja i LocalPlan routing.
- OpenFill.Core.Agent: AgentRunner, ToolRegistry, PageCapture/CurrentTextCaptureCache, pamięć i FTS5 SQLite, ledger, procedury, LocalPlan, adaptery domenowe.
- OpenFill.Core.Browser: BrowserController/CDP, WhatsAppBrowser, LinkedIn preflight.
- Warstwy: model zlecający wyznacza cel, Luna rozwiązuje wyjątki, deterministyczny wykonawca robi wiele lokalnych operacji, a SQLite i dowody skutku chronią wznowienia.
## 5. Stan zaawansowania i zakres ukończony
- A01-A03: narzedzia terminalowe, filtry i manifesty procesu.
- A04: capture DOM, manifest i SHA256, zakres i preview. A05/A27: receptury oraz cache neutralnego tekstu po sprawdzeniu bieżącego URL, selektora i SHA.
- A06-A27: router widoku, SemanticView, selektywne verify, act_many, nawigacja, krótsze instrukcje i wyspecjalizowane operacje.
- B01-B03: pamięć SQLite/FTS5, zakres konta, ledger, idempotencja, identyfikatory osób i wątków.
- C01-C03: katalog procedur, executor, ensure_auth, resolve, observe, execute, verify, reconcile.
- D01: WhatsApp batch read według numerów i nazw. W LIVE batch send explicit candidate, z preflight, kontrolą nadawcy i per-recipient ledger; rzeczywiste wysyłki NIE potwierdzone.
- D02: LinkedIn batch send explicit candidate w LIVE, preflight profilu nadawcy/odbiorcy/thread oraz ledger; rzeczywiste wysyłki NIE potwierdzone.
- E01: analiza 1477 zadań i 73329 model calls, ranking intencji na podstawie historii.
- E03: audyt tokenów, kosztów, DOM, task-review; tools\of_efficiency_audit_20261104.ps1 i raport OPENFILL_EFFICIENCY_LIVE_20261104.md.
- GitHub: brak wymuszonego routingu i autosync. Enter/Shift+Enter w karcie pytania Luny zaliczone syntetycznie.
- Stop w LIVE: zróżnicowane przyczyny user_stop, requester_cancel, upgrade_interrupted, crash_interrupted, timeout, model_finish i model_partial.

## 6. LocalPlan, aktualny zakres
- Produkcyjny execution_mode=local_plan_v1_draft: jedno ograniczone planowanie modelowe, lokalne akcje nawigacji, wyszukiwania i niezatwierdzonego draftu. Nie przenosić automatycznie wysyłek, płatności ani save/submit.
- W SOURCE/LIVE istnieje LocalPlanAutoRouting.TrySelect dla prostego filtrowania jednego pola przy jednym jednoznacznym niesensytywnym URL. Brak continuation, explicit mode, side effects; nie tworzyć drugiego routera.
- Produkcyjny test E2E OF-20261104-1708-459e: success, 1 model call, 4 DOM reads, 1 action, 0.0001403 USD.
- Legacy OF-20261104-1709-edd5 w porównywalnym lokalnym zadaniu: success, 5 model calls, 0.001762 USD. Jedna para, nie mediana ani gwarantowana oszczędność dla innych domen.
- Test auto-routing 16/16 PASS; szerszy autonomiczny routing LocalPlan pozostaje otwarty.

## 7. Ostatni kod E02 / D04, bez powtarzania wdrożenia
Dodane SOURCE: src\OpenFill.Core\Agent\ProcedureLearningStore__v0.1__2026-11-04.cs oraz src\OpenFill.Core\Agent\DomainReadAuditService__v0.1__2026-11-04.cs.
Testy: tools\of_imp_smoke\ProcedureLearningSmoke.cs i D04DomainReadSmoke.cs.
Zmodyfikowane SOURCE:
- src\OpenFill.Core\Agent\ProcedureCatalog__v0.1__2026-10-31.cs
- src\OpenFill.Core\Agent\ProcedureExecutor__v0.1__2026-10-31.cs
- src\OpenFill.Core\Agent\WhatsAppBatchService__v0.1__2026-10-31.cs
- src\OpenFill.Core\Agent\LinkedInSendBatchService__v0.1__2026-11-01.cs
- src\OpenFill.Core\Agent\ToolRegistry__v0.22__2026-10-29_2314.cs
- src\OpenFill.Core\Mcp\McpServer__v0.24__2026-10-28_1648.cs
- procedures\registry.json oraz tools\of_imp_smoke\Program.cs
- tests\OpenFill.Tests\McpTests__v0.17__2026-10-28_2216.cs
- tests\OpenFill.Tests\Program__v0.25__2026-10-29_2314.cs
- DEV activation: tools\of_final_e02_d04_20261104_activate_dev.ps1

E02: trwałe statusy candidate/verified/suspended, zgodnie z raportem wydania.
