# OF-187: audyt składu repozytorium OpenFill

Data: 2026-10-18. Audyt wyłącznie **nazw śledzonych plików**, bez odczytu sekretów, poświadczeń, profili ani danych produkcyjnych.

## Sprawdzony punkt odniesienia

- PBOX1, lokalny read-only `git ls-tree -r --name-only refs/remotes/origin/autosync/PBOX1`.
- `git rev-parse refs/remotes/origin/autosync/PBOX1` = `03c52d693810e64571dddaf37d1ea4314eceefee`.
- `git ls-remote origin refs/heads/autosync/PBOX1` = `03c52d693810e64571dddaf37d1ea4314eceefee`, zgodność z lokalnym remote-tracking ref w chwili odczytu.
- Liczba wersjonowanych plików: **225**.
- Wzorzec typowych plików produkcyjnych/binarnych: `bin/`, `obj/`, `data/stable/`, `profiles/`, `User Data/`, `*.sqlite`, `*.pfx`, `*.pem`, `*.key`, `*.age`, `*.ndjson`.
- Dopasowania: **1**. Jedyny plik to `tests/OpenFill.Tests/telemetry_quality_fixture__v0.1__2026-10-18.ndjson`, syntetyczne dane testowe zapisane w tej sesji. Nie jest to log środowiska produkcyjnego.
- `*.exe` / `*.dll` w drzewie śledzonych plików: **0**.

## Zakres i ograniczenia

Kontrola dotyczy wyłącznie **nazw w tej jednej gałęzi i chwili odczytu**. Nie jest skanem treści pod kątem sekretów ani audytem nieśledzonych plików lokalnych, wszystkich historycznych commitów, innych branchy czy kopii w Google Drive. Nie dowodzi także, że dowolny plik bez ryzykownej nazwy nie zawiera tajnych danych. Z tego powodu OF-187 pozostaje otwarty do niezależnego audytu treści i procesu release bez ujawniania sekretów.

## Dalsza realizacja

Przy wydaniu sprawdzać listę plików **paczki zbudowanej**, nie tylko repozytorium źródeł. Potwierdzić, że pakiet nie zawiera profili WebView2, credential vault, cookies, danych aplikacji ani surowych logów. Nie kopiować plików z lokalnego `data/stable` do repo lub publicznych artefaktów.
