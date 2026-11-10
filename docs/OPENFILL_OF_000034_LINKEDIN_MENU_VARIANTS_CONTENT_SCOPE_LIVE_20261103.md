# OF-000034: dwa warianty nawigacji LinkedIn i rozdzielenie receptur treści

Wybudzenie OF-000034-A01-061033: 2026-11-03 06:10:33 +01:00 (Warszawa).
Host: PBOX1. SOURCE: C:/Users/pboru/Downloads/OpenFill__v0.1__2026-10-04_1911/OpenFill.
Punkt wyjścia: docs/OPENFILL_OF_000033_LINKEDIN_ACCOUNT_MENU_LIVE_20261103.md.

## Diagnoza rzeczywistego LIVE
- Rejestr przetrwał OF-000033: www.linkedin.com.json ma /feed/, text, selector nav:has(button[data-view-name="navigation-settings"]), viewSignature SHA256/64, accountScope linkedin:in/pawel-boruta-osko, confidence=verified, taskSuccesses=1.
- Task OF-20261103-0811-c0ec na LinkedIn /feed/ dał page_capture: SELECTOR_NOT_FOUND dla tego selektora, capture_id brak. read_view bez argumentów: view_type=list, routing_reason=generic_visible_records. Nie był to błąd logowania ani przechowywania podpisu.
- Diagnostyka tej samej karty OF-20261103-0812-29c7: istnieją linki nawigacji /mynetwork, /messaging, /notifications, ale nie istnieje button[data-view-name="navigation-settings"]. Przyciskiem Me jest pojedynczy button[aria-expanded] we właściwym nav.
- Task OF-20261103-0813-cd64: w nowym menu dwa linki /in/ do własnego profilu. Jedyny link "View profile" ma aria-disabled="false"; po badaniu menu przywrócono aria-expanded=false. Potwierdzony profil /in/pawel-boruta-osko/.
- Wniosek: co najmniej dwa aktywne warianty DOM LinkedIn; poprzednia receptura dla jednego wariantu ma zbyt specyficzny selektor. Receptury nawigacji nie powinny być automatycznie używane jako receptury listy postów.

## SOURCE
1. src/OpenFill.Core/Agent/LinkedInAccountMenuProbe__v0.1__2026-11-03.cs: rozpoznaje przycisk Me ze starego data-view-name lub pojedynczy button[aria-expanded] w nawigacji z trzema linkami. W menu preferuje jeden link data-view-name=nav-me-settings-view-profile, w nowszym wariancie wymaga dokładnie jednego a[aria-disabled="false"][href*="/in/"] w kontenerze role=menu. Zawsze próbuje przywrócić wcześniejszy stan menu.
2. src/OpenFill.Core/Agent/AutoViewReadPlanner__v0.1__2026-11-01.cs: IsContentRecipe odrzuca jako automatyczny odczyt listy receptury z root nav/header/footer/aside oraz role=navigation. Chroni przed zwróceniem nawigacji jako wyników feedu.
3. src/OpenFill.Core/Agent/ToolRegistry__v0.22__2026-10-29_2314.cs: read_view automatyczny filtruje listę kandydatów przez IsContentRecipe przed live probe; nie wybiera też legacy receptur nawigacji. Explicit page_capture zachowuje możliwość zrzutu nav.
4. tools/of_imp_smoke/Of031TextRecipeSmoke.cs: pięć dodatkowych sprawdzeń, w tym fallback Me bez data-view-name, własny link w nowym menu, brak automatycznego reuse nav i zachowanie main content.

## QA i wdrożenie
- Test celowany: dotnet run tools/of_imp_smoke/FileFilterSmoke.csproj -c Debug --no-restore -- of031-text-recipe-only (cwd tools/of_imp_smoke): OF034_MENU_AND_CONTENT_22_CHECKS_OK, exit 0. Istniejące warnings bez błędów.
- dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000034 -v:q: exit 0.
- Core SHA256: 4D58AF0687180C66019CEDA156855CBFCB611A7B88B8E266BEB97B3268F157A3.
- DEV backup: staging/of_000034_backup_pre_dev_activation; task OpenFill-OFIMP-dev-20261031 aktywny, DEV PID 24708, EXE staging/of_imp_20261031/OpenFill.exe.
- -CheckOnly przed: RELEASE_READY_NO_IDLE_GATE. LIVE release: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True, exit 0.
- LIVE backup: C:/Users/pboru/OpenFill/app__rollback__before_release_20261103_082028.
- LIVE PID 27584, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, rzeczywisty Core SHA zgodny z wydaniem.
- LIVE EXE SHA256: 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52.
- CheckOnly po release: LIVE_ALREADY_CURRENT z wydaniem Core SHA, exit 0.
- Faza: SOURCE DONE -> DEV ACTIVE -> LIVE ACTIVE.

## Realne acceptance po wydaniu
- OF-20261103-0822-3776, 4 kroki, koszt 0.0018 USD, done/success. LinkedIn /feed/: page_capture mode=text, selector nav:has(a[href*="/messaging"]), use_best=false.
- Capture ID: capture_20261103_052238_937_cc0def. Manifest: source_url=https://www.linkedin.com/feed/, root_selector=nav:has(a[href*="/messaging"]), capture_mode=text, account_scope=linkedin:in/pawel-boruta-osko, account_scope_source=linkedin_signed_in_self_menu, view_signature długości 64, files=1.
- Rejestr LIVE zawiera dla nowego selektora signed recipe: confidence=verified, successes=1, taskSuccesses=1, accountScoped=true.
- read_view na tej samej karcie: view_type=list, routing_reason=generic_visible_records, zwrócił 9 aktualnie widocznych rekordów feedu, nie tylko nagłówek. Dobór generic jest poprawny: aktualna signed recipe obejmuje wyłącznie nav.
- Nie twierdzić, że verified nav recipe została użyta do odczytu feedu; dokładny reuse treści LinkedIn nadal niepotwierdzony. Reuse podpisanej receptury treści wcześniej potwierdzono na neutralnym news.ycombinator.com w OF-000032.

## Pozostałe
- D01 WhatsApp i D02 LinkedIn: akceptacja operacji skutkowych nadal otwarta; w tym wake żadnych wysyłek.
- D03 Airbnb: ANULOWANE przez właściciela, nie podejmować.
- Handoff current niezmieniony, lead OF-000030..039; następny handoff przy 039.
