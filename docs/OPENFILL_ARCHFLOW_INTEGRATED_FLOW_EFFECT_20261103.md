# OF-000016: scalony of.plan.v1 z if/foreach, granica efektów

Wybudzenie: `OF-000016-A01-000557`, 2026-11-03 00:05:57 +01:00 Warszawa. Host SOURCE: PBOX1.

## 1. Scalony plan

Zmodyfikowano `src/OpenFill.Core/Agent/LocalPlanExecutor__v0.1__2026-11-02.cs`:
- `of.plan.v1` przyjmuje `if` i `foreach` bez tworzenia drugiego planu i bez dodatkowego wywołania modelu.
- Walidacja węzłów korzysta z istniejącego `LocalPlanControlFlow.Validate`: gałęzie `then/else/body` zawierają tylko `assert_visible` lub `assert_absent`, bez `click/fill/save/send/submit`. Zakaz zagnieżdżania flow.
- Maksymalnie 4 węzły flow, `foreach.max_items <= 5`, maks. 4 asercje w body, łącznie maks. 30 unikalnych ID w planie (z ID asercji w gałęziach). Wymagana lokalna weryfikacja bieżących nazw i ról, brak indeksów DOM.
- `LocalPlanControlFlow` przyjmuje minimalną rewizję z sekwencyjnego egzekutora i zwraca ostatnio odczytaną rewizję; wprowadzono limit 80 odczytów DOM na wywołanie kontrolera flow. Współdzielona przeglądarka `ILocalPlanBrowser` daje prawdziwy pomiar w `LocalPlanDevPipeline`.
- Wyniki zagnieżdżonych asercji przechodzą do `SemanticPlanNodeResult` jako `observed`. Poprawiono parsowanie nazw dostępności zawierających dwukropek (`Status: Present`), bo dzielenie po dwukropkach myliło część nazwy ze statusem.
- Aktualizowano instrukcje `LocalPlanModelDevRunner`, aby model mógł w razie potrzeby planować ograniczone `if/foreach`, bez wymagania ich przy każdym zadaniu.

**Potwierdzona integracja**: jeden plan `if -> foreach -> locate link -> click -> wait_until -> locate textbox -> fill -> assert_value`, bez wywoływania modelu dla pojedynczych kroków.

## 2. Testy

- `ARCHFLOW_INTEGRATED_FLOW_18_CHECKS_OK`, exit 0, fake browser: gałąź prawdziwa i fałszywa, rozpoznawanie elementów, niejednoznaczność, zmiana hosta, brak ponawiania akcji, ograniczenia walidatora.
- `ARCHFLOW_WEBVIEW2_EMBEDDED_20_CHECKS_OK`, exit 0, faktyczny Microsoft WebView2 w Windows SessionId=1, lokalny serwer HTTP. `ARCHFLOW_DEV_METRICS`: flowReads=5, planReads=13, planModelCalls=0, planNavigations=1, planFills=1, observed=4. W tym teście `if/foreach` oraz edycja formularza były w tym samym `of.plan.v1`.
- Regresje: `ARCHFLOW_LOCAL_PLAN_26_CHECKS_OK` exit 0, `ARCHFLOW_PLANNER_79_CHECKS_OK` exit 0.
- `dotnet build src/OpenFill.App/OpenFill.App__v0.3__2026-10-12_2034.csproj -c Release --no-restore -v:q`: exit 0, 0 błędów, 6 wcześniejszych warnings. DEBUG harness: 0 błędów, 9 wcześniejszych warnings.

## 3. Trwałe operacje skutkowe

Nowy SOURCE `src/OpenFill.Core/Agent/LocalPlanEffectExecutor__v0.1__2026-11-03.cs`:
- Łączy istniejący `LocalPlanEffectJournal` (SQLite WAL) z oddzielnym `ILocalPlanEffectAdapter`.
- Przed wywołaniem `AttemptAsync` musi zapisać `attempted` w journal. Wywołanie nie występuje, gdy operacja została już rozpoczęta lub zakończona.
- Sam status `applied` nie oznacza sukcesu: `ObserveAsync` musi zwrócić konkretny `evidenceRef`. Dopiero wtedy journal zapisuje `verified`.
- Wyjątek, nieobserwowany skutek, odpowiedź niejednoznaczna albo przerwanie po początku próby powoduje zapis `uncertain`, nigdy automatyczny retry.
- `ARCHFLOW_EFFECT_EXECUTOR_19_CHECKS_OK`, exit 0: weryfikacja dowodu, zakaz podwójnej operacji po restarcie, brak ponowienia po niepewności i po wyjątkach.

**Ograniczenia**: brak podłączenia do produkcyjnych adapterów przycisków `send/save/submit`; testowany jest wyłącznie fake. Obecny `LocalPlanEffectJournal` przypisuje odcisk do jednego `business_operation_id`, więc więcej niż jeden odrębny skutek w tej samej operacji biznesowej wymaga jeszcze jednoznacznego `node_id/effect_id` w kluczu, bez kolizji. Nie deklarować tego jako wdrożenia produkcyjnego.

## 4. Diagnostyka

- W nowym smoke był niepoprawny wielowierszowy string C# (CS8997, CS1003); poprawiono składnię.
- Pierwszy przebieg testu potwierdził wykonanie planu, lecz błędnie policzył asercję dla nazwy `Status: Present`; poprawiono dekodowanie sufiksu statusu, 18 checks PASS.
- Znany problem sprzątania profilu WebView2 `BrowserMetrics*.pma Access denied` nadal występuje po zaliczonym teście, kod wyjścia pozostaje 0. Przyczyna blokady nie została ustalona.
- Nie zmieniano LIVE, nie wykonywano zewnętrznych wysyłek ani zapisów w kontach, nie uruchamiano kolejnego A/B i nie naliczano nowych wywołań modelu podczas tego wybudzenia.

## 5. SHA256 SOURCE

- `LocalPlanExecutor__v0.1__2026-11-02.cs`: `3C37C893B6CE4F72FFE9267089D06A71B92899C0B4A1350EB279CF4D0FEAB748`
- `LocalPlanControlFlow__v0.1__2026-11-02.cs`: `CF05A0E087F103F6CE855776E4BE1D40BAFB8F3A56E63171DDBC66CD71F93BB8`
- `LocalPlanEffectExecutor__v0.1__2026-11-03.cs`: `0D907FE04B1702092131D3D6431F053B68ED8388B6F56E8682D5D005E17540C0`
- `LocalPlanModelDevRunner__v0.1__2026-11-02.cs`: `EA36C6D4AD28E6C3364B0239E94B6F1D3AC9DE8D40BAB3DAEA3287E1C23DCAA7`

## 6. Następny etap

Sprecyzować trwałe `effect_id` lub `node_id` dla wielu efektów w jednej `business_operation_id`, podłączyć Journal do rzeczywistego DEV-only adaptera formularzy na localhost, i zweryfikować niezależny dowód wyniku po ponownym odczycie DOM. Dopiero potem rozważyć rejestrację `submit_plan` w produkcyjnym AgentRunner i ścieżkę LIVE.
