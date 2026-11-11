# OF-000032: rzeczywista promocja i ponowne uzycie verified recipe w LIVE

Wybudzenie: OF-000032-A01-053316, 2026-11-03 05:33:16 +01:00 Warszawa.
Host: PBOX1. SOURCE: C:/Users/pboru/Downloads/OpenFill__v0.1__2026-10-04_1911/OpenFill.
Punkt wyjscia: docs/OPENFILL_OF_000031_TEXT_RECIPE_LIVE_20261103.md.
Wczesniejsze A05 text recipe i A08 feed rozpoznawanie byly LIVE.

## Faktyczna luka SOURCE
- ToolRegistry.RecordCaptureTaskOutcome przekazuje _usedCaptures do CaptureRecipeStore.RecordTaskOutcomeScoped po zakonczeniu calego taska.
- page_capture dodawal swoje captures do _usedCaptures, ale observe_query, uzywany przez read_view dla jawnych typow i receptur, nie dodawal zadnego.
- Wiec zapisany podpis 64 SHA256 mogl zostac jako candidate nawet przy poprawnym wykonaniu read_view. Potwierdzenie w LIVE: plik www.linkedin.com.json zawieral signed candidate, taskSuccesses=0 i brak accountScope. Poprzednie legacy recipe bez podpisu to inny kontrakt.
- Rownolegle realny LinkedIn /feed/ korzystal z nowego nav bez klasy global-nav. Potwierdzil to rzeczywisty, minimalny zrzut HTML nav (capture_20261103_043733_562_4539f3, 7006 B): nav zawiera /mynetwork, /messaging, /notifications i przycisk menu z aria-expanded, ale nie nav.global-nav.

## SOURCE
- ToolRegistry observe_query: po udanym filtrowaniu status=ok i podpisanym capture SHA64 rejestruje (host,path,mode,selector,pattern="",viewSignature,accountScope) w _usedCaptures. TaskRun juz wywoluje RecordCaptureTaskOutcome(conclusion.Status), bez nowego lifecycle. Nie awansuje nieudanych zapytan.
- PageCaptureService CaptureAsync: rozpoznaje dodatkowo nowy nav LinkedIn na podstawie jednoczesnej obecnosci /mynetwork, /messaging, /notifications i button[aria-expanded]; w razie explicite ustawionego authenticatedSelector nadal wymaga wskazanego selektora.
- CaptureRecipeLiveProbe: wykrywanie markeru auth przystosowano do tego samego nowego nav.
- Nie uznaje samego markera auth za identyfikator konta. CaptureAccountScopeGate nadal wymaga jednoznacznego linku do wlasnego profilu w menu, wiec LinkedIn signed candidate bez accountScope nie staje sie verified.

## QA i wydanie
- Celowany smoke: dotnet run tools/of_imp_smoke/FileFilterSmoke.csproj -c Debug --no-restore -- of031-text-recipe-only, cwd tools/of_imp_smoke. Wynik OF031_TEXT_AND_FEED_13_CHECKS_OK, exit 0.
- Publish Release staging/of_000032: exit 0, tylko dotychczasowe warnings.
- Core SHA256: B402253322D9411389F08CED093A31EC863DA42902DF159597EB31F5AC777205.
- DEV backup: staging/of_000032_backup_pre_dev_activation. Zadanie OpenFill-OFIMP-dev-20261031 odswiezone, PID 11296, EXE staging/of_imp_20261031/OpenFill.exe.
- CheckOnly przed: RELEASE_READY_NO_IDLE_GATE. Skrypt release tools/of_imp_release_live_ALWAYS_20261101.ps1: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True, exit 0.
- LIVE backup: C:/Users/pboru/OpenFill/app__rollback__before_release_20261103_074214.
- LIVE PID 18776, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, Core SHA zgodny, EXE SHA256 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52.
- CheckOnly po release: LIVE_ALREADY_CURRENT, exit 0. MCP openfill_info: working=[].
- Faza SOURCE DONE -> DEV ACTIVE -> LIVE ACTIVE.

## Realny test zweryfikowanej receptury
1. Task OF-20261103-0743-32d1: https://news.ycombinator.com/, read_view(view_type=list,operation=text_regex,query=[A-Za-z]); status=ok, 65 linii, capture_id=capture_20261103_044318_806_f6af99, source_scope=currently_loaded_dom, task done/success, 3 kroki.
2. Zapis LIVE: data/stable/notes/recipes/news.ycombinator.com.json: host news.ycombinator.com, path /, mode text, selector body, signed SHA64, confidence=verified, successes=1, taskSuccesses=1. To potwierdza uruchomienie nowej rejestracji outcome przez read_view.
3. Task OF-20261103-0744-1b35 na tej samej karcie domeny: read_view bez argumentow, done/success, status=ok, view_type=table, routing_reason=verified_live_recipe. Wykorzystanie podpisanej receptury w rzeczywistym WebView2 potwierdzone.
- Laczy koszt tych dwoch taskow: 0.0014 + 0.0011 = 0.0025 USD.
- Test nie jest symulacja ani jedynie smoke klasy. Prawdziwy zapis, outcome i ponowny selection na host neutralny.

## Pozostale
- LinkedIn: nowy marker nav zaimplementowany i skompilowany, ale nie wykonano osobnego post-release page_capture z require_authenticated=true. AccountSelfLinks z aktualnego menu zamknietego pozostaje brak; scoped verified recipe niepotwierdzone. Nie obchodzic rozdzielenia login marker i identity.
- D01 i D02 skutkowe acceptance nadal niewykonane, brak zatwierdzonego odbiorcy i tresci. Nie wysylano wiadomosci ani nie modyfikowano kont.
- D03 Airbnb ANULOWANE przez wlasciciela 2026-11-02.
- Rotacja: OF-000032 jest wybudzeniem tego samego leada co 000030, handoff dopiero na 000039.
