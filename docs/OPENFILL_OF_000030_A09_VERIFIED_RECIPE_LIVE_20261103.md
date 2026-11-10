# OF-000030: A09 verified capture recipe, DEV i LIVE
Data wybudzenia: 2026-11-03 04:50:14 +01:00 (Warszawa).
Nowy lead: b7a481ef878b4a79981563d5a676b558. Poprzedni handoff: SATANAMD, D:/OpenBrowserLiteHandoffs/of/HANDOFF_CURRENT.md, OF-000029-A01-043006.
Host pracy: PBOX1. SOURCE: C:/Users/pboru/Downloads/OpenFill__v0.1__2026-10-04_1911/OpenFill.

## Ustalona przyczyna
- CaptureManifestMetadata.ComputeViewSignature zapisuje SHA256 64 znakow selector/mode/DOM.
- SemanticViewInspector.Summarize daje 20-znakowy hint. To odrebny kontrakt.
- Automatyczne ToolRegistry.read_view dotychczas wybieralo _recipes.Best(host,path) bez przetestowania pelnej sygnatury w obecnym DOM.
- CaptureRecipeLiveProbe.SelectAsync i CaptureRecipeRaceGate juz istnialy w SOURCE. Uzyto tych implementacji, bez drugiego registry.

## Zmiana SOURCE
- AutoViewReadPlanner: odrebne SemanticSignature (20, tylko routing) i RecipeSignature (64, SHA256); signed verified recipe wymaga exactLiveVerified, zgodnego host/route/mode all; dla LinkedIn dodatkowo account scope.
- ToolRegistry.read_view: preferuje CaptureRecipeLiveProbe.SelectAsync przed route-only fallback. Przed filtrem przekazuje podpis, URL i account scope do observe_query.
- ToolRegistry.observe_query: verified live recipe przechwytuje mode=all (w tym elements), po czym CaptureRecipeRaceGate sprawdza pelna 64-znakowa sygnature; blad struktury konczy filtrowanie bez wykonania niezweryfikowanej receptury.
- SemanticViewInspector: wyrazne semantic_signature i view_signature_kind=semantic_hint_v1, stary view_signature=20 zachowany jako alias zgodnosci.
- Test: tools/of_imp_smoke/Program.cs, OF030_SIGNATURE_CONTRACT_5_CHECKS_OK.
- Skrypt release: tools/of_imp_release_live_ALWAYS_20261101.ps1 uzupelniony o exit 0 po potwierdzonym sukcesie, aby kod robocopy 1..7 nie byl blednie traktowany jako blad procesu. Nie zmieniano procedury rollback.

## QA i wydanie
- dotnet run tools/of_imp_smoke/FileFilterSmoke.csproj -c Debug --no-restore z cwd tools/of_imp_smoke: ALL_SMOKES_PASS, exit 0; OF030_SIGNATURE_CONTRACT_5_CHECKS_OK.
- dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000030 -v:q: exit 0, bez bledow.
- Core release SHA256: 1C53D07D395DA9F224A3B25B91093330F5091B556A1B34EF9FD9F70A5C109B97.
- DEV backup: staging/of_000030_backup_pre_dev_activation. DEV aktualny: staging/of_imp_20261031, identyczny Core SHA256. Task DEV Running, PID 20980.
- CheckOnly przed release: RELEASE_READY_NO_IDLE_GATE.
- LIVE script: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True. Powrot procesu exit_code=3 mimo udanego release, bo robocopy kod 3 pozostawal jako LASTEXITCODE; skrypt poprawiony po wydaniu.
- LIVE backup: C:/Users/pboru/OpenFill/app__rollback__before_release_20261103_070041.
- LIVE PID 27604, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, harmonogram OpenFill-open Running.
- LIVE Core SHA256: 1C53D07D395DA9F224A3B25B91093330F5091B556A1B34EF9FD9F70A5C109B97.
- LIVE OpenFill.exe SHA256: 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52.
- Niezalezne wywolanie OpenFill MCP openfill_info: {"working":[]}.
- CheckOnly po release: LIVE_ALREADY_CURRENT sha256=1C53D07D395DA9F224A3B25B91093330F5091B556A1B34EF9FD9F70A5C109B97, exit 0.
- Faza: SOURCE DONE -> DEV ACTIVE -> LIVE ACTIVE. Nie wykonywano rzeczywistych wysylek ani zmian w serwisach zewnetrznych.

## Pozostalo
- Rzeczywiste WebView2 acceptance verified recipe na istniejącym, zalogowanym widoku nie było częścią tego smoke. Nie oznacza to potwierdzenia skutku domenowego.
- D01 WhatsApp i D02 LinkedIn: skutkowy acceptance wymaga precyzyjnie zatwierdzonego odbiorcy i tresci; bez automatycznego wysylania.
- D03 Airbnb anulowane decyzja wlasciciela 2026-11-02, nie realizowac.
- Rotacja: OF-000030 pozostaje leadem do 000039; harmonogram wybudzen bez zmian.
