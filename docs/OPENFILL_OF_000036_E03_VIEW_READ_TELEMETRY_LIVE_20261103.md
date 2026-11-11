# OF-000036: E03 pomiar decyzji i czasu read_view, wdrożone LIVE

Wybudzenie: OF-000036-A01-065033, 2026-11-03 06:50:33 +01:00 (Warszawa).
Host PBOX1. SOURCE C:/Users/pboru/Downloads/OpenFill__v0.1__2026-10-04_1911/OpenFill.
Kontynuacja docs/OPENFILL_OF_000035_LINKEDIN_CONTENT_RECIPE_REUSE_LIVE_20261103.md.

## Wybór zadania
- E01 już wykonane z analizą intencji v3; A05 i A06 zweryfikowane LIVE.
- D01 WhatsApp i D02 LinkedIn mają komponenty SOURCE, ale rzeczywiste operacje skutkowe bez zatwierdzonego odbiorcy i treści pozostały niezweryfikowane.
- Wybrano niedokończony E03: brak jednolitej telemetrycznej informacji, dlaczego automatyczne read_view trafiło albo nie trafiło do podpisanej receptury. Dotychczas istnieją odrębne eventy capture_filter i liczniki użycia modelu; nie duplikowano ich.

## SOURCE
- src/OpenFill.Core/Agent/ToolRegistry__v0.22__2026-10-29_2314.cs:
  - Stopwatch mierzony od początku jednego logicznego read_view.
  - Lokalna funkcja FinishRead emituje jeden event EventLog source=view_read, eventType=read_completed przy wszystkich ścieżkach normalnego zakończenia: error routing, generic digest, delegowane get_page/observe_query/file_query, podpisane recipe.
  - Struktura eventu: runId, host, target, viewType, routingReason, recipeCandidates (verified content scope dla obecnej ścieżki), recipeHit, recipeMiss, accountScoped (bool), reusedStoredCapture (file_query), elapsedMs, outputBytes.
  - Zdarzenia zapisują tylko metadane, bez treści stron i wartości formularzy. Dane szczegółowe CLI pozostają w capture_filter.
  - Poprawiono opis read_view: exact verified view/account scoped (w LIVE działa również LinkedIn).
- Nie tworzono nowego loggera, drugiego MCP, nowych narzędzi ani nowego schematu w SQLite.

## Kompilacja, wdrożenie, rollback
- dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000036 -v:q: exit 0. Wyłącznie istniejące ostrzeżenia WindowsBase, nullable, unused.
- SHA256 OpenFill.Core.dll: B9ECB6EC4ADA29E5987E87B8AFBFBA6CE4DA8FD5A9A67E038F2D5D9D2CCBB8C5.
- DEV backup: staging/of_000036_backup_pre_dev_activation. Zatrzymano jedynie DEV zadanie OpenFill-OFIMP-dev-20261031, skopiowano publish do staging/of_imp_20261031 i ponownie uruchomiono. DEV PID 25168, EXE staging/of_imp_20261031/OpenFill.exe.
- Release -CheckOnly przed: RELEASE_READY_NO_IDLE_GATE. tools/of_imp_release_live_ALWAYS_20261101.ps1: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True, exit 0, bez warunku idle.
- LIVE rollback: C:/Users/pboru/OpenFill/app__rollback__before_release_20261103_085735.
- LIVE PID 18904, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe. Core SHA zgodny. OpenFill.exe SHA256 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52.
- Po release -CheckOnly: LIVE_ALREADY_CURRENT, exit 0. MCP openfill_info working=[].

## Rzeczywiste acceptance
- Task OF-20261103-0858-a9ad: read_view na https://news.ycombinator.com/, status=ok, view_type=table, routing_reason=generic_visible_records, task partial z powodu treści widoku: serwis odpowiedział "Sorry, we're not able to serve your requests this quickly." Brak normalnej treści strony, więc nie użyto podpisanej receptury.
- Plik LIVE C:/Users/pboru/OpenFill/data/stable/logs/app_20261103.ndjson zawiera dwa rzeczywiste eventy view_read/read_completed dla tego taska, zgodnie z wykonanymi wywołaniami narzędzia. Przykład seq=49, tsUtc=2026-11-03T05:58:32.9386083Z, runId=20261103_055826_96d59e, host=news.ycombinator.com, target=observe_query, viewType=table, routingReason=generic_visible_records, recipeCandidates=1, recipeHit=false, recipeMiss=true, accountScoped=false, reusedStoredCapture=false, elapsedMs=187, outputBytes=1178. Drugi event seq=56: elapsedMs=8, outputBytes=1178. To potwierdza logowanie każdej faktycznej próby, nawet przy zmianie zawartości strony.
- Test potwierdza zapis danych telemetrycznych w rzeczywistym LIVE, nie twierdzi, że pokazuje sukces pobrania postów Hacker News.

## Status i dalszy zakres
- E03 read_view routing metrics: SOURCE DONE, DEV ACTIVE, LIVE ACTIVE, realny event log PASS. E03 szerszy przekrojowy zakres kosztów operacji pozostał częściowo otwarty, nie oznaczać całego E03 jako ukończone.
- D01/D02 realne wysyłki nie były wykonywane. Airbnb D03 ANULOWANE przez właściciela 2026-11-02.
- Lead OF-000030..039, handoff dopiero na OF-000039. Harmonogramu wybudzeń nie zmieniano.
