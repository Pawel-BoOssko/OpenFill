# OF-000031: A05 text-only signed recipes, DEV i LIVE

Wake: OF-000031-A01-051024, 2026-11-03 05:10:24 +01:00 Warszawa.
Host: PBOX1. SOURCE: C:/Users/pboru/Downloads/OpenFill__v0.1__2026-10-04_1911/OpenFill.

## Weryfikowana luka
`PageCaptureService.CaptureAsync(mode=text)` nie zwracal struktury DOM, a podpis manifestu stanowil SHA256 z pustego ciagu elementow. `CaptureRecipeLiveProbe` odrzucal wszystkie recipes mode=text, przez co najczestsze odczyty tekstowe nie mialy drogi do dokladnego automatycznego reuse verified recipe. Wczesniejszy OF-000030 obslugiwal tylko exact mode=all.

## Wykonana zmiana
- PageCaptureService: dla mode=text odczytuje wyłącznie lokalną listę znaczników tag/role/type/name ograniczoną do 3000 węzłów; nie pobiera HTML ani JSON elementów. Strukturę wykorzystuje do podpisu, zapisuje jedynie zwykły plik tekstowy i manifest. Nie zapisuje nowej receptury, gdy struktura jest niekompletna.
- CaptureManifestMetadata: podpis text używa tego samego `ComputeViewSignatureFromStructure`, co istniejący live probe, 64 znaki SHA256.
- CaptureRecipeLiveProbe: dopuszcza verified text recipes i sprawdza zgodność obecnego URL, struktury oraz account scope.
- CaptureRecipeRaceGate: ponawia kontrolę 64-znakowego podpisu przed filtracją text.
- AutoViewReadPlanner + ToolRegistry: przenoszą podpis oraz dokładny mode=text do capture; po wykrytej zmianie widoku filtr nie uruchamia niezweryfikowanej receptury.
- Test: tools/of_imp_smoke/Of031TextRecipeSmoke.cs, uruchomienie z argumentem of031-text-recipe-only, bez testowania Airbnb ani wysyłek.

## QA i wdrożenie
- `dotnet run --project FileFilterSmoke.csproj -c Debug --no-restore -- of031-text-recipe-only`: OF031_TEXT_RECIPE_9_CHECKS_OK, exit 0.
- `dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000031`: exit 0, ostrzeżenia istniejące.
- Wydanie Core SHA256: CBD15C6B0CF5A79382C294092E27782EEC99353D8ACA4333E163FDA781B759B7.
- DEV backup: staging/of_000031_backup_pre_dev_activation; aktywacja do staging/of_imp_20261031, zadanie OpenFill-OFIMP-dev-20261031 uruchomione; DEV PID 14000.
- CheckOnly: RELEASE_READY_NO_IDLE_GATE.
- Release LIVE skrypt: tools/of_imp_release_live_ALWAYS_20261101.ps1; BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True, exit 0.
- LIVE backup: C:/Users/pboru/OpenFill/app__rollback__before_release_20261103_071736.
- LIVE PID 27108, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, Core SHA256 CBD15C6B0CF5A79382C294092E27782EEC99353D8ACA4333E163FDA781B759B7.
- LIVE OpenFill.exe SHA256: 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52.
- Faza: SOURCE DONE -> DEV ACTIVE -> LIVE ACTIVE.
- Jednorazowe WebView2 read_view acceptance: OF-20261103-0719-ee86, wynik do uzupelnienia po zakonczeniu. To zadanie odczytowe.

## Otwarty zakres
- Aktywny odczyt WebView2 nie dowodzi automatycznego reuse verified recipe, jeśli brak już zweryfikowanej receptury dokładnego widoku.
- D01/D02 effect acceptance nadal niewykonane (brak zatwierdzonego konkretnego celu i tresci); brak wysylek w tym wake.
- D03 Airbnb anulowane, bez zmian.

## Korekta na podstawie rzeczywistego WebView2: A08 LinkedIn Feed
- Read-only task LIVE OF-20261103-0719-ee86: LinkedIn /feed/, 3 kroki, cost 0.0019 USD, status done/success; wynik read_view view_type=form, routing_reason=interactive_or_detail, source_scope=current_live_view.
- Zrzut istniejącej karty WebView2 potwierdził zalogowany LinkedIn Feed i widoczne posty. Klasyfikacja jako form była błędna (liczenie inputów przed zawartością listy).
- SOURCE SemanticViewInspector: dodano ograniczone liczenie content cards; dla rzeczywistych feedów z content cards lub odpowiednio bogatą zawartością feed priorytet list nad input/form. Zachowano priorytet inbox/table, a formularze bez zawartości feed nadal klasyfikowane jako form.
- Testy celowane Of031TextRecipeSmoke z fixtures feed/form/inbox: OF031_TEXT_AND_FEED_13_CHECKS_OK, exit 0; nie uruchamiano pełnych testów Airbnb.
- Publish staging/of_000031_feed: exit 0. Nowy Core SHA256: 7148D551A86F5C288DCF2687432A886EE3DF0E277F872AA81E9D7BE1441506D7.
- DEV backup staging/of_000031_feed_backup_pre_dev_activation, wdrożony DEV PID 13128.
- CheckOnly RELEASE_READY_NO_IDLE_GATE; release LIVE BACKUP_OK/UPDATE_NOTICE_SENT/LIVE_RELEASE_OK/mcp=True exit 0.
- Rollback LIVE: C:/Users/pboru/OpenFill/app__rollback__before_release_20261103_072601.
- Potwierdzony LIVE PID 5144, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, Core SHA256 7148D551A86F5C288DCF2687432A886EE3DF0E277F872AA81E9D7BE1441506D7. CheckOnly po wydaniu LIVE_ALREADY_CURRENT exit 0.
- Drugi read-only WebView2 task OF-20261103-0726-255a (continued_from OF-20261103-0719-ee86): done/success, 3 kroki, koszt 0.0016 USD. Realny read_view: status=ok, view_type=list, routing_reason=generic_visible_records, source_scope=current_live_view. Potwierdza usunięcie błędnej klasyfikacji tego feedu. Nie dowodzi jeszcze reuse konkretnej verified recipe.
