# OpenFill OF-000028: capture_id dla zrzutów zapisanych poza shared/captures

Wake: OF-000028-A01-041017, 2026-11-03 04:10:17 +01:00 Warszawa.
Host: PBOX1. SOURCE: C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\

## Zdiagnozowana luka A06
- `PageCaptureService.ResolveOutputFolder` zezwalał na jawne ścieżki absolutne, a także ścieżki względne do shared root. Przy zapisie poza `shared/captures` lokalny manifest i pliki były poprawne, ale `CaptureArtifactQueryService.Resolve` nie mógł rozwiązać takiego `capture_id` przez MCP.
- OF-000027 naprawił podkatalogi `shared/captures`, ale nie objął katalogów poza tym root. Samo `source_path` było obejściem, nie pełnym kontraktem odczytu po ID.

## Zmiana SOURCE
1. `src/OpenFill.Core/Agent/PageCaptureService__v0.1__2026-10-30.cs`: parametr wewnętrzny `captureIndexRoot` z produkcyjnego `Path.Combine(sharedDirectory,"captures")`. Przy zapisie zewnętrznym (poza tym root) rejestrowany jest mały `capture_id.external-index.json` we wspólnym katalogu captures. Indeks zawiera `capture_id`, absolutną ścieżkę manifestu, SHA256 bajtów manifestu oraz rzeczywisty folder źródłowy.
2. Pliki źródłowe HTML, text, elements pozostają w folderze wskazanym przez użytkownika, bez dublowania. Indeks jest publikowany po źródłowym manifeście, przez staging `.pending` i `File.Move`; przy niepowodzeniu usuwane są utworzone pliki tej operacji.
3. `src/OpenFill.Core/Agent/CaptureArtifactQueryService__v0.1__2026-11-01.cs`: dotychczas szybki root i recursive lookup pozostają. Gdy ich wynik jest pusty, resolver sprawdza tylko dokładny zarejestrowany indeks, weryfikuje zgodność ID, dokładne położenie i nazwę manifestu oraz SHA256 jego treści. Sprawdza też, czy plik danych leży w folderze tego manifestu i pasuje nazwą do capture ID; następnie dotychczasowe SHA256 pliku i jego rozmiar.
4. Nie skanuje dysków ani przypadkowych folderów. Nie wymaga odczytu DOM ani nowych narzędzi MCP. Przechwytywania sprzed tej zmiany, poza shared/captures i bez indeksu, nadal wymagają `source_path`.

## Weryfikacja
- Rozszerzono `tools/of_imp_smoke/NestedCaptureQuerySmoke.cs` o zewnętrzny folder z rzeczywistym zapisem `PageCaptureService.SaveSnapshotForTestAsync`, sprawdzenie indeksu, `CaptureArtifactQueryService.Resolve`, lokalnego `QueryAsync` przez rg, modyfikacji pliku i manifestu. Zachowano poprzednie testy nested.
- Test: `dotnet run --project tools/of_imp_smoke/FileFilterSmoke.csproj -c Debug --no-restore -- of027-nested-capture-only`: `OF028_NESTED_EXTERNAL_CAPTURE_13_CHECKS_OK`, exit 0.
- Wykryto jedno nowe ostrzeżenie nullable CS8602, wyeliminowano przez jawny test `index is null` przed odwołaniem. Publish Release: `dotnet publish src/OpenFill.App/OpenFill.App__v0.3__2026-10-12_2034.csproj -c Release --no-restore -v:q -o staging/of_000028`, exit 0, pozostały historyczne ostrzeżenia.
- SHA256 SOURCE PageCaptureService: `7A5293D999C20DBC79849F9DCC181ACA78C11FBE7D92DD452D7E08249ABACD63`.
- SHA256 SOURCE CaptureArtifactQueryService: `BD75870478C1776536D9FF83682C7E11B0A8613431FF5F49365ECAE909219411`.
- SHA256 Core.dll Release i DEV: `6F23153EA11500E9B1750127E3E779C6E049A9E656C03C22C491C8663DA623AA`.
- SHA256 wydanego EXE: `6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52`.

## Wdrożenie
- Przed zmianą DEV PID 8020 `--instance dev`, MCP `{"working":[]}`, LIVE PID 252.
- Backup poprzedniej wersji: `staging/of_000028_backup_pre_dev_activation`, Core SHA `137F1A5902C139FF8C76EDF6B5A21BB917E95C921CEF878945036D1063D4FB64`.
- Stop wyłącznie `OpenFill-OFIMP-dev-20261031`; obserwowany przy stop tylko LIVE PID 252. Kopia release do aktywnego `staging/of_imp_20261031`, weryfikacja Core SHA, restart harmonogramu.
- Aktywny DEV PID 9040, ścieżka `staging/of_imp_20261031/OpenFill.exe --instance dev`, MCP `{"working":[]}`. LIVE PID 252, Core SHA `694E48BAAA9D59DDF9B47553968D007C0353FFDA6FC9E402C3B5FB1A0001B89B`, bez zmian.
- Rollback: Stop-ScheduledTask DEV, przywrócenie z backup do active staging, Start-ScheduledTask, kontrola PID/Core/MCP.

## Rzeczywista ścieżka MCP DEV
- `tools/of_000028_dev_external_capture_probe.ps1`: utworzono kontrolowany plik testowy i manifest poza `shared/captures`, indeks w `shared/captures`; wywołano `openfill_query_capture` po `capture_id` przez aktywny MCP DEV.
- Wynik: `OF028_DEV_MCP_EXTERNAL_CAPTURE_OK`, `MATCHED_LINES=1`, exit 0; `capture_20261103_061658_356_a8b464`. Skrypt usuwa fixture, indeks i pliki wynikowe. Nie użyto WebView2 ani kont zewnętrznych.
- Test dev MCP korzystał z kontrolowanego pliku/manifestu, nie wywoływał przeglądarkowego `CaptureAsync`. Niezależny test SOURCE ćwiczył rzeczywistą serializację snapshotu i indeksu; obu wyników nie należy przedstawiać jako jednego testu końcowego WebView2.
