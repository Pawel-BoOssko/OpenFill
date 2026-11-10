# OpenFill OF-000012: rzeczywiste WebView2 i ograniczony if/foreach

Wybudzenie: `OF-000012-A01-224619` (2 listopada 2026, 22:46:19 +01:00, Warszawa).
Host źródłowy: PBOX1, SOURCE `C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\`.

## Zmiany wykonane

1. Nowy projekt `tools/of_archflow_webview2_smoke/`: niezależny WinForms test, faktyczna kontrolka Microsoft.Web.WebView2.WinForms.WebView2, lokalny HTTP fixture, istniejący `WebView2CdpConnection` i `LocalPlanCdpBrowser`, bez LIVE i serwisów zewnętrznych.
2. Istniejący `src/OpenFill.App/LocalPlanWebView2DevSession__v0.1__2026-11-02.cs` udostępnia DEBUG-only public `Attach(WebView2)`, co umożliwia rzeczywiste wywołanie tej samej klasy z harnessu, zamiast tworzenia osobnego połączenia CDP.
3. Test rzeczywistego osadzonego WebView2: snapshot i stabilna rewizja, odczyt aktualnego hosta, rozpoznanie linku, read-only `if` i `foreach`, jeden plan `locate -> click/navigation -> wait_until -> locate -> fill -> assert_value` i potwierdzenie wyniku w DOM. Wynik: `ARCHFLOW_WEBVIEW2_EMBEDDED_14_CHECKS_OK`, EXIT 0.
4. Nowy SOURCE `src/OpenFill.Core/Agent/LocalPlanControlFlow__v0.1__2026-11-02.cs`: wyłącznie odczytowy, ograniczony `of.flow.v1` z `if` oraz `foreach`. Maksimum 20 węzłów, maks. 20 elementów na foreach, 10 asercji w gałęzi. Każdy element jest ponownie odnajdywany po nazwie i roli na aktualnym DOM, nie po przestarzałym indeksie. Sprawdza host, rewizję i jednoznaczność. Asercje `assert_visible` oraz `assert_absent`; działanie zmieniające stronę odrzucane.
5. Testy `tools/of_imp_smoke/LocalPlanFlowSmoke.cs` i przełącznik `archflow-flow-only`: `ARCHFLOW_FLOW_15_CHECKS_OK`, EXIT 0. Sprawdzono obie gałęzie, pętlę, niejednoznaczność, limit, zmianę hosta, zgodność hosta planu przed pierwszym DOM read i odrzucanie zapisów.
6. Kompilacja `tools/of_archflow_webview2_smoke/ArchflowWebView2Smoke.csproj` PASS, zero błędów. Nie wykonywano wdrożenia LIVE.

## Diagnoza blokady GUI

- Pierwszy `dotnet run` przez LC MultiHost uruchamiał test w Windows SessionId=0. `CoreWebView2Environment.CreateCoreWebView2ControllerAsync` zgłosił `COMException 0x80070578: Nieprawidłowe dojście okna`. Aktywna sesja użytkownika `pboru` to `console ID=1`; diagnostyka potwierdziła SessionId=0 procesu shell. Wniosek: uruchomienie procesu GUI w sesji 0 jest przyczyną niepowodzenia testu, nie sama logika CDP.
- Utworzono ręczne zadanie Windows `OpenFill-OF012-WebView2-Smoke` z `LogonType=Interactive` dla `PBOX1\\pboru`, bez harmonogramu automatycznego uruchamiania. `tools/of_archflow_webview2_smoke/run_interactive.ps1` zapisuje log i exit status. Uruchomienie w SessionId=1 potwierdziło test.
- Dwa defekty sprzątania fixture po zaliczeniu testu: `UnauthorizedAccessException` dla `BrowserMetrics*.pma`, oraz sporadyczny `IOException/SocketException 10053` przy końcowym zamknięciu lokalnego HTTP socket. Drugi rozwiązano przez obsługę rozłączenia klienta w fixture; ostatni pełny przebieg 14 checks ma EXIT=0. `BrowserMetrics*.pma` nadal zgłasza `ARCHFLOW_WEBVIEW2_PROFILE_DEFERRED`, stan końcowy profile folder nie jest usunięty. Nie ustalono, czy uchwyt trzyma Edge/WebView2, czy mechanizm OS/ACL.

## Status i dalsze zadanie

Wykonane w SOURCE/DEV, bez zmiany AgentRunner, ToolRegistry i LIVE. `of.flow.v1` jest osobnym czytającym modułem, **nie** pełnym wykonywaniem `if/foreach` w `of.plan.v1`. `EffectJournal` pozostaje oddzielny i nie jest jeszcze połączony z realnym zapisem/wysyłką.

Kolejny priorytet: scalić reguły `if/foreach` z właściwym plannerem, podłączyć `LocalPlanExecutor` do DEV AgentRunner, zmierzyć rzeczywiste DOM reads, browser actions i model calls, oraz porównać wynik kompletnego zadania, a nie hipotetyczny koszt. Po integracji side effects wymagane `business_operation_id` i trwały `EffectJournal`.
