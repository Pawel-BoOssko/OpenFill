# OpenFill: ISSUE_openfill_koszt_cache_20261022, diagnoza i poprawka źródeł

Wersja raportu 0.1, 2026-10-23 11:10 (PBOX1). Zgłoszenie źródłowe (niezmienione): `C:\Users\pboru\OpenFill\shared\ISSUE_openfill_koszt_cache_20261022.md`.

## Reprodukcja historyczna, 22.10
- Log: `C:\Users\pboru\OpenFill\data\stable\logs\runs\run_20261022_144008_7a39b4.ndjson`, 3198 linii (odczyt bez zmiany).
- Zadanie `OF-20261022-1640-de9d`, 14 zaproszeń LinkedIn, model gpt-6-luna reasoning medium, OpenFill 1.55. Zgłaszający podaje 166 wywołań, $0.39, 10.16 mln tokenów wejścia, 7.0 mln cache / 3.15 mln uncached. 110 zdarzeń starej kompakcji. Cel odbioru: koszt najwyżej $0.05 oraz cache >=90% poza świadomą kompakcją.
- Stary `AgentRunner__v0.27__2026-10-18_0810.cs` utrzymywał wszystkie wcześniejsze reasoning + function_call + tool_output. Po przekroczeniu `HistoryCompactThresholdChars=240000` skracał jedynie dawne wyniki >4000 znaków, dodatkowo przepisywał stare screenshoty przy każdym kroku. To **powodowało trwały wzrost historii i zmianę wcześniej wysłanego prefiksu**, więc cache mógł przestawać trafiać.
- Istniejący w v0.27 `prompt_cache_key` był już generowany per instance/runner i stały w czasie zadania (`openfill-agent-` + 12 znaków Guid). Nie dodawano drugiego ustawienia klucza.
- Z historycznego NDJSON **nie da się uzyskać dokładnych bajtów kroku 57/58 ani 95/96**: kod logował tylko rozmiar requestu i usage, nie pełne request JSON. Nie rekonstruować rzekomo bajtowej różnicy z samych tokenów.

## Zmiana SOURCE, 23.10
- `src/OpenFill.Core/Agent/AgentRunner__v0.28__2026-10-23_1055.cs` (zastępuje v0.27, bez edycji `SystemPrompt` i limitów kosztowych).
- `src/OpenFill.Core/Agent/PromptHistoryCompactor__v0.1__2026-10-23_1054.cs` (nowa klasa).
- Granica wywołania kompakcji jest ograniczona przez min(konfigurowany próg, 110000 znaków historii JSON); po kompakcji celem jest ok. 72% tej wartości. Próg jest **przybliżeniem znakowym**, nie gwarancją liczby tokenów. Bardzo duża bieżąca obserwacja lub obraz może przekroczyć cel.
- Zamiast przewijającego się przepisywania starych wyników, usuwane są **całe zakończone cykle**: output modelu (reasoning, tool_call), wszystkie odpowiadające `function_call_output` i screenshoty. Zachowane są co najmniej dwa ostatnie cykle, pełne pierwotne zadanie oraz jeden zwarty checkpoint z historią ostatnich 55 wyników narzędzi (ograniczenie 8500 znaków). Stary checkpoint jest wymieniany tylko przy następnej kompakcji. Pomiędzy kompakcjami historia jest append-only.
- Checkpoint wyraźnie oznacza fragmenty z witryn jako nieufne dane, nie potwierdzenie skutków zewnętrznych. Dla wyciętych szczegółów agent ma `search_logs` i weryfikację stanu strony. Nie wprowadzano ograniczeń lub zmian do instrukcji Luny.
- Telemetria per call `cacheFraction`, `historyCompactions`, `firstChangedInputChar` oraz **`firstChangedRequestByte`**: porównanie bajt po bajcie UTF-8 JSON faktycznie przekazywanego `OpenAIClient`. Do logu trafia wyłącznie offset/liczby, bez payloadu, tokenów auth, URL z sekretami lub treści.

## Testy i faktyczna kwalifikacja
- Dodano `tests/OpenFill.Tests/PromptHistoryTests__v0.1__2026-10-23_1100.cs`: 100 syntetycznych cykli reasoning + call + output, wiele checkpointów, historia po kompakcji ograniczona, oryginalne zadanie zachowane, brak osieroconych wyników narzędzi, zachowane najnowsze obserwacje.
- `tests/OpenFill.Tests/Program__v0.23__2026-10-23_1102.cs`: dołącza nowy test do całej regresji, stary plik Program usunięty.
- Build całego rozwiązania `-c Release --no-restore`: **0 errors, 1 known warning MSB3277**. Full `dotnet run ... OpenFill.Tests__v0.3...csproj -c Release --no-restore`: **1192 PASS, 0 FAIL**, powtórzone po dołożeniu diagnostyki bajtowej, obydwa przebiegi 1192/0.
- **SOURCE/BUILD/TEST jest gotowe. STAGED/DEPLOYED/LIVE niepotwierdzone.** Produkcyjnego OpenFill 1.55 nie restartowano i nie wykonywano nowej płatnej kampanii zaproszeń.

## Bramka odbioru na prawdziwej usłudze
1. Staging z niezależnym profilem, małe **read-only** powtarzalne zadanie testowe bez wysłania żadnego zaproszenia; zebrać usage z serwera, nie szacować cache z długości JSON.
2. Upewnić się, że poza świadomą kompakcją `cached_tokens/input_tokens >= 0.90` po rozgrzaniu prefixu. Porównać `firstChangedRequestByte` w momentach spadku cache i zmian `historyCompactions`. Wymagana analiza tokenów, nie sam offset.
3. Test jakości wieloetapowego workflow na lokalnej stronie testowej z powtarzalnymi 14 celami oraz zapisem weryfikowanego postępu. Sprawdzić, że po kompakcji agent nie ponawia ukończonych działań ani nie gubi celu.
4. Dopiero po staging i zaakceptowaniu wyników: kontrolowane wydanie następnej wersji z kopią całego `app`, rollback, smoke MCP i GUI. `data\stable` nie dotykać.
5. Ostateczne kryterium kosztu <= $0.05/14 zaproszeń można sprawdzić tylko na porównywalnym realnym workflow i znanych rzeczywistych cenach modelu; przed jego uruchomieniem upewnić się, że nie spowoduje niezamówionych zaproszeń.

## Zasady operacyjne
- Wyłącznie Desktop Commander do zmian, żadnych skryptów modyfikujących źródła przez PowerShell lub lc-multihost.
- Żadnych automatyzacji ChatGPT ani ręcznych git commit/push; istniejący Autosync działa sam, bez ręcznego uruchamiania.
- Nie modyfikowano produkcyjnego `data\stable`, nie logowano request bodies.
- Status: **przyczyna w implementacji potwierdzona i poprawiona w źródłach; koszt/cache LIVE pozostaje do odbioru**.
