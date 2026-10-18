# OpenFill: Technology Map, v0.1

Data: 2026-10-18. Zakres: architektura zrodel, nie nowego wdrozenia. Kontrakt OF-184: jeden manifest technologii wspolny z panelem u gory aplikacji.

## Jedno zrodlo prawdy
Pelne 41 tematow w 7 kategoriach to dokument maszynowy:
docs/openfill_technology_manifest__v0.1__2026-10-17_1616.json.
Kazdy temat ma category, name, technology, component. Nie kopiujemy wszystkich wpisow do tego dokumentu, bo powstaloby drugie recznie utrzymywane zrodlo. Panel wyswietla tematy z TechnologyAtlas (C#), a manifest jest odciskiem zrodel. Przed oznaczeniem dokumentacji jako zgodnej nalezy sprawdzic manifest.sourceSha256 wobec faktycznego kodu TechnologyAtlas.

Zrodlo panelu: src/OpenFill.App/TechnologyAtlas__v0.1__2026-10-15_2314.cs.
Dwie wartosci dynamiczne: BuildInfo.Version oraz Environment.ProcessId. Wskazuja odpowiednio wersje aplikacji i PID aktualnego procesu, nie historyczne stale.

## Siedem obszarow do nauki
1. Languages / runtime: C#, .NET 10, JavaScript, HTML/CSS, PowerShell, JSON/NDJSON, MSBuild i NuGet.
2. Desktop / browser: WinForms, WebView2, Chromium, CDP, WebSocket, semantyczny DOM, rzeczywiste zdarzenia i popup.
3. AI / orchestration: OpenAI Responses API, AgentRunner, ToolRegistry, model wykonawczy, budzet krokow/kosztow.
4. Connectivity / protocols: MCP, JSON-RPC 2.0, Streamable HTTP, Cloudflare Tunnel, file transfer HTTP PUT/GET, SHA-256 i idempotencja.
5. Data / security: Windows Credential Manager, DPAPI, Firefox NSS, exact-origin, SiteNotesStore, redakcja danych.
6. Operations / quality: drzewo procesow, ResourceTelemetry, WinForms lifecycle, backup, staging, rollback, testy C#.
7. Ecosystem: ChatGPT / Claude, Networking Radar, PBOX1, narzedzia zewnetrzne i harmonogram Windows.

## Przeplywy danych i komponenty
- Wykonanie zadania: ChatGPT lub inny klient MCP -> McpServer -> McpTaskManager -> AppHost/TaskRun -> AgentRunner (model wykonawczy) -> ToolRegistry -> BrowserController/CDP -> WebView2/strona -> obserwacje i wynik -> MCP.
- Hasla: Firefox profilu uzytkownika -> importer NSS -> osobny Windows Credential Manager -> origin wybranej strony -> lokalny formularz. Hasla nie staja sie czescia tej mapy i nie wolno tu umieszczac ich wartosci.
- Radar: intencjonalny event -> idempotency_key -> MCP task -> formularz organizatora -> potwierdzony rezultat organizatora -> dopiero wtedy Google Calendar. Wynik agenta lub Calendar nie jest sam w sobie dowodem rejestracji.
- Recovery: stan lokalny -> szyfrowany backup -> prywatny magazyn -> odizolowany restore i porownanie hashy -> dopiero po testach mozliwe wydanie/rollback. Odtworzenie rzeczywistego backupu na SATANAMD nadal niepotwierdzone.

## Dlaczego taki stos technologiczny (na podstawie rzeczywistych komponentow)
- WinForms i WebView2: natywna sesja desktopowa, lokalny browser oraz mozliwosc pracy z witryna i formularzami w kontekście uzytkownika.
- DOM i CDP: model semantyczny interfejsu zamiast ciaglego polegania tylko na zrzutach ekranu; wykorzystywanie rzeczywistych zdarzen przy trudniejszych kontrolkach.
- MCP: standardowy interfejs dla ChatGPT/Claude, oddzielony od wykonawczego modelu uruchamianego w aplikacji.
- WinCred/DPAPI: sekretow nie przechowuje sie w opisach technologii ani w zdalnych instrukcjach.
- Telemetria: zbieranie kosztu calosci procesu i rzeczywistych potomkow WebView2; srednie WS moga podwojnie liczyc wspoldzielone strony DLL.

## Przykładowe zdania na rozmowę techniczną
- C#: "W OpenFill wykorzystano C# do implementacji agenta, API MCP i zarzadzania kartami."
- WinForms: "W OpenFill panel, karta WebView2 i formularze poświadczeń dzialaja w aplikacji Windows Forms."
- CDP: "W OpenFill BrowserController uzywa Chrome DevTools Protocol do odczytu i obslugi kontrolowanych stron."
- MCP: "W OpenFill McpServer przyjmuje zlecenia od klienta ChatGPT, a McpTaskManager zapisuje task_id i zarzadza cyklem zycia zadan."
- DPAPI: "W OpenFill SecretStore wykorzystuje mechanizmy Windows do lokalnego zabezpieczenia sekretow."
- ResourceTelemetry: "W OpenFill monitorowane sa proces, jego potomkowie, CPU i rozne miary pamieci w odstepach czasu."

To opis komponentow projektu, a nie przypisanie autorstwa lub osobistego doswiadczenia uzytkownikowi.

## Stan akceptacji
Static source tests: sprawdzaja dynamiczne pola, fabryke banera, mocowanie w MainForm, 41 tematow / 7 kategorii i hash atlasu. Pełny test GUI w interaktywnej sesji Windows z rozdzielczoscia, DPI, szukaniem, kopiowaniem i brakiem sekretow nie jest tu potwierdzony (OF-030/168).

Repozytorium: https://github.com/Pawel-BoOssko/OpenFill
