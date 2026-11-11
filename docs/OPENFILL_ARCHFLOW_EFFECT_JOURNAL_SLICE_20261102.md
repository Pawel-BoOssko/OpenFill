# OF-000010: trwały EffectJournal, SOURCE

Data: 2026-11-02 (Europe/Warsaw). Warstwa przygotowawcza, bez aktywacji w LIVE.

## Zmiany

- `src/OpenFill.Core/Agent/LocalPlanEffectJournal__v0.1__2026-11-02.cs`: typowany `LocalPlanEffectKey` oraz `LocalPlanEffectJournal`.
- Reużycie istniejącego `OperationalMemoryStore` i jego tabel SQLite WAL (`batches`, `batch_request_fingerprints`, `operations`, `operation_events`). Nie utworzono osobnej bazy.
- Idempotencja jest przypisana do `provider + account_id + business_operation_id` i trwałego odcisku `provider/account/target/action/payload_sha256`. Zmiana operacji pod tym samym identyfikatorem biznesowym oznacza konflikt.
- `task_id` i `run_id` zachowane w metadanych batch jako kontekst pierwszego przebiegu, nie są kluczem idempotencji.
- Dozwolone typy efektu: `send`, `save`, `submit`. Nie są wykonywane przez ten moduł.
- `TryBeginAttempt` utrwala `attempted` przed wywołaniem adaptera; kolejne próby, także po ponownym uruchomieniu, zwracają false.
- `Verify` wymaga referencji dowodowej, `MarkUncertain` zapisuje przyczynę. Brak automatycznego retry dla `uncertain` i `verified`.

## Kontrola

`dotnet run --no-restore --project tools/of_imp_smoke/FileFilterSmoke.csproj -- archflow-effect-journal-only`

Test: osobny identyfikator operacji, zgodność odcisku, konflikt payload, trwałość próby i zakaz ponowienia po reinstancjacji store, `uncertain`, `verified`, audit trail.

## Pozostaje

Moduł NIE jest podłączony do `LocalPlanExecutor` ani `ToolRegistry`. DSL do tej pory wykonuje tylko nawigację. Przed włączeniem rzeczywistych `send/save` należy zintegrować journal z business operation identity, dodać dowody postcondition i uzgodniony mechanizm ręcznej rekoncyliacji dla `uncertain`. Nie oznaczać tego slice jako gotowości produkcyjnej.
