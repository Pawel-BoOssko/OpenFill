# OpenFill, checkpoint v1.3, 2026-10-23 11:13 CEST

## Zadanie
Paweł przekazał zgłoszenie `C:\Users\pboru\OpenFill\shared\ISSUE_openfill_koszt_cache_20261022.md`. Zadanie z 22.10, 14 zaproszeń, 166 requestów, raportowany koszt $0.39, rosnąca historia i ~42 wywołania ze stałym 5366 cached tokens.

## Wykonane w SOURCE
1. Odczytano zgłoszenie i istniejący `AgentRunner__v0.27__2026-10-18_0810.cs`. Potwierdzona wada: po progu 240k znaków stary kod wyłącznie obcinał wyniki narzędzi >4000 znaków, nigdy nie usuwał par function_call + output + reasoning, a przy kolejnych krokach przepisywał historię. Klucz cache był już osobny na każdy obiekt AgentRunner, nie ruszano go.
2. W miejsce v0.27 wprowadzono `src\OpenFill.Core\Agent\AgentRunner__v0.28__2026-10-23_1055.cs` i nowy `src\OpenFill.Core\Agent\PromptHistoryCompactor__v0.1__2026-10-23_1054.cs`. Usuwane są zakończone cykle w całości, najnowsze 2 pozostają, zwarta notatka z ostatnimi wynikami + oryginalny prompt. Pomiędzy kompakcjami prefiks wejścia się nie zmienia, jeśli jedynie dopisuje się nowe cykle.
3. Trigger min(konfigurowany próg, 110000 znaków), po kompaktowaniu cel ~72% tej wartości. Nie jest to gwarancja liczby tokenów ani kosztu; 2 ogromne najnowsze cykle mogą przekroczyć znakowy limit. Szczegóły starszych działań pozostają w `search_logs` i wymagają weryfikacji na stronie.
4. Telemetria per request: cacheFraction, historyCompactions, firstChangedInputChar, firstChangedRequestByte. Ten ostatni mierzy pierwszy różniący się bajt UTF-8 JSON przekazywanego OpenAIClient, bez zapisywania requestu (i sekretów). Brak historycznych pełnych requestów 22.10 uniemożliwia rzetelne bajtowe porównanie kroku 57/58 wstecz.
5. Test `tests\OpenFill.Tests\PromptHistoryTests__v0.1__2026-10-23_1100.cs` z 100 syntetycznymi turami, brak osieroconych wyników, zachowanie zadania, checkpointy, ograniczona historia. Aktualny `Program__v0.23__2026-10-23_1102.cs` rejestruje test.

## Dowód
- `dotnet build OpenFill.sln -c Release --no-restore`: **0 errors, 1 warning MSB3277**, exit 0, po ostatecznej zmianie.
- `dotnet run --project tests\OpenFill.Tests\OpenFill.Tests__v0.3__2026-10-14_2259.csproj -c Release --no-restore`: **1192 PASS, 0 FAIL**, powtórzone po ostatniej poprawce telemetrii, exit 0. Brak prawdziwego wywołania płatnego API w tym teście.
- Opis szczegółowy `docs\OpenFill__issue_cache_cost_resolution__v0.1__2026-10-23_1110.md`. Do oryginalnego zgłoszenia w `shared` dodano krótką odpowiedź SOURCE/TEST.
- Backlog v1.7, A7: nowy wpis **OF-241** SOURCE/TEST PASS, LIVE COST ACCEPTANCE PENDING.

## Stan produkcji i otwarte kryteria
- Odczyt `openfill_info` 23.10 o 11:11: **OpenFill 1.55**, build 18.10 14:25, maintenance=false, 1 aktywne zadanie. Nie uruchamiano instalatora, nie zatrzymywano aplikacji, nie modyfikowano `data\stable`.
- **NIE ZWERYFIKOWANO** realnego wyniku <=$0.05/14 zaproszeń, cachedTokens>=90% w każdym kroku poza kompaktowaniem oraz input<=30–40k tokenów dla każdej sytuacji. Wymagane: odizolowane staging, mały read-only test na realnym modelu, ocena zachowania po kompaktowaniu; dopiero potem kontrolowane wydanie i porównywalne E2E.
- Źródła i testy są lokalne; ich pojawienie się na gałęzi `autosync/PBOX1` wymaga potwierdzenia zwykłym read-only odczytem po uruchomieniu istniejącego Autosync.

## Zasady
Nie uruchomiono automatyzacji ChatGPT ani ręcznego push/commit, nie wywołano zadań Harmonogramu i nie uruchomiono płatnej kampanii. Edycja przez Remote Desktop Commander; testy przez `dotnet`. Brak odmów bezpieczeństwa i brak zablokowanych komend w tym cyklu.
