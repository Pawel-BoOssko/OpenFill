# OF-239: audyt istniejącego watchdog i luki MCP

Data: 2026-10-18, kontrola wyłącznie do odczytu po wydaniu 1.55.
Źródła: `tools/watchdog-openfill__v1.0__2026-10-07_1432.ps1`, `docs/openfill_watchdog_mcp_gap__v0.1__2026-10-17_1232.md`, `docs/openfill_watchdog_handoff_audit__v0.1__2026-10-17_1535.md`.

## Potwierdzone
- Na PBOX1 `Get-ScheduledTask` nie znalazł zadania Harmonogramu o nazwie OpenFill-watchdog/watchdog-OpenFill (liczba 0).
- Skrypt `watchdog-openfill` jedynie sprawdza proces i port TCP oraz może wymusić jego zakończenie, gdy port nie odpowiada dłużej niż 3 minuty. Nie obsługuje JSON-RPC, `openfill_start_task` ani `openfill_task_status`, nie podaje klientom `maintenance`.
- Skrypt ma limiter do pięciu restartów na godzinę i wyłącza restart podczas uruchomionego instalatora pasującego do `install__`/ `install-tester__`. Nowy skrypt `install__v0.13__2026-10-18_1650.ps1` pasuje do istniejącego filtra.
- Protokół zastępczy, jego przekazanie tego samego portu nowej wersji i realne testy izolowane pozostają otwarte; w tej sesji nie bindowano portu ani nie włączano watchdog.

## Wymagany odbiór
1. Stworzyć zastępczy serwer MCP w odizolowanym środowisku testowym, bez ingerencji w działający port 1.55.
2. Dla `openfill_start_task` zwracać wyłącznie `maintenance` bez startu, zachowując zgodny format JSON-RPC, requester/chat.
3. Dla `openfill_task_status` zwracać `maintenance` z tym samym `task_id`, szacowanym pozostałym czasem przerwy i long-poll maks. 30 s.
4. Sprawdzić wejście/wyjście procesu zastępczego oraz handoff portu po starcie OpenFill, bez kolizji i utraty żądań.
5. Odrębny realny test SessionId 1, rollback i zdrowie MCP na tej samej zewnętrznej trasie.

Poprzednia konkretna zmiana zastępczego respondenta MCP była odrzucona przez narzędzie, więc jej nie ponawiano innym narzędziem lub jako obejście. Status: ANALIZA POTWIERDZONA, SOURCE/TEST PROTOKOŁU NIEWYKONANE.
