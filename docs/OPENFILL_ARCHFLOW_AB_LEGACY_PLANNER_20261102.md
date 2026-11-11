# OpenFill: OF-000015, A/B legacy AgentRunner kontra plan lokalny

Wybudzenie: `OF-000015-A01-234551`, 2026-11-02 23:45:51 +01:00 Warszawa.
Host: PBOX1. SOURCE i test DEV, bez wdrożenia LIVE.

## Cel i metoda

Dokładnie ten sam cel obu przebiegów: na lokalnej stronie testowej `127.0.0.1` otworzyć widoczny link `Customer 42`, na formularzu wpisać `AB verified` w textbox `Customer name` i zweryfikować wartość. Bez `save/submit/send` i bez sieciowych serwisów zewnętrznych.

Legacy: **rzeczywisty** `AgentRunner.RunAsync`, `ToolRegistry.Build()`, `BrowserController` i `OpenAIClient`, a nie symulacja kosztu. Planner: `AgentRunner.RunPlannedDevAsync` z tym samym `gpt-6-luna`, `OpenAIClient`, `LocalPlanModelDevRunner` i `LocalPlanExecutor`. Po każdej próbie niezależny bieżący odczyt formularza za pomocą `LocalPlanCdpBrowser` potwierdzał docelową wartość i ścieżkę `/form`. Każda próba rozpoczynała się od odrębnego wejścia na `/list`.

Test uruchomiony w interaktywnej sesji Windows 1. Wyniki z `ARCHFLOW_AB_RESULT` i `ab_of_000015_result.json`; `ARCHFLOW_AB_BOTH_RESULTS_VERIFIED`; `ARCHFLOW_WEBVIEW2_EMBEDDED_19_CHECKS_OK`, `EXIT=0`.

## Dane zmierzone, jedna para

| Metryka | Legacy | Planner |
|---|---:|---:|
| Końcowy stan DOM | poprawny | poprawny |
| Wywołania modelu, prawdziwy Responses API | 5 | 1 |
| Tokeny wejściowe | 39183 | 625 |
| Tokeny wejściowe z cache | 31015 | 0 |
| Tokeny wyjściowe | 354 | 281 |
| Tokeny łącznie | 39537 | 906 |
| Wielkość żądań, znaki JSON | 262628 | 2746 |
| Czas od rozpoczęcia przebiegu | 17763 ms | 5468 ms |
| Wywołania narzędzi legacy | 5 | nie dotyczy |
| Lokalny licznik snapshotów planu | nieinstalowany | 7 |
| Próby działań: nawigacja + fill | nieinstalowany | 1 + 1 |
| Obserwowane postconditions w planie | nieinstalowany | 2 |

Legacy wykonał kolejno `read_view`, `act`, `get_page`, `act`, `finish`.
Porównanie wyników: **80% mniej model calls**, **97,71% mniej tokenów łącznie**, **69,22% krótszy czas**, czyli planner zakończył ten przypadek **3,25 raza szybciej**.

### Estymacja kosztu według konfiguracji OpenFill

Zweryfikowane ustawienia `stable/config.json` dla `gpt-6-luna`: 0,10 USD/1 mln tokenów wejściowych, 0,01 USD/1 mln cached input, 0,50 USD/1 mln tokenów wyjściowych.

- Legacy: `(39183-31015)*0.10/1e6 + 31015*0.01/1e6 + 354*0.50/1e6 = 0.00130395 USD`.
- Planner: `625*0.10/1e6 + 281*0.50/1e6 = 0.00020300 USD`.
- Różnica: `0.00110095 USD`, tj. **84,43% szacunkowego kosztu** dla tej jednej pary. Jest to wyliczenie z lokalnych stawek, nie odczyt faktury ani uśrednienie.

## Zmiany w SOURCE i QA

- Dodano `tools/of_archflow_webview2_smoke/ABBenchmark.cs` wykonujący prawdziwą trasę legacy oraz planner, niezależną weryfikację DOM, pomiar calls/tokens/cache/czasu i zapis wyników JSON.
- Test GUI `tools/of_archflow_webview2_smoke/Program.cs` uzyskał opcjonalny przełącznik `--ab`; środowisko, model i obie ścieżki mają odrębne dzienniki i początkową nawigację.
- Naprawiono niejednoznaczną referencję C# `EventLog` (CS0104 między System.Diagnostics.EventLog i OpenFill.Core.Logging.EventLog) przez alias, kolejny build PASS.
- Doprecyzowano sygnał błędu: jeśli choć jeden rezultat DOM nie został potwierdzony, A/B kończy się wyjątkiem i exit code innym niż 0, zamiast cichego sukcesu. Zmiana po opisanym udanym przebiegu, ponowny build harness PASS.
- `run_interactive.ps1` po jednorazowym A/B **przywrócony** do domyślnego uruchomienia bez `--ab` i bez `--real-planner`, żeby następne ręczne wywołanie nie konsumowało API bez jawnego przełącznika.
- `dotnet build tools/of_archflow_webview2_smoke/ArchflowWebView2Smoke.csproj --no-restore -v:q`: exit 0, 0 błędów, 9 wcześniejszych ostrzeżeń.
- `dotnet build src/OpenFill.App/OpenFill.App__v0.3__2026-10-12_2034.csproj -c Release --no-restore -v:q`: exit 0, 0 błędów, 6 wcześniejszych ostrzeżeń.
- Po zakończeniu GUI nadal `ARCHFLOW_WEBVIEW2_PROFILE_DEFERRED`: `BrowserMetrics*.pma` nieusuwalny. Nie wpływa na wynik poprawności i exit 0. Źródło blokady pliku nieustalone.

## Interpretacja i kolejny krok

W tym kontrolowanym scenariuszu **oba tryby poprawnie wykonały zadanie**. Nowy planner zużył mniej wywołań i czasu. Nie oznacza to, że przewaga 80%/97,7% jest reprezentatywna dla rzeczywistego portfela zadań. Przeprowadzono jeden wynikowy A/B, w ustalonej kolejności legacy -> planner. Tryby mają odmienną treść instrukcji, schematy narzędzi i sposób przechowywania historii. Legacy używał dużego standardowego promptu i rozbudowanego schematu narzędzi; jest to część rzeczywistej różnicy architektur, nie ten sam model promptowy.

Następny etap projektu: domknięcie integracji `of.flow.v1` z `of.plan.v1`, optymalizacja obsługi niejednoznacznych lokatorów i związanie `EffectJournal` z operacjami skutkowymi przed ich włączeniem. Nie przeprowadzano testów wiadomości ani zapisu na zewnętrznych kontach. LIVE bez zmian.
