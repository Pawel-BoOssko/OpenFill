# OpenFill 1.64: LunaGuard, ograniczenia agenta

Data: 2026-11-12 18:19 CET. Autor: GPT-6.

## Przyczyna
W zadaniu wysłania dokładnie jednej wiadomości na jeden adres czatu ChatGPT Luna wykonywała niepotrzebne przeszukiwania logów i plików, choć samo wysłanie wiadomości nie wymagało tych czynności. W konfiguracji aplikacji standardowe 60 kroków z 5 przedłużeniami pozwalało na 360 kroków, o ile nie wykryto pętli.

## Implementacja
- Nowy `LunaTaskGuardProfile`: rozpoznaje jednoznaczne zlecenie wysłania dokładnie raz na jeden adres `https://chatgpt.com/c/...`.
- Tylko narzędzia przeglądarki, weryfikacji, dokończenia i pytania. `terminal_exec`, `search_logs`, `file_query` wyłączone również po stronie wykonania, nie tylko ukryte w schemacie narzędzi.
- Prompt nakazuje wysłać wiadomość raz, zweryfikować jej obecność w docelowym czacie i natychmiast zakończyć zlecenie. Ścieżki i inne projekty w wysyłanej wiadomości stanowią treść, nie dalsze polecenie działania.
- Zadanie jednorazowej wiadomości ma twardy limit 18 kroków bez przedłużania.
- Domyślne limity MCP dla takiego zadania: `max_cost_usd=0.04`, `max_duration_seconds=180`, `max_idle_seconds=90`.
- Domyślne limity MCP dla innych zleceń: `max_cost_usd=0.15`, `max_duration_seconds=900`, `max_idle_seconds=150`.
- Każde jawne ograniczenie podane przez zlecającego ma pierwszeństwo nad domyślnym odpowiednikiem.
- Próg kosztu jest sprawdzany po rozliczeniu odpowiedzi modelu, dlatego pojedyncze trwające wywołanie modelu może przekroczyć limit o własny koszt.

## Weryfikacja
- Kompilacja OpenFill.Core Release: 0 błędów, 5 ostrzeżeń istniejących poza nowym profilem.
- Testy izolowane `OpenFill.LunaGuard.Smoke`: 14/14 PASS.
- Modyfikacje nie obejmują niezatwierdzonych lokalnych usunięć plików mostu Telegrama, które muszą pozostać poza wydaniem.

## Wdrożenie, 2026-11-12 18:24 CET
- Git commit źródła: `e65a3ed68b80b56ecf94992964c2a254e06e6ba2`, istniejąca gałąź `feature/openfill-telegram-20261111`.
- Czysty odłączony Git worktree pozwolił wydać program razem z kodem Telegrama, bez niezapisanych lokalnych usunięć gatewaya.
- Build/publish OpenFill 1.64 wykonano na PBOX1. SHA256 pliku EXE: `D328AD12BBB72A25AC6BB0EE6B356426FF5BEAA5D65A82C831AF89E6AAE31CC1`.
- Instalacja przez interaktywną sesję Windows 1 i kontrolowany skrypt z rollbackiem: `DEPLOY_OK version=1.64 session=1 mcp=ok`.
- Wersja LIVE: `1.64+e65a3ed68b80b56ecf94992964c2a254e06e6ba2`. PID 20988, odpowiada, HTTP /health 200.
- Zadania Windows `OpenFill` i `OpenFillTelegramBridge`: Running. Telegram supervisor PID 12316, worker PID 18504.
- Nowe zadanie MCP `OF-20261112-1824-0b68` dostało automatycznie limity 0.15 USD / 900 s / 150 s, wykonało 4 kroki i zakończyło się statusem done; koszt 0.0012 USD.
- W teście odczytu strony model poprawnie podał jej tytuł, ale podał `brak H1`, co nie jest wiarygodnym odczytem standardowej strony example.org. To osobna kwestia poprawności ekstrakcji/odpowiedzi, nie dowód na błąd limitów.
- Poprzednia produkcja 1.63 zachowana w `C:\Users\pboru\OpenFill\app__rollback__2026-11-12_18-24-01_CET_GPT-6_OpenFill_v1.63_BinariaPrzedAktualizacja_DlaAutomatycznegoCofniecia`.
- Szczegółowy log wdrożenia: `artifacts\2026-11-12_18-22-00_CET_GPT-6_OpenFill_v1-64_PBOX1_BinariaLunaGuard_DlaBezpiecznegoWdrazania\2026-11-12_18-23-00_CET_GPT-6_OpenFill_v1-64_PBOX1_WdrozenieLunaGuard_WalidacjaIOpcjaRollback.log`.


## Ścieżki
- `src/OpenFill.Core/Agent/LunaTaskGuardProfile__v0.1__2026-11-12_1812.cs`
- `src/OpenFill.Core/Agent/AgentRunner__v0.29__2026-10-29_2314.cs`
- `src/OpenFill.Core/Mcp/McpTaskManager__v0.21__2026-10-28_2216.cs`
- `tests/OpenFill.LunaGuard.Smoke`
