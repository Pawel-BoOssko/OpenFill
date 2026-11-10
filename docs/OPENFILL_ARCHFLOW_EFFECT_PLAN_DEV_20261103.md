# OpenFill OF-000018: jawne węzły efektowe w planie DEV, z trwałą kontrolą rezultatu

Wybudzenie: `OF-000018-A01-004632`, start silnika 2026-11-03 00:46:32 +01:00 (Warszawa). PBOX1, SOURCE i DEBUG wyłącznie. Brak zmian LIVE.

## Cel i wykonany kod

- Dodano `src/OpenFill.Core/Agent/LocalPlanEffectPlanDevRunner__v0.1__2026-11-03.cs`, objęty `#if DEBUG`. Jawna metoda przyjmuje JSON z `protocol:of.plan.v1` zawierający zwykłe odczytowe i nawigacyjne węzły oraz węzły `op:effect`.
- Dla `effect` wymagane dokładnie `id,op,effect_id,target_id,action,payload_sha256,postcondition`. `postcondition` wymaga `type:adapter_evidence` i oczekiwanego konkretnego `expected`. Zablokowano nieznane pola i duplikaty ID, przekroczenie liczby węzłów oraz sekwencje z niedokończonym warunkiem.
- Bezpieczna ścieżka nie przyjmuje arbitralnych providerów: host `127.0.0.1`, adapter `of-local-fixture` / `dev-session`, wyłącznie `save-a` i `save-b`, `target_id:a/b`, `action:save`, hash weryfikowany z `value-a` lub `value-b`, oczekiwany dowód `local-effect:a:count=1` lub `local-effect:b:count=1`.
- Zanim wykona pierwszą akcję, egzekutor analizuje **wszystkie** węzły. Zwykłe fragmenty między efektami są walidowane przez `PlanProtocolValidator.Parse` (m.in. wymagane `wait_until` po nawigacji i `assert_value` po fill). Zagnieżdżone `if/foreach` wykorzystują poprzedni read-only DSL. Węzły są ograniczone do max 12 na poziomie planu, max 20 ID z gałęziami, max 2 efekty.
- Wykonanie efektu: sprawdzenie aktualnego hosta przez `ILocalPlanBrowser.ReadAsync`, `LocalPlanEffectExecutor`, trwały zapis `attempted` przez `LocalPlanEffectJournal`, dokładnie jedno wywołanie adaptera, oddzielny odczyt `ObserveAsync` i porównanie z `postcondition.expected`, następnie `verified` albo `uncertain`.
- Po `already_recorded` lub `uncertain_effect` egzekutor zatrzymuje cały plan, bez powtórnego kliknięcia i bez przechodzenia do kolejnych efektów. Brak automatycznej próby naprawy niepewnego skutku.
- `AgentRunner__v0.29__2026-10-29_2314.cs`: nowa metoda `RunEffectPlanDevAsync` wyłącznie DEBUG, przyjmująca plan dostarczony przez kod DEV, bez Responses API. Zapisuje zdarzenie `archflow/effect_plan_dev` w EventLog. Standardowy `RunAsync`, ToolRegistry i produkcyjny `PlanProtocolValidator` bez zmian. Produkcyjny walidator **nadal odrzuca** `op:effect`.

## Test rzeczywistego WebView2

- Rozszerzono `tools/of_archflow_webview2_smoke/EffectPlanDevSmoke.cs` oraz `Program.cs` (opcjonalny `--effect-dev`): plan `if(button Save a) -> effect save-a -> effect save-b` na lokalnym fixture `http://127.0.0.1:<losowy_port>/effects`. Dwa kliknięcia faktyczne przez `WebView2.ExecuteScriptAsync`.
- Test najpierw podaje plan ze zmienionym hashem drugiego efektu, potem ze skutkową akcją w gałęzi `if`: oba są odrzucone **przed pierwszym kliknięciem**, co potwierdza niezależny odczyt DOM `count-a=0,count-b=0`.
- Poprawny plan wykonano przez `AgentRunner.RunEffectPlanDevAsync`, otrzymano dwa `verified`, dwa odmienne `operationId`, dwa dowody `local-effect:a:count=1` oraz `local-effect:b:count=1`; niezależny DOM potwierdził `1/1`.
- Odtworzono SQLite przez nowy `OperationalMemoryStore` i ponowiono ten sam plan: otrzymano `already_recorded`, zero nowych prób i nadal `count=1/1`.
- Osobny scenariusz z prawdziwym kliknięciem pierwszego przycisku, lecz adapterem celowo niedostarczającym dowodu: wynik `uncertain_effect`, stan `uncertain` utrwalony, drugi efekt nie wykonany, DOM `1/0`. Ponowienie po nowym otwarciu SQLite daje `already_recorded`, DOM `1/0`.
- Weryfikacja: `ARCHFLOW_EFFECT_PLAN_DEV_19_CHECKS_OK`, `ARCHFLOW_EFFECT_WEBVIEW2_14_CHECKS_OK`, `ARCHFLOW_WEBVIEW2_EMBEDDED_20_CHECKS_OK`, `interactive.exit=0`. Środowisko Windows SessionId=1. `ModelCalls=0`.
- Drugi przebieg całego testu po dopisaniu metody w AgentRunner również zakończył się PASS, exit 0.
- Kompilacja DEBUG harness: 0 błędów, 9 wcześniejszych ostrzeżeń. Kompilacja aplikacji `dotnet build OpenFill.App -c Release --no-restore`: 0 błędów, 6 wcześniejszych ostrzeżeń, exit 0.
- Po wykonaniu testu `run_interactive.ps1` przywrócono do wywołania bez `--effect-dev`, tak aby ręczne uruchomienie zadania nie wykonywało efektów. Harmonogram pozostaje manualny.
- Znany nieblokujący problem sprzątania profilu WebView2: `BrowserMetrics*.pma Access denied`, exit 0. Przyczyna blokady uchwytu nieustalona.

## SHA256 (Get-FileHash, PBOX1)

- `LocalPlanEffectPlanDevRunner__v0.1__2026-11-03.cs`: `CB20159E0E8C3F17B396D8A8B3C73CC465B53A235AC7423F3D7B8B5A52AD155A`
- `AgentRunner__v0.29__2026-10-29_2314.cs`: `593A8A39050126CFD0A6098D9806C12EB1FE62E4D5C0C1F4B61CFF93110A9E2E`
- `EffectPlanDevSmoke.cs`: `1640DF936D67553601833C0B0F8DED9F29525D090A21828FBFB29A1D435C3E9C`
- `Program.cs` w `tools/of_archflow_webview2_smoke`: `B278B416E26BDBA4AFDCCE141128580535743B99291DF3E4BB8AFA04896D2AFD`

## Co pozostało

Obecna ścieżka umożliwia walidację i wykonanie pojedynczych *z góry zadeklarowanych* efektów DEV tylko na localhost. Nie umożliwia modelowi generowania ani wykonywania efektów na produkcyjnych witrynach. `RunPlannedDevAsync` nadal generuje zwykłe plany bez skutków. Dalszy etap: przygotować minimalną abstrakcję providerowego `postcondition` oraz jawne przełączanie trybu planowania efektów, z dowodem rzeczywistym i bez automatycznego retry. Bez wiarygodnego dowodu nie aktywować ścieżki w LIVE.
