# OpenFill PBOX1: wdrożenie sterowania bez Luny, rejestru SQLite i bezpieczników zadań

**Autor i wykonawca:** GPT-6  
**Data i strefa:** 2026-11-12, CET (Europe/Warsaw)  
**Host produkcyjny:** PBOX1  
**Jedno repozytorium:** https://github.com/Pawel-BoOssko/OpenFill  
**Gałąź:** feature/openfill-telegram-20261111, bez tworzenia nowej

## Wynik produkcyjny

- Instalacja: OpenFill 1.63, `1.63+8f3a45d6a96fff07c8609eef7057f2639037c6fe`.
- Podstawowy commit bezpieczeństwa: `8f3a45d6a96fff07c8609eef7057f2639037c6fe`.
- Dodatkowy commit instalatora i klienta: `02f80cafbe89f5ddf526ce3db2def13c69a08e81`, wysłany do tej samej gałęzi GitHub. Dopisane narzędzia nie zmieniają uruchomionego już EXE 1.63.
- Instalator: `DEPLOY_OK version=1.63 session=1 mcp=ok`, wynik harmonogramu 0.
- API bez Luny: zdrowie HTTP 200 na 127.0.0.1:17873, dostępne sterowanie kartami przez JSON, nawigacja, JavaScript, DOM, zrzuty, sekwencje.
- MCP: `openfill_start_task` zawiera `max_cost_usd`, `max_duration_seconds`, `max_idle_seconds`. Zweryfikowano na uruchomionej wersji narzędziem `tools/list`.
- Odrzucenie niepoprawnego kosztu: RPC `max_cost_usd=0` zwróciło komunikat walidacji oraz `Nothing was started`.

## Jak zlecać zadania

Dodatkowe pola w MCP:
```json
{
  "task": "Zbadaj stronę i wykonaj wskazane działania",
  "requester": "GPT-6",
  "chat": "Nazwa zlecającego czatu",
  "max_cost_usd": 0.05,
  "max_duration_seconds": 300,
  "max_idle_seconds": 90
}
```

Skrypt lokalny:
`C:\Users\pboru\Documents\Projects\OpenFill-SelfChatSteering-Current-20261110\tools\direct-browser\2026-11-12_15-53-00_CET_GPT-6_OpenFill_ZlecajMCPZLimitemKosztuCzasuBezNadzoru_PBOX1.ps1`.

Użycie:
```powershell
$tool = 'C:\Users\pboru\Documents\Projects\OpenFill-SelfChatSteering-Current-20261110\tools\direct-browser\2026-11-12_15-53-00_CET_GPT-6_OpenFill_ZlecajMCPZLimitemKosztuCzasuBezNadzoru_PBOX1.ps1'
& $tool -Task 'Zbadaj podaną stronę' -Requester 'GPT-6' -Chat 'Badanie' -MaxCostUsd 0.05 -MaxDurationSeconds 300 -MaxIdleSeconds 90
```


## Reakcja automatyczna

1. Szacowany koszt modelu zostaje naliczony po odpowiedzi; przekroczenie wartości odcina następny krok. Jedno wywołanie będące w toku może przekroczyć podany pułap.
2. Niezależny zegar liczy maksymalny czas od przyjęcia zadania, obejmując przygotowanie i oczekiwanie na człowieka.
3. Watchdog sprawdza co 500 ms czas bez wykonania kolejnego kroku. Oczekiwanie na człowieka nie jest traktowane jako zawieszenie, ale ogólny limit czasu pozostaje aktywny.
4. Kończone jest wskazane zadanie; przeglądarka i inne zadania działają dalej.
5. Działają także dotychczasowe limity na domenę, limity pojedynczych operacji i detekcja zapętlenia.

**Granica egzekwowania:** token anulowania nie gwarantuje fizycznego przerwania natywnej operacji, która ignoruje anulowanie. Nie wdrożono odrębnego watchdoga procesowego zabijającego cały EXE.

## Testy

- Czysta kompilacja OpenFill 1.63 z dokładnego commita: PASS.
- Testy TaskSafety: PASS, w tym limity kosztu, zegar, bezczynność, oczekiwanie na człowieka i walidacja MCP.
- Testy DirectBrowser i SQLite: PASS.
- Testy TelegramGateway: PASS.
- Pełny zestaw regresyjny z lokalnego drzewa: 1478 PASS, 9 FAIL. Osiem niezgodności oczekiwanego promptu obecnych także w poprzednim commicie, dziewiąta dotyczy niezmienianej logiki WhatsApp. Nie ukryto tych rozbieżności.
- Wdrożenie 1.63 zintegrowane z istniejącym harmonogramem `OpenFill`; poprzedni katalog zachowany do rollbacku.
- Sprawdzono wersję EXE, aktywną sesję Windows 1, port MCP i port API bez Luny.
- Nie uruchamiano kosztownego zadania produkcyjnego tylko na potrzeby testu limitów.

## Pliki

- Zródła Git: C:\Users\pboru\Documents\Projects\OpenFill-SelfChatSteering-Current-20261110
- OpenFill EXE: `C:\Users\pboru\OpenFill\app\OpenFill.exe`
- Baza czatów: `C:\Users\pboru\OpenFill\data\stable\memory\chatgpt\2026-11-12_15-01-19_CET_GPT-6_OpenFill_ChatGPT_CzatyURLTytulOstatniDostep_RejestrAutomatyczny.sqlite`
- Uruchomienie direct API: `http://127.0.0.1:17873/health`
- Skrypt aktualizacyjny: `tools\deploy_safely__v0.1__2026-10-14_2235.ps1`
- Log wdrożenia: `artifacts\2026-11-12_15-50-00_CET_GPT-6_OpenFill_PBOX1_InstalacjaWatchdog163_WynikZdrowieIRollback.log`
- Dokument wymagań limitów: `docs\2026-11-12_15-36-00_CET_GPT-6_OpenFill_LimityKosztuCzasuWatchdog_PerTaskMCP_ZebyOdciacPetleZawieszenia.md`

Stan końcowy: instalacja produkcyjna 1.63 działa na PBOX1 i uwzględnia trzy limity zadania. Brak nowych gałęzi.

