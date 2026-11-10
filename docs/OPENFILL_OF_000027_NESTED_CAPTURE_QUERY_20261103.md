# OpenFill OF-000027: odczyt capture_id z podkatalogu w DEV MCP

Wake: OF-000027-A01-035007. Rozpoczęcie silnika: 2026-11-03 03:50:07 +01:00 Warszawa.
Host: PBOX1. SOURCE: C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\

## Ustalenia i zmiana SOURCE
- W `PageCaptureService.ResolveOutputFolder` argument `output_folder` może być podkatalogiem współdzielonego root albo jawnie wskazanym folderem absolutnym. `SaveSnapshotAsync` zapisuje pliki i `capture_id.manifest.json` w wybranym folderze.
- Przed poprawką `CaptureArtifactQueryService.Resolve(capturesRoot,captureId,fileKind)` sprawdzał manifest tylko w katalogu głównym `captures`, a nie w podkatalogach. Dlatego poprawny capture zapisany w `captures/task/page/` nie mógł być odczytany przez `openfill_query_capture` po ID, mimo obecności manifestu.
- SOURCE `src/OpenFill.Core/Agent/CaptureArtifactQueryService__v0.1__2026-11-01.cs`: zachowano szybki odczyt manifestu z głównego katalogu. W przypadku braku pliku resolver szuka manifestu o dokładnej nazwie w podkatalogach `captures`, do głębokości 4 poziomów i maksymalnie 2 znalezionych plików; wielokrotne wystąpienie daje `capture_manifest_ambiguous`. Nadal kontroluje SHA256, wielkość i przynależność źródła do root.
- Nie zmieniano struktury manifestu, filtra `rg/jq`, AgentRunner ani danych innych instancji. Jawne foldery absolutne poza `captures` pozostają obsługiwane przez `source_path`, ale nie są indeksowane po `capture_id` przez ten resolver.

## QA
- `tools/of_imp_smoke/NestedCaptureQuerySmoke.cs`, flaga `of027-nested-capture-only` w `Program.cs`, wykonuje pełen `PageCaptureService.SaveSnapshotForTestAsync` -> zrzut w podkatalogu -> `CaptureArtifactQueryService.Resolve` -> `QueryAsync` i realny `rg` -> plik wyniku.
- Test obejmuje: ID, znalezienie manifestu w podfolderze, ścieżkę manifestu, query bez DOM, tekst wyniku, plik wyniku, niezgodność SHA256 i odrzucenie dwóch manifestów o tym samym ID.
- Wynik: `OF027_NESTED_CAPTURE_8_CHECKS_OK`, exit 0.
- Publish: `dotnet publish src/OpenFill.App/OpenFill.App__v0.3__2026-10-12_2034.csproj -c Release --no-restore -v:q -o staging/of_000027`, exit 0. Tylko istniejące ostrzeżenia.
- SHA256 SOURCE `CaptureArtifactQueryService.cs`: `07DB9E2EC03143DD74247A9B64FFA4D92EE809BFC750B18150F0A5A73EF1D147`.
- SHA256 opublikowany Core: `137F1A5902C139FF8C76EDF6B5A21BB917E95C921CEF878945036D1063D4FB64`; EXE: `6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52`.

## DEV
- Przed aktywacją MCP DEV `{"working":[]}`. Backup `staging/of_000027_backup_pre_dev_activation`, poprzedni Core SHA `4BD2C502C31E4319D382F69CE3E4F412E5F8551D18253F2E150191E77FFCB2C1`.
- Stop tylko `OpenFill-OFIMP-dev-20261031`, obserwowano pozostający LIVE PID 252. Kopia release do `staging/of_imp_20261031` i potwierdzenie SHA Core. Start zadania DEV.
- DEV PID 8020 z `--instance dev`, MCP po aktywacji `{"working":[]}`. LIVE PID 252 nie restartowano, Core LIVE SHA `694E48BAAA9D59DDF9B47553968D007C0353FFDA6FC9E402C3B5FB1A0001B89B`.
- Rollback: Stop-ScheduledTask DEV, przywróć `staging/of_000027_backup_pre_dev_activation` do `staging/of_imp_20261031`, Start-ScheduledTask i potwierdź PID/MCP.

## Rzeczywiste MCP DEV
- `tools/of_000027_dev_nested_capture_probe.ps1`: tworzy lokalny zrzut i manifest w `OpenFill\shared\captures\of027-probe`, oblicza SHA256, wywołuje `openfill_query_capture` po ID przez rzeczywisty MCP DEV i sprawdza wynik bez przeglądarki.
- Wynik `OF027_DEV_MCP_NESTED_CAPTURE_OK`, 1 linia, exit 0; ID testu `capture_20261103_055657_103_f1532b`. Skrypt usuwa fixture i wyniki filtra; sprawdzono dodatkowo `Test-Path ...\of027-probe = False`.
- Nie dotykano realnych kont ani zewnętrznego WebView2.

## Dalsza praca
- A06: inne nieukończone elementy zgodnie z scalonym planem. Przy wymaganym dostępie po ID do capture zapisanego w **absolutnym folderze poza shared/captures** potrzebny osobny trwały indeks lub ujednolicenie miejsca zapisu. Nie wprowadzono skanowania całego dysku.
- D01/D02: realne efekty wysyłki nadal wymagają konkretnego zatwierdzonego odbiorcy, treści i zalogowanej sesji; izolowane testy nie są potwierdzeniem dostarczenia.
