# OF-000010, lokalny adapter planów do CDP

Data: 2026-11-02. Status: SOURCE DONE, real-browser smoke PASS, WebView2 UI acceptance PENDING, LIVE bez zmian.

## Zaimplementowane

- `src/OpenFill.Core/Agent/LocalPlanCdpBrowser__v0.1__2026-11-02.cs` realizuje `ILocalPlanBrowser` na istniejącym `OpenFill.Core.Cdp.CdpSession`, współdzielonym z architekturą WebView2.
- `ReadAsync`: widoczne elementy link/textbox z rolą, nazwą, dostępnością i identyfikatorem. Rozpoznawanie nazwy przez aria-label, aria-labelledby, label, placeholder albo tekst.
- Rewizja rośnie tylko po zaobserwowanej zmianie: URL, tożsamość dokumentu, semantyczne elementy i ich href. Dwa identyczne odczyty nie zwiększają rewizji. Limit wycinka: 500 elementów.
- `ClickNavigationAsync`: w pojedynczym wywołaniu JS sprawdza fingerprint i referencję, po czym wykonuje `location.assign` dla rzeczywistego linku `<a href>` z tym samym origin; nie wywołuje eventów click ani operacji aplikacyjnych. Link zewnętrzny, javascript:, pobranie i inne działania pozostają poza DSL.
- Odrzucony stare revision oznacza `stale`, odrzucone nawigacje `failed_before_action`, niepewne wyjątki `uncertain`. Nie ma automatycznego retry akcjami.
- Po nawigacji odczyt czeka na zaobserwowaną zmianę i `document.readyState=complete` (maks. 2,4 s), bez powtórnego kliku.

## Integracyjny test lokalny

`tools/of_archflow_cdp_smoke/ArchflowCdpSmoke.csproj` oraz `Program.cs`:

- Oddzielny proces Microsoft Edge w headless z nowym tymczasowym profilem i własnym portem CDP.
- Lokalny HTTP fixture na 127.0.0.1 z widokami `/list`, `/form`, `/redirect-list`, `/redirect-go`, `/external`.
- Rzeczywisty WebSocket CDP: `Target.createTarget`, `Target.attachToTarget`, `Runtime.evaluate` przez istniejący `CdpSession`.
- 12 sprawdzeń: obecność semantycznego DOM, stabilność i zmiana rewizji, stara rewizja, nieklikanie dla stale, wyścig DOM między Read i Click, udana lista -> formularz, postcondition, przekierowanie HTTP oraz odmowa linku zewnętrznego.
- Wynik ostatniego testu: `ARCHFLOW_CDP_REAL_BROWSER_12_CHECKS_OK`, exit 0.
- W jednym wcześniejszym przebiegu `postcondition_missing` ujawnił wyścig obserwacji dokumentu. Dodano warunek `readyState=complete`, kolejny przebieg PASS. Nie należy uznawać jednostkowego PASS za dowód stabilności w każdej witrynie.

Kompilacja końcowa `dotnet build src/OpenFill.App/... --no-restore -v:q`: exit 0, 0 errors, 6 istniejących warnings. Wersja LIVE nie została wydana i żadne rzeczywiste wiadomości ani zapisy w zewnętrznych serwisach nie zostały wykonane.

SHA256 adaptera: `CCE7419B1F834B5FB728B5DBB1FE94FA080D3D3DFDB2D82A8FFA77AED29D00FF`.

## Ograniczenia i dalszy etap

- Test korzysta z realnego silnika Edge/CDP, ale nie otwiera okna osadzonego WebView2 należącego do OpenFill. Kontrakt i transport CdpSession są wspólne, lecz testu tego nie należy określać jako pełnego acceptance WebView2.
- Dodano `src/OpenFill.App/LocalPlanWebView2DevSession__v0.1__2026-11-02.cs`: jawny adapter `WebView2CdpConnection -> CdpSession -> LocalPlanCdpBrowser`, kompilowany tylko w `DEBUG`. Właściciel połączenia implementuje `IAsyncDisposable`. Powtórny build App: PASS, 0 errors, 6 wcześniejszych warnings.
- `LocalPlanCdpBrowser` jest izolowany od produkcyjnego AgentRunner, nie ma nowego routingu narzędzi w LIVE. Na stronach dynamicznych ładowanie danych po `readyState=complete` nadal może wymagać lokalnego oczekiwania na konkretny semantyczny postcondition.
- Następny etap: DEV WebView2 fixture, a następnie dodać do DSL `wait_until` i `fill` tylko z potwierdzonymi stanami. Przed jakimkolwiek `send` lub `save` obowiązuje trwały `EffectJournal` i `business_operation_id`.
- Koszty A/B mierzyć na równoważnych zakończonych zadaniach, nie utożsamiać historycznego wzorca 5515 calls z rzeczywistą oszczędnością.
- Airbnb anulowane, nie wznawiać.
