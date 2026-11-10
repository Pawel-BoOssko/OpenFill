# OF-000033: identyfikacja konta LinkedIn w zamknietym menu, LIVE

Wake: OF-000033-A01-055024. Warszawa: 2026-11-03 05:50:24 +01:00.
Host: PBOX1, SOURCE C:/Users/pboru/Downloads/OpenFill__v0.1__2026-10-04_1911/OpenFill.
Punkt wyjścia: docs/OPENFILL_OF_000032_VERIFIED_RECIPE_REAL_LIVE_20261103.md.

## Ustalona przyczyna
- LinkedIn ma nowy interfejs nawigacji bez dawnych klas global-nav.
- Z zamknietego menu "Me" nie da się odczytać odnośnika do własnego profilu przez dotychczasowy selektor .global-nav__me-content/.global-nav__me-menu.
- CaptureAccountScopeGate wymaga zweryfikowanego odnośnika do własnego profilu, nie zgadza się na samo rozpoznanie zalogowanego nav.
- Realny task OF-20261103-0751-c0a0 otworzył i zamknął menu, ustalił:
  button[data-view-name="navigation-settings"]
  div[role="menu"] a[data-view-name="nav-me-settings-view-profile"]
  link https://www.linkedin.com/in/pawel-boruta-osko/
  status zadania partial, ponieważ nie zrealizowano całości intencji w pierwszym przebiegu, ale powyższe selektory oraz przywrócenie aria-expanded=false były zweryfikowane w DOM.
- W tym wake zakres nie obejmuje wysyłania wiadomości ani edycji kont.

## Zmiana SOURCE
- Nowy src/OpenFill.Core/Agent/LinkedInAccountMenuProbe__v0.1__2026-11-03.cs.
- Jedna asynchroniczna procedura JS: znajduje zweryfikowaną nawigację LinkedIn, odczytuje stary menu link jeśli istnieje; dla nowej nawigacji otwiera menu przyciskiem navigation-settings, czeka do 6x60 ms na własny anchor nav-me-settings-view-profile, zwraca jeden URL, po czym przywraca poprzedni stan menu przy użyciu aria-expanded. Nie nawigowano do profilu.
- PageCaptureService.CaptureAsync(mode=text/elements/html/all) wykorzystuje procedurę do accountSelfLinks w tym samym wywołaniu RunJsAsync. RunJsAsync korzysta z CDP EvaluateAsync(awaitPromise=true), więc asynchroniczna ewaluacja jest obsługiwana.
- CaptureRecipeLiveProbe.SelectAsync korzysta z tej samej procedury podczas próby dopasowania signed recipe.
- CaptureAccountScopeGate.Resolve pozostaje bez zmian: wymagane HTTPS, właściwy host, marker auth, brak login form, jeden prawidłowy self-profile slug. Nie uznaje linków dowolnych osób.
- Capture + live probe pozostają podpisane pełnym 64-znakowym SHA i accountScope, bez bypassu.

## Testy i wdrożenie
- Smoke: dotnet run tools/of_imp_smoke/FileFilterSmoke.csproj -c Debug --no-restore -- of031-text-recipe-only przy cwd=tools/of_imp_smoke: OF033_TEXT_FEED_AND_MENU_17_CHECKS_OK, exit 0, znane ostrzeżenia kompilacji.
- Publish Release: dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000033 -v:q, exit 0.
- Core SHA256 wydania: 3722DB65FCB83D56BD4818F1F59549AED034EE78A1DA6FCD411A9976ADD60505.
- DEV backup: staging/of_000033_backup_pre_dev_activation; task OpenFill-OFIMP-dev-20261031 Running, PID 13332.
- Release CheckOnly przed: RELEASE_READY_NO_IDLE_GATE. LIVE release: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True, exit 0.
- LIVE rollback: C:/Users/pboru/OpenFill/app__rollback__before_release_20261103_075717.
- LIVE proces PID 14224, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe.
- LIVE Core SHA256: 3722DB65FCB83D56BD4818F1F59549AED034EE78A1DA6FCD411A9976ADD60505.
- LIVE OpenFill.exe SHA256: 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52.
- Po wdrożeniu: CheckOnly LIVE_ALREADY_CURRENT, exit 0.

## Rzeczywiste WebView2 acceptance
- Task OF-20261103-0757-226d: LinkedIn Feed, page_capture(mode=text, selector=nav:has(button[data-view-name="navigation-settings"]), use_best=false), zadanie done/success, 3 kroki, koszt 0.0015 USD.
- Capture ID capture_20261103_045824_216_adccf5. Odczyt rzeczywistego manifestu potwierdza source_url=https://www.linkedin.com/feed/, mode=text, account_scope=linkedin:in/pawel-boruta-osko, account_scope_source=linkedin_signed_in_self_menu, view_signature o długości 64, files=1.
- Rejestr LIVE data/stable/notes/recipes/www.linkedin.com.json zawiera dla /feed/, mode=text, selector nav:has(...): confidence=verified, successes=1, taskSuccesses=1, signatureLen=64, accountScoped=true.
- Task OF-20261103-0759-e370: read_view bez nawigacji na about:blank -> structure_unavailable, partial. Przyczyna: nowa pusta karta. Nie wyciągać wniosków o recipe.
- Kontynuacja OF-20261103-0759-436d po otwarciu Feed: done/success, view_type=list, routing_reason=generic_visible_records, posts + navigation obecne, 4 kroki, koszt 0.0014 USD. Nie wskazano verified_live_recipe na tym aktualnym widoku. Struktura mogła się zmienić, ale przyczyna braku exact match nie jest potwierdzona. Nie zgłaszać pozytywnego LinkedIn recipe reuse.
- Reuse signed verified recipe wcześniej potwierdzone na neutralnym news.ycombinator.com w OF-000032.

## Status / dalsza praca
- A05 identyfikacja konta w zamknietym LinkedIn "Me" menu: SOURCE DONE, DEV ACTIVE, LIVE ACTIVE, REAL CAPTURE+ACCOUNT SCOPE CONFIRMED.
- Signed recipe LinkedIn: utrwalenie i task promotion CONFIRMED; exact reuse w odrębnej sesji NIE POTWIERDZONE, bez eskalowania do błędu bez diagnostyki struktury.
- D01 WhatsApp / D02 LinkedIn skutkowe acceptance nadal niepotwierdzone; nie wysyłano niczego.
- D03 Airbnb ANULOWANE 2026-11-02, nie wracać do niego.
- Lead trwa przez wake 30..39. W wake 33 nie nadpisywano handoff na SATANAMD.
