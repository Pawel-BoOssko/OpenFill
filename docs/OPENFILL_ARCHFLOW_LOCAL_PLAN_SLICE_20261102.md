# OpenFill OF-000009: lokalny egzekutor planów, pionowy slice DEV

Data: 2026-11-02. Status: SOURCE TESTED, brak integracji z LIVE.

## Pliki i granice

- `src/OpenFill.Core/Agent/LocalPlanExecutor__v0.1__2026-11-02.cs`: `PlanProtocolValidator`, `SemanticLocatorResolver`, `LocalPlanExecutor`, `ILocalPlanBrowser`, struktury planu, snapshotu i wyników.
- `tools/of_imp_smoke/LocalPlanSmoke.cs`: deterministyczna symulacja listy i formularza.
- `tools/of_imp_smoke/Program.cs`: przełącznik `archflow-local-plan-only`.
- Test: `dotnet run --no-restore --project .\\tools\\of_imp_smoke\\FileFilterSmoke.csproj -- archflow-local-plan-only`.
- Wynik: `ARCHFLOW_LOCAL_PLAN_26_CHECKS_OK`, exit_code 0.

To jest ograniczony podzbiór docelowego `of.plan.v1`, nie pełny executor DSL. Nie ma jeszcze `foreach`, `if`, `fill`, `wait_until`, `effect journal`, integracji z WebView2, generowania planu przez LLM ani obserwacji realnej strony.

## Akceptowany kontrakt JSON

```json
{
  "protocol": "of.plan.v1",
  "expected_host": "example.test",
  "nodes": [
    {"id":"n1","op":"locate","role":"link","name":"Customer 42","bind":"row"},
    {"id":"n2","op":"click","ref":"$row","effect":"navigation"},
    {"id":"n3","op":"assert_visible","role":"textbox","name":"Customer name"}
  ]
}
```

Validator: nieznane pola odrzucane; wymagane typowane wartości string; identyfikatory node i bind unikalne; binding musi istnieć przed kliknięciem; maksymalnie 30 node, maksymalnie 12 click; po każdym click bezpośredni `assert_visible`; kliknięcie dopuszczalne wyłącznie dla lokatora o roli `link` i `effect=navigation`. `send_message`, `save`, `run_js`, `button` i inne side effects odrzucane. Każda zmiana protokołu wymaga osobnego validatora.

## Wykonanie i semantyka

1. `ReadAsync` przed każdym node: bieżący host, rewizja, elementy role/name/ref/enabled.
2. `locate` wymaga dokładnie jednego pasującego elementu; zapisuje **semantyczny locator**, nie referencję DOM z poprzedniej rewizji.
3. `click` ponownie odczytuje i rozwiązuje locator, wymaga `enabled` i `expectedRevision`. Adapter musi sprawdzić rewizję przed akcją. Wynik `applied` jest tylko `applied_unverified`.
4. `assert_visible` po click wymaga nowszej rewizji snapshotu oraz jednoznacznego elementu postcondition. Bez tego wynik `needs_replan`, nie powtórzenie kliknięcia.
5. `stale` lub `failed_before_action`: `needs_replan`; `uncertain` lub wyjątek podczas kliknięcia: `uncertain_effect`; nigdy auto-retry.
6. Zmiana hosta, cofnięcie rewizji, brak, niejednoznaczność, disabled lub niedostępny snapshot: stop z jawnym reason_code.
7. Testy fake browser weryfikują dokładnie jedno kliknięcie w sukcesie, zero kliknięć dla błędów preflight i brak ponowień dla wszystkich błędów po próbie kliknięcia.

## Wyniki testów

26 scenariuszy: poprawna lista -> link -> formularz; ambiguous, missing, disabled, host mismatch, invalid revision, read exception, regression revision; stale, failed before action, uncertain, action exception, postcondition missing/ambiguous, host changed after click, unchanged revision; duplicate node, forbidden effect, unknown binding, forbidden button click, unknown op, missing postcondition, unknown JSON field, wrong protocol, over-limit node count, invalid host.

Weryfikacja w symulatorze nie dowodzi działania w prawdziwej przeglądarce ani oszczędności model calls. Żaden plan nie został wykonany w LIVE.

## Następny etap

1. Zaimplementować adapter `ILocalPlanBrowser` oparty na WebView2/CDP, wyłącznie w DEV, z aktualnym semantic snapshot i rewizją opartą na zmianie obserwowanego stanu, nie na liczbie odczytów.
2. Dodać fixture HTML: lista z linkiem -> formularz, test integracyjny na lokalnym host, test race stale/redirect, bez kont zewnętrznych.
3. Rozszerzyć DSL o bezpieczne `fill`, `wait_until`, `if`, `foreach`, a przed jakąkolwiek akcją wysłania/zapisu wdrożyć trwały `EffectJournal` i jawne `business_operation_id`.
4. A/B: ten sam cel i potwierdzenie efektu, osobno liczyć model calls, DOM reads, browser actions, niepewne wyniki i koszt; nie utożsamiać teoretycznego baseline z oszczędnościami.
