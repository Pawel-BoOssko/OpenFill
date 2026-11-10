# OpenFill OF-000017: effect_id dla wielu skutków jednej operacji i realny WebView2 DEV

Start silnika: `OF-000017-A01-002557`, 2026-11-03 00:25:57 +01:00 (Warszawa). Host: PBOX1. Zmiany wyłącznie SOURCE i DEV. LIVE niezmieniony.

## Zakres wdrożenia

1. `src/OpenFill.Core/Agent/LocalPlanEffectJournal__v0.1__2026-11-02.cs`:
   - `LocalPlanEffectKey` ma opcjonalny `EffectId` z domyślnym `"default"`, zachowując poprzedni kontrakt konstruktora, identyfikator batch i fingerprint dla wszystkich istniejących wywołań.
   - Dla jawnych `EffectId` batch request ID jest deterministycznym SHA256 kanonicznie serializowanego zakresu `{Provider,AccountId,BusinessOperationId,EffectId}`, pod prefiksem `of-effect-v1:`. Dwa węzły efektów w jednej operacji biznesowej mają dwa niezależne, trwałe wpisy SQLite.
   - Fingerprint efektu uwzględnia dodatkowo `TargetId`, `Action`, `PayloadSha256`. Ponowne użycie tego samego `effect_id` ze zmienioną operacją lub payload powoduje `local_plan_business_operation_conflict`. `TaskId` i `RunId` pozostają wyłącznie korelacją, nie zmieniają tożsamości.
   - `EffectId` wymagany po normalizacji: niepusty, bez białych znaków na końcach, długość maksymalnie 128. Bez zmian w oryginalnym `OperationalMemoryStore` ani migracji schematu SQLite.
2. `src/OpenFill.App/LocalPlanWebView2EffectDevAdapter__v0.1__2026-11-03.cs`:
   - Kompilacja tylko `#if DEBUG`. Wyłącznie `http://127.0.0.1:<port>/effects`, provider `of-local-fixture`, account `dev-session`, akcja `save`, zamknięty zbiór targetów `a/b` i powiązane `save-a/save-b` oraz hash lokalnego payload.
   - Jeden rzeczywisty `WebView2.ExecuteScriptAsync` klikający przycisk dla elementu docelowego. Niezależny `ObserveAsync` ponownie odczytuje licznik oraz napis `Saved a` lub `Saved b`, dowód dopiero przy count=1.
   - Żadnych model-generated JS, dowolnych selektorów, zewnętrznych kont, zewnętrznego HTTP ani wpisów do LIVE.
3. `tools/of_archflow_webview2_smoke/EffectDevSmoke.cs` oraz `Program.cs`:
   - Opcjonalny `--effect-dev`; lokalny HTTP fixture z przyciskami `Save a/Save b` i licznikami DOM.
   - Dwa `LocalPlanEffectExecutor.ExecuteAsync` z tym samym `BusinessOperationId=business-017`, ale `EffectId=save-a/save-b`, odmienne `TargetId`.
   - Po odtworzeniu nowego `OperationalMemoryStore` z tej samej bazy SQLite próba ponownego wykonania obu efektów zwraca `already_recorded`; niezależny skrypt DOM potwierdza po jednym kliknięciu obu przycisków.
4. `tools/of_imp_smoke/LocalPlanEffectExecutorSmoke.cs`: dodano test pary efektów, ochrony po restarcie, konfliktu tożsamości i nieprawidłowego effect_id.

## Wyniki pomiarów

- `ARCHFLOW_EFFECT_EXECUTOR_30_CHECKS_OK`, kod wyjścia 0.
- `ARCHFLOW_EFFECT_WEBVIEW2_14_CHECKS_OK`, kod wyjścia 0, rzeczywisty Microsoft WebView2 w Windows SessionId=1.
- `ARCHFLOW_WEBVIEW2_EMBEDDED_20_CHECKS_OK`, kod wyjścia 0.
- Potwierdzone dane z rzeczywistego WebView2:
  - `businessOperationId=business-017`;
  - `effectIds=[save-a,save-b]`;
  - `aState=verified`, `bState=verified`;
  - dowody `local-effect:a:count=1`, `local-effect:b:count=1`;
  - niezależne odczyty DOM `count-a=1`, `count-b=1`;
  - `modelCalls=0`.
- `dotnet build tools/of_archflow_webview2_smoke/ArchflowWebView2Smoke.csproj --no-restore -v:q`: 0 errors, 9 istniejących warnings.
- `dotnet build src/OpenFill.App/OpenFill.App__v0.3__2026-10-12_2034.csproj -c Release --no-restore -v:q`: 0 errors, 6 istniejących warnings.
- Harmonogram `OpenFill-OF012-WebView2-Smoke` pozostaje ręczny, skrypt `run_interactive.ps1` przywrócony do trybu BEZ `--effect-dev`. Brak automatycznego wykonywania efektów lub wywołań modelu.
- Nadal po sukcesie występuje `ARCHFLOW_WEBVIEW2_PROFILE_DEFERRED: Access to ... BrowserMetrics*.pma is denied`. Przeszkoda dotyczy tylko sprzątania efemerycznego profilu, exit=0; nie ustalono, który proces utrzymuje uchwyt.

## SHA256 zweryfikowane na PBOX1

- `LocalPlanEffectJournal__v0.1__2026-11-02.cs`: `33D830807ECC3A1B55453A2D32A223701880B834F14FA04D450A57C3123E67D0`
- `LocalPlanWebView2EffectDevAdapter__v0.1__2026-11-03.cs`: `18ABAE22E6FF181716F6F482BFBE102EFD290B58C201AB728B34B7DA27EBB686`
- `EffectDevSmoke.cs`: `169AD48F59AD9008DC2BCDD8C32377612BAECBF1E827D32CA303EF3F7D77E3B3`
- `LocalPlanEffectExecutorSmoke.cs`: `5277B5F61A1A3D776834B2C3D0B19ADD664E284F4FBF10E8A24B43915937DA69`

## Granica realizacji i kolejny etap

To rzeczywiste kliknięcie na lokalnym fixture, a nie zapis do rzeczywistego serwisu. Dowód bazuje na niezależnym DOM i utrwaleniu SQLite. Produkcyjny ToolRegistry i `AgentRunner.RunAsync` nie zostały zmienione; w `of.plan.v1` nadal nie wolno wykonywać rzeczywistych `send/save/submit`.

Następny etap: jawne węzły efektowe z `effect_id` i `postcondition` w ograniczonym protokole DEV, powiązanie trwałego journalu z planem oraz testy replan/uncertain bez ponawiania. Potem dopiero włączanie rzeczywistych providerowych adapterów i opt-in ścieżki LIVE.
