# OF-000011: wait_until, fill i assert_value, SOURCE/DEV

Wybudzenie: OF-000011-A01-222552, 2026-11-02 22:25:52 +01:00 (Warszawa).

## Zmiany

- `src/OpenFill.Core/Agent/LocalPlanExecutor__v0.1__2026-11-02.cs`: rozszerzenie ścisłego `of.plan.v1` o `wait_until`, `fill`, `assert_value`. Zachowano poprzednie `locate`, `click` na linku nawigacyjnym i `assert_visible`.
- `wait_until` ma obowiązkowy `timeout_ms` w zakresie 100 do 5000 ms, odczyt co najwyżej co 100 ms. Po kliknięciu wymaga nowej rewizji DOM oraz jednoznacznego zaobserwowania elementu. Zmiana hosta, spadek rewizji, niejednoznaczność lub timeout kończą plan bez ponawiania akcji.
- `fill`: wyłącznie wcześniej powiązany semantyczny locator `textbox`; wymaga bezpośrednio następującego `assert_value`, limit 2000 znaków, maksymalnie 12 fill w planie. `assert_value` wymaga nowej rewizji i dokładnego porównania tekstu. Nie wolno zinterpretować samo `applied` jako dowodu skutku.
- NOWY `ILocalPlanFormBrowser` jest opcjonalnym interfejsem. Stare przeglądarki implementujące wyłącznie `ILocalPlanBrowser` pozostają zgodne.
- `src/OpenFill.Core/Agent/LocalPlanCdpBrowser__v0.1__2026-11-02.cs`: snapshot uwzględnia wartość textbox, wlicza ją do fingerprintu. Fill atomowo sprawdza fingerprint/locator w jednej ewaluacji CDP, używa natywnego setter + zdarzenia input; brak przycisku save/submit/send.
- `tools/of_imp_smoke/LocalPlanWaitSmoke.cs` oraz `LocalPlanFillSmoke.cs`, przełączniki `archflow-wait-only` i `archflow-fill-only`.
- `tools/of_archflow_cdp_smoke/Program.cs`: lokalny formularz z opóźnionym renderingiem, wypełnienie pola, odczyt wartości, race stale, brak nadpisania zmodyfikowanego pola.

## Potwierdzone QA

- `ARCHFLOW_WAIT_20_CHECKS_OK`: exit 0.
- `ARCHFLOW_FILL_13_CHECKS_OK`: exit 0.
- `ARCHFLOW_LOCAL_PLAN_26_CHECKS_OK`: exit 0, poprzednia funkcjonalność działa.
- `ARCHFLOW_CDP_REAL_BROWSER_19_CHECKS_OK`: exit 0 na rzeczywistym Edge/CDP, lokalny serwer fixture, bez logowania do usług zewnętrznych.
- W jednym przebiegu testu Edge wszystkie kontrole przeszły, ale końcowe usunięcie katalogu profilu testowego zgłosiło `UnauthorizedAccessException: Access to the path 'Affiliation Database' is denied`. Poprawiono cleanup na 3 próby i raportowanie `CDP_PROFILE_CLEANUP_DEFERRED`. Przyczyny blokady pliku nie rozstrzygnięto (możliwy uchwyt procesu potomnego lub ACL); nie jest to awaria testowanego działania CDP.

## Stan i granica

SOURCE/DEV, brak podłączenia do AgentRunner, ToolRegistry lub LIVE. Nie wykonywano żadnych wysyłek, zmian cen, Airbnb ani zapisów w kontach zewnętrznych. `fill` nie jest operacją wysłania, ale zdarzenie input może mieć skutki na stronie z autosave, więc przed produkcyjnymi zmianami trzeba użyć EffectJournal i potwierdzeń skutku. Nie deklarować oszczędności calls, dopóki rzeczywisty A/B nie zostanie wykonany.

Następne: kontrolowany test w osadzonym WebView2 na PBOX1, później warunki `if` i ograniczone `foreach`, połączenie dziennika efektów z wykonawcą i telemetrią `business_operation_id/task_id/run_id`, wreszcie A/B jakości + liczników.
