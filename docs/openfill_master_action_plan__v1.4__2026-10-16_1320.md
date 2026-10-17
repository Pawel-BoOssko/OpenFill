# OPENFILL: PEŁNY PLAN PRAC I WYMAGAŃ
**Wersja dokumentu:** 1.2
**Data:** 2026-10-16, 11:42 (Europe/Warsaw); bazuje na v1.1
**Repozytorium:** C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill
**Produkcja:** C:\Users\pboru\OpenFill\app\OpenFill.exe
**Zakres:** cała aplikacja OpenFill, jej agent, UI, przeglądarka, MCP, poświadczenia, infrastruktura, koszt, testy, wydajność, współpraca z Networking Radar. Nie jest to plan zmian samego Radaru, Lupus Consulting, Skakanki ani konfiguracji portali zewnętrznych.
**Charakter:** plan przed implementacją. Samo przygotowanie tego dokumentu nie oznacza wykonania opisywanych zmian.

## 0. Zasady czytania planu i źródła prawdy
- [DO ZROBIENIA]: funkcja albo test niezakończony.
- [CZĘŚCIOWO]: element istnieje w kodzie lub produkcji, ale nie jest odbierany jako całość.
- [GOTOWE W ŹRÓDŁACH]: implementacja i testy automatyczne są w niewdrożonym kodzie.
- [GOTOWE W PRODUKCJI]: istnieje w działającej wersji 1.52 i ma potwierdzenie działania.
- [DO AUDYTU]: wymóg historyczny lub potencjalny problem, którego aktualnego stanu nie można uznać za rozstrzygnięty.
- [BLOKER]: konkretna operacja jest niedostępna, nie oznacza to zablokowania innych prac.
- [PROPOZYCJA]: usprawnienie techniczne, nie wcześniej zatwierdzona funkcja.

Źródła: handoff_complete v1.3 (15.10 15:35), performance_implementation_status v0.5 (15.10 21:22), release_preflight_allowed v0.2, README v0.17, bieżące źródła .NET, dokument integration_networking_radar v0.3, dotychczasowe rozmowy i wymagania użytkownika, Trello Lupus Consulting (wzorzec kont i haseł tymczasowych), Trello Networking Radar (karty: FV71I2uV i 92Jv4POM), Moja tablica Trello (0J35AKcd). Historyczne README i starsze sekcje handoffu mogą opisywać stan sprzed 1.52: nie traktować ich jako bieżącego statusu bez weryfikacji.

## 1. Cel, granice i kryterium ukończenia
- [WYMAGANE] OpenFill jest lokalnym wykonawcą czynności na prawdziwych stronach WWW. Silniejszy model nadrzędny dostarcza pełny cel, nie serię mikropoleceń. Agent OpenFill ma tanio realizować czynności, korzystając z DOM, CDP, sesji i narzędzi, zwracać sprawdzony wynik.
- [WYMAGANE] OpenFill nie ma samodzielnie prowadzić researchu i podejmować decyzji biznesowych, które należą do zlecającego modelu. Nie rozbudowywać go w duplikat zewnętrznego asystenta.
- [WYMAGANE] Domknięcie ma oznaczać działającą, sprawną produkcję na laptopie, nie jedynie wykrycie/brak wycieku, build lub testy.
- [WYMAGANE] Nie zatrzymywać niezależnych prac z powodu blokady jednego narzędzia. Dzielenie na dozwolone samodzielne działania jest regułą; nie obchodzić konkretnego zakazu przez inną trasę.
- [WYMAGANE] Do PBOX1 korzystać z Lupus Commander MultiHost. Nie korzystać z Remote Desktop Commandera.
- [WYMAGANE] Nigdy nie zmieniać nazwy istniejącego czatu w wyniku dodania polecenia, automatyzacji ani kontynuacji. Nowo utworzonemu czatowi można nadać nazwę.
- [WYMAGANE] Nie restartować produkcji, nie zmieniać kart/profili, nie przerywać cudzych zadań przy pracach rozwojowych lub diagnostycznych. Aktualizację robić wyłącznie przez dozwolon�� kontrolowaną procedurę.
- [WYMAGANE] Dokumenty i raporty zapisywać pod nazwą zawierającą wersję, datę i godzinę, bez nadpisywania starszych wersji.
- [WYMAGANE] Należy oddzielać potwierdzenie: zbudowane, przetestowane automatycznie, uruchomione testowo, wdrożone, sprawdzone w realnej pracy.
- [WYMAGANE] Zaplanowana automatyzacja godzinowa ma kontynuować niedokończoną pracę, a nie być pretekstem do przedwczesnego kończenia sesji.

## 2. Status obecny, bez fikcyjnego zamykania spraw
- Produkcja: OpenFill 1.52, PID 27472, uruchomiona 14.10.2026 o 23:02:56, Windows SessionId 1, MCP działające. Znany stan kontroli: 5 zakończonych kart, brak aktywnych zadań w chwili odczytu.
- Wydanie robocze: 1.53 w repo, nie wdrożone. Najnowszy checkpoint 15.10 21:22: 634/634 testów zaliczonych, build Release 0 błędów, 1 wcześniejsze ostrzeżenie MSB3277.
- 1.53 zawiera w kodzie: zwalnianie zakończonych kart i należących do nich pop-upów, zapis adresów do wznowienia, odzyskanie śledzenia kart po błędzie, interfejs listy kont Credentials, wielokrotny import z wielu domen, dokładniejszą weryfikację credentiali, dodatkowe testy.
- SEOHost: wcześniejszy handoff odnotował udane rzeczywiste logowanie dla właściwego konta. Nie wracać do twierdzenia, że nigdy go nie testowano.
- CyberFolks: origin panel.cyberfolks.pl i login 04229383 istnieją w Windows vault; OpenFill poprawnie wypełnił login i hasło, lecz serwer odrzucił dane. Odrębne originy i loginy nie mogą być mieszane.
- Pamięć stron i telemetria są wdrożone w 1.52, ale wprowadzone później optymalizacje nie są jeszcze na produkcji.
- Krytyczne pomiary 1.52: użytkownik widział ok. 5 GB RAM i 20% CPU; zarejestrowano także wiele okresów 2,4-4,6 GB sumarycznego Working Set drzewa WebView2 z niezerowym CPU w idle. W 120 próbkach do 21:22: 90 idle, 30 active, średnio 3,293% CPU całej maszyny idle i 13,315% active, końcowo 2417 MB WS, 2191 MB PB, 20 WebView2, 5 kart. Sumaryczny Working Set procesów nie jest tożsamy z unikalnym fizycznym RAM.
- Wdrożenie: staging 1.53 i nowy wrapper wcześniej blokowane w konkretnej operacji. MultiHost PowerShell jest w Windows SessionId 0; produkcyjna interaktywna sesja to 1. Nie traktować zbudowanego DLL/EXE jako staged release.
- Dokumentacja ma luki względem kodu, a repozytorium nie ma pełnej historii Git. Jest to wymaganie do audytu, nie wolno twierdzić, że wszystko utrzymuje się w VCS.

## 3. NOWE WYMAGANIE: stały panel technologiczny do przygotowania do rozmów rekrutacyjnych

### 3.1. Umiejscowienie i zachowanie
1. [DO ZROBIENIA] W górnej części głównego okna WinForms dodać osobny, stale widoczny panel technologii pomiędzy najwyższym paskiem okna (title bar Windows) a istniejącym paskiem adresu przeglądarki. Nie umieszczać go w panelu rozmowy AI po prawej.
2. [DO ZROBIENIA] Panel ma być widoczny przy KAŻDYM uruchomieniu aplikacji, przed przejściem na dowolną stronę. Ma działać przy zminimalizowanym lub ponownie otwartym oknie.
3. [DO ZROBIENIA] W podstawowym pasku, bez rozwijania, pokazać najważniejsze fakty: C#/.NET 10, WinForms, WebView2/Chromium/CDP, OpenAI Responses API, MCP/Streamable HTTP, Windows/PBOX1, magazyn poświadczeń, dane lokalne, wersja i status procesu. Fakty mają pochodzić z rzeczywistej implementacji, nie z dekoracji.
4. [DO ZROBIENIA] Obok dodać otwarcie pełnego widoku wszystkich technologii i architektury, ponieważ wszystkich szczegółów nie da się czytelnie umieścić w jednym wąskim pasku.
5. [DO ZROBIENIA] Widok pełny musi być przeszukiwalny i pogrupowany. Bez automatycznego przewijania utrudniającego naukę i bez zasłaniania pola adresu i przeglądarki.
6. [DO ZROBIENIA] W układzie ze skalowaniem Windows, różną rozdzielczością i DPI sprawdzić wysokość, wielokrotne wiersze, obcięcie tekstu, zmianę rozmiaru okna, niezmienną dostępność paska adresu i przycisków Credentials/Import Firefox.
7. [DO ZROBIENIA] Wybór rozwiniętej kategorii, wyszukiwarki lub sekcji nie może blokować pracy przeglądarki, weryfikacji CAPTCHA, kart ani podglądu zadań.
8. [PROPOZYCJA] Dodać „Kopiuj opis technologii” oraz „Słownik na rozmowę” z konkretną, krótką odpowiedzią po polsku oraz prawidłowym terminem angielskim.
9. [PROPOZYCJA] Umożliwić ręczne oznaczenie wybranych pojęć „znam”, „powtórzyć”, bez przekształcania tego panelu w oddzielny system edukacyjny. To dodatki, nie warunki uruchomienia panelu.

### 3.2. Treść: kompletna mapa stosu technologicznego
Zadanie polega na udokumentowaniu RZECZYWISTEGO stosu i zależności. Każdy rekord powinien mieć: kategorię, nazwę i wersję (gdy znana), rolę w OpenFill, miejsce w kodzie/konfiguracji, miejsce uruchomienia, sposób komunikacji, po co to rozwiązanie wybrano, angielski odpowiednik dla rozmowy technicznej oraz status: używane / tylko CLI / test / planowane. Nie dopisywać technologii, których repozytorium nie używa.

10. [DO ZROBIENIA] Języki: C#, .NET, JavaScript, HTML/CSS, PowerShell oraz formaty JSON/NDJSON, konfiguracyjny MSBuild XML i ewentualne CMD. Ustalić rzeczywiste zastosowanie każdego z nich.
11. [DO ZROBIENIA] Framework i GUI: .NET 10, Windows Forms, Microsoft Edge WebView2, podział na aplikację desktop, rdzeń i CLI, wbudowany web-panel HTML/JS, obsługa DPI.
12. [DO ZROBIENIA] Silnik przeglądarki: Edge/Chromium, WebView2 Runtime, Chrome DevTools Protocol (CDP), CDP events, DevTools network/console, WebSocket w trybie CLI, DOM extractor, semantic page model, JavaScript actions, realne zdarzenia klawiatury/myszy, iframe, popup.
13. [DO ZROBIENIA] AI: model i rzeczywiste ustawienia produkcyjne, OpenAI Responses API, JSON function calling/tool calls, AgentRunner, SystemPrompt, ToolRegistry, model input/output, reasoning effort, kontekst zadania, retry i limit kosztów/tokenów. Rozróżnić model nadrzędny ChatGPT/Claude od wykonawczego modelu Luny.
14. [DO ZROBIENIA] Protokoły i komunikacja: MCP, Streamable HTTP, JSON-RPC 2.0, HTTPS, lokalny HTTP, JSON REST tam gdzie jest, WebSocket/CDP, file-transfer HTTP PUT/GET oraz SHA-256; opisać dokładnie relacje, nie traktować wszystkich jako jednego protokołu.
15. [DO ZROBIENIA] Serwer MCP: endpoint lokalny 127.0.0.1:48731 w obecnej konfiguracji, usługa nasłuchująca, tools/list, tools/call, wersjonowanie kontraktu, task_id i idempotency_key; nie ujawniać sekretnego sufiksu URL.
16. [DO ZROBIENIA] Łączność z zewnątrz: Cloudflare Tunnel / wariant quick tunnel i stan skonfigurowanej nazwy/hostname, routingu i TLS. Osobno ustalić czy nazwany tunnel i własna domena rzeczywiście istnieją; nie twierdzić, że plan jest produkcją.
17. [DO ZROBIENIA] Środowisko: PBOX1, Windows, sesja interaktywna SessionId 1, proces OpenFill.exe, procesy WebView2, 4 logiczne CPU i rzeczywisty RAM, Task Scheduler/autostart/watchdog, zasady ponawiania procesu. W razie sprzeczności nazwy Windows z API weryfikować rzeczywisty system.
18. [DO ZROBIENIA] Pliki i dane: repo źródłowe, katalog instalacji, data\stable i data\dev, profil WebView2, config, historia zadań, notatki per domena, logi NDJSON, telemetry, folder współdzielony, katalog releases, backup/rollback. Wyświetlać lokalizacje w formie skróconej + „pokaż ścieżkę”.
19. [DO ZROBIENIA] Hasła i ochrona: Windows Credential Manager/WinCred API, DPAPI, szyfrowanie klucza, Firefox NSS, import poświadczeń, hasła tymczasowe, ograniczenia exact-origin, redakcja sekretów. W panelu jedynie NAZWY MECHANIZMÓW, nigdy loginów i haseł, tokenów, kluczy API, sekretnego MCP URL.
20. [DO ZROBIENIA] Architektura kodu: OpenFill.App, OpenFill.Core, OpenFill.Cli, AppHost, McpServer/McpService/McpTaskManager, AgentRunner, BrowserController, CDP, TabManager, HistoryStore, SiteNotesStore, ResourceTelemetry, CostGuard, DomainLocks, OutputLimiter, Redactor, SecretStore.
21. [DO ZROBIENIA] Model współbieżności: maks. 3 zadania, 5 kart w aktualnej konfiguracji 1.52, blokada jednej domeny, własność kart/pop-upów, idempotencja, kolejka i statusy, zachowanie stanu po restarcie; podkreślić limity odczytywane dynamicznie.
22. [DO ZROBIENIA] Testy i narzędzia: .NET SDK 10, MSBuild/dotnet CLI, własny test harness bez pakietów NuGet do testowania, izolowany serwer strony testowej, testy jednostkowe/integracyjne/E2E, logs i obsługa wersji Release.
23. [DO ZROBIENIA] Monitoring i diagnostyka: Process.TotalProcessorTime, Working Set, Private Bytes, GC heap, procesy potomne, uchwyty/wątki, idle/active, raporty NDJSON, screenshot/CDP DOM, logi konsoli/sieci, limity retencji.
24. [DO ZROBIENIA] Operacje wydawnicze: dotnet build, dotnet publish, katalog osobnego stagingu, sesja interaktywna, maintenance, backup, health check, rollback i aktualne skrypty PowerShell.
25. [DO ZROBIENIA] Integracje: ChatGPT, Claude i inne zgodne klienty MCP, Networking Radar (przycisk „Zapisz mnie”), przekazanie plików, potencjalny backend Google Calendar po potwierdzonej rejestracji; jasno rozróżnić istniejące i przyszłe połączenia.

### 3.3. Interaktywny materiał do nauki
26. [DO ZROBIENIA] Zbudować czytelne mapy przepływu: „ChatGPT -> MCP -> OpenFill -> Luna -> CDP/WebView2 -> strona -> wynik”, „Firefox -> import -> WinCred -> agent -> formularz”, „Radar -> idempotentne zgłoszenie -> OpenFill -> potwierdzenie”.
27. [DO ZROBIENIA] Przy każdym pojęciu dodać przykładowe zdanie „Zastosowałem to w OpenFill do...” i wskazać komponent implementujący funkcję. Bez dopisywania użytkownikowi nieistniejących doświadczeń.
28. [DO ZROBIENIA] Dodać wyjaśnienie decyzji architektonicznych, ograniczeń i kompromisów: WinForms zamiast WWW dla natywnej sesji, DOM/CDP zamiast obrazów jako domyślnego źródła, osobny model wykonawczy, MCP jako kontrakt integracyjny, vault poza promptem.
29. [DO ZROBIENIA] Zapewnić aktualizację zawartości panelu wraz ze zmianą wersji i config: statyczny manifest technologii generowany z kodu plus dynamiczny zestaw parametrów bez sekretów. Wersjonować manifest i testować zgodność panelu z projektem.
30. [DO ZROBIENIA] Test odbiorczy: po uruchomieniu rzeczywistego wydania panel od razu widać nad adresem, działa rozwinięcie, wyszukiwanie, kopiowanie, właściwy układ na małym oknie, nigdzie nie ujawnia poświadczeń. Weryfikacja screenshotem i ręczny smoke w sesji Windows 1.

## 4. Priorytet operacyjny: wydajność laptopa, RAM/CPU, WebView2
31. [CZĘŚCIOWO] Utrzymać niskokosztową telemetrię całego drzewa OpenFill oraz rzeczywistych procesów potomnych, bez wliczania obcych WebView2. Zachować obecne 30 sekund między próbkami, 7 dni retencji, limit 100 MB, chyba że pomiary uzasadnią zmianę.
32. [DO ZROBIENIA] Opisać reprodukowalne profile obciążenia: czysty start; 1/3/5 kart; 0/1/3 zadania; strona lekka/ciężka; logowanie i popup; długa bezczynność 30/60/180 minut; wiele zadań zakończonych; wielogodzinna praca.
33. [DO ZROBIENIA] Oddzielić CPU całej maszyny od zużycia jednego logicznego procesora, Working Set sumowane od faktycznych unikalnych stron RAM, Private Bytes od Commit, .NET managed heap od Chromium. Pokazać dane z jednostkami i oknem czasowym.
34. [DO ZROBIENIA] Przy każdej analizie oznaczać próbki jako idle/active/waiting oraz sprawdzać luki czasowe, zmiany PID, start procesu, liczbę kart, WebView2, wątki i uchwyty. Nie liczyć nieciągłych próbek jako pełnej godziny.
35. [GOTOWE W ŹRÓDŁACH] releaseFinishedTabs: po zadaniu porzucić renderowanie strony, zachowując ostatni URL i historię zadania, z jedną gotową pustą kartą, oraz otworzyć adres przy kontynuacji.
36. [GOTOWE W ŹRÓDŁACH] Obsługa błędów zwolnienia: jeśli zamknięcie lub about:blank nie zadziałało, nie gubić śledzenia aktywnego fizycznego tab/renderer.
37. [GOTOWE W ŹRÓDŁACH] Powiązać popup loginowy z kartą inicjującą i zwolnić go po zakończeniu, bez zamykania ręcznie utworzonych kart użytkownika.
38. [DO ZROBIENIA] Po instalacji 1.53 porównać 30-60 minut przy porównywalnym stanie idle z 1.52: liczba kart/procesów, średnie CPU, Working Set, Private Bytes, najcięższe procesy, uruchomienia rendererów oraz wpływ na responsywność laptopa.
39. [DO ZROBIENIA] Przeprowadzić także test aktywnych realnych zadań, by zysk przy idle nie powodował utraty stanu formularzy, wzrostu kosztu odtwarzania czy porażek na stronach logowania.
40. [DO ZROBIENIA] Skontrolować przełączanie kart i aktywność WebView2 przy zamknięciu/przywróceniu, otwieraniu wielu zadań oraz pytaniach wymagających uwagi człowieka; przechowywać tylko dane niezbędne do kontynuacji.
41. [DO AUDYTU] Zweryfikować, czy każda kończąca się ścieżka TaskRun (success, partial, failed, cancelled, blocked, stopped_by_user, timeout, wyjątek) wywołuje ten sam cleanup tab/popup/connection, również po awarii.
42. [DO AUDYTU] Sprawdzić event handlery, WebView2CdpConnection.DisposeAsync, przypięcia CDP, monitor konsoli/sieci, dispose i uwolnienie kontrolki WebView2. Testować liczbę uchwytów, wątków i rendererów przed/po zadaniu.
43. [DO AUDYTU] Określić czy puste karty są naprawdę lekkie i czy przełączenie na about:blank zwalnia realny renderer; nie przyjmować tego na wiarę po sukcesie testu mock.
44. [BLOKER] Hipoteza wpływu --disable-background-timer-throttling na wysokie CPU wymaga izolowanego eksperymentu porównawczego; wcześniejszy eksperyment zmiany flag był konkretnie blokowany, więc nie ponawiać go drogą alternatywną.
45. [BLOKER] Powiązanie renderer PID z konkretnym tab/site: wcze��niejsze działania eksperymentalne były zablokowane. Zachować problem w backlogu, do czasu dozwolonej metody nie przypisywać witryn na podstawie domysłu.
46. [DO ZROBIENIA] Zaprojektować minimalny koszt pamięci podczas bezczynności jako mierzalny cel. Początkowa hipoteza odbioru: obniżyć średnie idle CPU co najmniej o połowę względem porównywalnego baseline i istotnie zredukować liczbę utrzymywanych rendererów oraz WS; progi końcowe ustalić według danych i funkcji wymagających stałych sesji.
47. [PROPOZYCJA] Ustanowić alert diagnostyczny, gdy nie ma zadań, ale CPU i RAM przekraczają ustalony próg przez dłuższy czas; bez automatycznego zabijania procesu ani cudzych kart. [PROPOZYCJA]
48. [DO ZROBIENIA] Doprowadzić do weryfikowalnej stabilności działania laptopa także po wielogodzinnym uptime, nie tylko bezpośrednio po restarcie.

## 5. Poświadczenia i logowanie: pełny zakres
49. [GOTOWE W ŹRÓDŁACH] Import wielu zapisanych kont Firefox w jednym przebiegu, zaznaczanie 2+ loginów per domena, zmiana filtra bez utraty zaznaczeń, jeden import, wynik per rekord.
50. [GOTOWE W ŹRÓDŁACH] Widok Credentials: lista origin i loginów, wyszukiwanie i odświeżanie bez odczytu haseł. Narzędzia Luny: list_imported_logins i use_imported_password z loginem i wybranym origin.
51. [DO ZROBIENIA] Przeprowadzić natywny test UI rzeczywistego wydania: zaznaczyć 6 wpisów z 3 domen po kolejnych filtrach; sprawdzić zachowanie zaznaczeń, liczbę zaznaczonych, podgląd, jednorazowe potwierdzenie i wynik.
52. [DO ZROBIENIA] Po imporcie automatycznie odświeżyć widok zapisanych loginów lub zapewnić bezpośrednie przejście do tego widoku, bez restartu; sprawdzić działanie także po ponownym uruchomieniu OpenFill.
53. [DO ZROBIENIA] Sprawdzić per wpis status Created/Updated/Failed, rzeczywisty readback, odporność na częściowy błąd, brak duplikatów przy ponowieniu oraz brak haseł w logu.
54. [DO ZROBIENIA] Zweryfikować logowanie z wybranym kontem na realnym formularzu, z wypełnianiem zarówno loginu, jak i hasła, a przy loginie wieloetapowym poprawne wypełnienie samego hasła.
55. [DO ZROBIENIA] Sprawdzić wybór loginu przy kilku kontach, brak wskazanego loginu, powiązane subdomeny, exact-origin, rozróżnienie schematu HTTP/HTTPS, niestandardowe porty, przekierowanie do providera SSO.
56. [GOTOWE W PRODUKCJI] SEOHost: wcześniej zanotowano poprawne rzeczywiste uwierzytelnienie zaimportowanym kontem. Nie odtwarzać logowania tylko po to, by powtarzać już udowodniony fakt; test po nowym wdrożeniu ma charakter regresji.
57. [DO ZROBIENIA] CyberFolks: pokazać w Credentials konto 04229383 i tosiapl w panel.cyberfolks.pl oraz osobny origin now.cyberfolks.pl; ponowić poprawnie ukierunkowany test tylko na istniejącym, właściwym koncie i odróżnić odrzucenie serwera od błędu vault.
58. [DO ZROBIENIA] Uzupełnić testy błędnych/starych poświadczeń: powiadomienie z jasną klasą przyczyny i właściwym screenshotem/DOM, bez automatycznego resetowania hasła.
59. [CZĘŚCIOWO] Lokalna strona testowa już obsługuje 2+ kont i prawdziwy submit. Rozszerzyć pokrycie o trwałość po restarcie, wiele domen, duplikację, odmowę loginu i brak pola loginu, a także układ UI z autouzupełnianiem.
60. [GOTOWE W ŹRÓDŁACH] Dodatkowy test 6 credentiali / 3 originów, Created/Updated, weryfikacji wartości WinCred i cleanup ma testy automatyczne. Nie twierdzić, że to samo co ręczny test importu Firefox.
61. [DO ZROBIENIA] Zweryfikować pełną ochronę granic „same site family”. Najnowsza doraźna lista suffixów zapobiega myleniu github.io/pages.dev/s3.amazonaws.com, lecz nie pokrywa całej Mozilla Public Suffix List.
62. [DO ZROBIENIA] Zastąpić niepełną heurystykę suffixów sprawdzonym PSL, z aktualizacją danych, cache oraz testami tenantów, prywatnych suffixów, IDN/punycode, IP, localhost, portów i public/private suffix rules.
63. [DO ZROBIENIA] Nigdy nie dobierać poświadczenia innego tenanta pod wspólnym publicznym suffixem. Exact-origin jest domyślną granicą, cross-origin może być tylko jawną świadomą akcją.
64. [DO AUDYTU] Sprawdzić zachowanie z Firefox Primary Password i z wieloma profilami Firefox bez przeglądania lub ujawniania wszystkich sekretów.
65. [DO ZROBIENIA] Hasła generowane przy zakładaniu kont: po zapisie do Windows Credential Manager przekazywać wiarygodny rezultat, login, serwis, informację o tymczasowości, instrukcję zmiany i obowiązek uaktualnienia zapisu.
66. [ODROCZONE, TRELLO 0J35AKcd] Osobny przypominający komunikat o trwałym zapisaniu/zmianie hasła tymczasowego pozostaje w backlogu „na później”. Karta nie upoważnia sama do automatycznego zapisywania jawnego hasła w jej opisie.
67. [DO AUDYTU] Przepływ temporary_credentials przez wynik MCP, lokalną historię i redakcję: potwierdzić dokładnie, gdzie hasło tymczasowe pojawia się jawnie i czy te miejsca odpowiadają zadeklarowanej polityce. Nie kopiować sekretów do dokumentacji technicznej.
68. [DO AUDYTU] Secret channel dla przekazania OTP/credentialu przez model nadrzędny, jeśli bieżące narzędzia nie przyjmują tych danych; ma ograniczać powtarzanie i ekspozycję, bez obchodzenia zabezpieczeń.
69. [DO ZROBIENIA] Dopilnować, że listy i UI zawsze mają dokładnie to samo źródło prawdy co agent, a „hasło zapisane” i „hasło działa na serwerze” są odrębnymi wynikami.

## 6. Agent Luna, decyzje, narzędzia, prompty, walidacja wyniku
70. [DO AUDYTU] Potwierdzić w nowym buildzie komplet narzędzi: get_page, act, act_many, navigate, upload_file, wait, get_network, get_console, screenshot, read_text, search_logs, read_site_note, write_site_note, list_imported_logins, use_imported_password, save_temporary_password, ask_user, confirm_irreversible, finish, report_gap i ewentualne nowe.
71. [WYMAGANE] Zleceniodawca przekazuje komplet celu, kontekstu, konta, domeny, granic i oczekiwanego dowodu. Luna wykonuje czynności, a nie zaczyna równoległego ogólnego researchu ani samodzielnego procesu biznesowego.
72. [DO AUDYTU] ask_user powinno prawidłowo kierować rozstrzygalne pytania do modelu nadrzędnego, a do człowieka tylko wymagające jego udziału; oddzielić needs_input od waiting_for_user.
73. [DO AUDYTU] confirm_irreversible: jawnie zlecone zakładanie kont, wysłanie wskazanej wiadomości, upload i zwykły submit nie powinny generować bezsensownych powtórzeń zatwierdzenia; osobno utrzymać kontrolę płatności i innych rzeczywistych zmian nieobjętych zadaniem.
74. [DO AUDYTU] finish i RunConclusion: pełny zweryfikowany status, identyfikator konta, użyta domena, dowód wykonania, ewentualny wynik częściowy, dane tymczasowe przekazane uprawnionemu klientowi, brak fałszywego „done”.
75. [DO ZROBIENIA] Rozbudować regresje błędów narzędzi, błędnej odpowiedzi modelu, powtarzania kroków, 400/timeout API, kosztów, przerwania przez człowieka, częściowego sukcesu i przypadków bez możliwości dalszego postępu.
76. [DO ZROBIENIA] Weryfikować ukończenie akcji po zmianie DOM, odpowiedzi serwera lub realnym ekranie potwierdzenia, a nie po samym kliknięciu Submit.
77. [DO AUDYTU] SystemPrompt aktualizować minimalnie: niepotrzebny kontekst/tokeny podnoszą koszt i mogą pogarszać wybór narzędzi. Wymagać deterministycznej ścieżki do notatek domeny i poprawnego domknięcia zadania.
78. [DO AUDYTU] Testy rzeczywistych „human tasks” na nieznanych agentowi witrynach i fikcyjnych danych, bez preferowania zoptymalizowanych na pamięć konkretnych portali. Nie wprowadzać Playwright jako dodatkowego wykonawcy.
79. [DO AUDYTU] Porównać koszt i skuteczność typowych pełnych zleceń z częściowymi zadaniami. Podział dużych operacji deweloperskich nie oznacza przekształcenia każdego zlecenia Luny w 50 mikrozleceń.

## 7. Wiedza o domenach i niezależność klientów
80. [GOTOWE W PRODUKCJI] SiteNotesStore: odczyt na wejściu, merge bez utraty wcześniejszych wpisów, deduplikacja, korekty oznaczonych faktów, zapis atomowy, restart, odrzucanie sekretów. Potwierdzone w 1.52, ale trzeba utrzymać regresję.
81. [DO AUDYTU] Zgodność instrukcji Luny z zachowaniem: read_site_note dla nowej domeny przed get_page; obowiązkowy odczyt na starcie przy znanym origin; minimalny zapis nowej wiedzy na końcu; brak zapisu, gdy nie ma nowej wiedzy.
82. [DO ZROBIENIA] Testy notatek z przekierowaniami, SSO, zmianą kraju/locale, poprawkami starego przepisu, nieaktualnymi selectorami, nieudanymi akcjami, notatką równoległą z dwóch czatów, po restarcie procesu i awarii podczas zapisu.
83. [DO ZROBIENIA] Dla zadań READ-ONLY z Networking Radar dodać (lub zweryfikować istniejący) lekki per-domain playbook: udowodniona ścieżka URL, selektor/click, data ostatniego testu, locale, przyczyna błędu, termin przydatności. Bez kopii sekretnych i personalnych wartości.
84. [DO ZROBIENIA] Notatka ma być danymi pomocniczymi, nie instrukcją nadrzędną. Weryfikować odporność na prompt injection w treści stron i w zapisanych notatkach.
85. [DO ZROBIENIA] Odróżnić odczyt faktu wydarzenia „max 2-3 kliknięcia” od pełnego zlecenia rejestracji, które może wymagać dłuższej pracy. Ograniczenie 2-3 kliknięć z Trello dotyczy diagnostyki/read-only w tym workflow, nie ogólnego silnika.
86. [DO AUDYTU] Nadmiar informacji z sąsiednich hostów ograniczyć, aby nie podawać obcego loginu/notatki jako właściwej stronie. Integracja z PSL i exact-origin.
87. [DO ZROBIENIA] Ustandaryzować wersjonowanie/śledzenie zmian operacyjnych notatek: odróżnić nowy fakt, korektę i domniemanie; ustalić retencję historycznych wpisów, bez przepisywania istniejących 100+ notatek.

## 8. Przeglądarka i realne wykonywanie działań
88. [GOTOWE W PRODUKCJI] DOM extraction, semantyczny model strony, act_many, kontrole formularzy, klawiatura/mysz, network/console i screenshot fallback mają już testy i wdrożenie. Nie projektować ich od zera.
89. [DO AUDYTU] Trudne formularze: React controlled inputs, autocomplete, date picker, Select2, contenteditable, przełączniki, custom list, modal, upload, zgody cookies, wieloetapowe formy, walidacja asynchroniczna, confirm()/alert().
90. [DO ZROBIENIA] Zgłoszenia screenshotów z Trello: zestawić get_page/DOM, screenshot aktualnej karty oraz decyzję narzędzia przy błędzie „widzę hasło, nie widzę loginu”, ukrytym modalu, treści w iframe, nietypowym dropdownie.
91. [DO ZROBIENIA] Automatycznie dołączać do raportu błędu sanitizowaną diagnostykę: identyfikator zadania, origin, stan strony i formularzy bez sekretów, screenshot za zgodą kontekstu, logi CDP i minimalny odtwarzalny przebieg.
92. [DO AUDYTU] Cross-origin iframe nadal ma ograniczenia w get_page (README); rozważyć udokumentowany fallback do screenshotu i prawdziwych kliknięć, bez fałszywego zapewnienia, że struktura jest widoczna.
93. [DO AUDYTU] Pop-up SSO Google/Microsoft/Apple/Facebook, przełączenie CDP do popup i powrót, domykanie okien tylko po zako��czeniu właściciela, niekolidujące logowania z wielu projektów.
94. [DO AUDYTU] CAPTCHA/Turnstile i interaktywne kroki: stan waiting_for_user w lokalnym UI, bez wykonywania niedozwolonych obejść, z możliwością przekazania poprawnej odpowiedzi przez uprawnionego klienta, jeśli to zwykły kod z jego poczty.
95. [DO AUDYTU] Download, upload, pick-file i realna obsługa lokalnej ścieżki: nie zgadywać nazwy pliku, upewniać się że pobranie zakończone oraz że plik jest poprawny; testować wielkość i integralność.
96. [DO ZROBIENIA] Testy UI operacyjnego zadań: wyświetlanie bieżącego i historycznych celów, źródła requester/chat, kolorów, procesu, błędów, kart potrzebujących człowieka, oczekiwania i ostatniego zakończenia.
97. [DO AUDYTU] Przy odświeżeniu interfejsu lub powrocie do zadania nie tracić historii, informacji o źródle ani skutku wcześniejszego działania.
98. [DO ZROBIENIA] Natywny smoke GUI po legalnym wdrożeniu, w tym ograniczone okno, fullscreen, rozdzielczość/DPI i nowy panel technologii; nie podstawiać samych testów DOM za test Windows Forms.

## 9. MCP, sieć, transport i integracje modelu nadrzędnego
99. [GOTOWE W PRODUKCJI] Wbudowany serwer MCP w trybie Streamable HTTP, JSON-RPC 2.0; usługa uruchamiana lokalnie na PBOX1 i udostępniana klientom za autoryzowanym HTTPS.
100. [DO AUDYTU] Kontrakt narzędzi dla ChatGPT, Claude i innych klientów: start, status, reply, cancel, info, screenshot, file_transfer, maintenance. Sprawdzić świeże tools/list, bo klient może przechowywać starszy katalog narzędzi.
101. [DO AUDYTU] Właściwości requester i chat mają identyfikować źródło zadania i pozostawać stabilne. Wielu klientów nie może przejmować cudzych zadań, a jeden chat nie może zmieniać nazwy innego czatu.
102. [DO AUDYTU] Stany: running, needs_input, waiting_for_user, done, failed, blocked, cancelled, stopped_by_user, interrupted, domain_busy, maintenance i inne odmowy. W rezultacie końcowym osobno outcome success/partial/failed.
103. [DO AUDYTU] idempotency_key: powtórzone identyczne zgłoszenie ma zwracać ten sam task_id także po restarcie; inna treść z tym samym kluczem ma być odmową, a nie nową akcją; powtórzenie sieciowego POST nie może tworzyć dwóch kont/rejestracji.
104. [DO AUDYTU] Kontynuacja po przerwie: jawne continue_task_id, ostatnie URL i kroki z pamięci, wznowienie z weryfikacją aktualnej strony, nie zakładać zachowania tymczasowego stanu formularza po zwolnieniu karty.
105. [DO ZROBIENIA] Test kompatybilności ChatGPT i Claude na bieżącej produkcji: bez sekretów MCP w promptach, brak zależności od starego cache listy narzędzi.
106. [DO AUDYTU] Endpoint lokalny i zewnętrzny mają ten sam autoryzowany kontrakt, ale secret path, tokeny transferu i główny URL nie mogą trafić do frontendowego JS ani publicznego logu.
107. [DO AUDYTU] Cloudflare Tunnel: ustalić, czy istnieje nazwany tunel openfill-pbox1 i własny DNS, czy aplikacja nadal używa quick tunnel; rozróżnić wykonane od próby rejestracji zatrzymanej na formularzu. Bez kupowania domeny czy zmiany nameserverów bez ustalonego celu.
108. [DO ZROBIENIA] Test reconnect po awarii sieci, chwilowym braku tunelu, restarcie procesu i zmianie URL; zgłoszony błąd starego połączenia MCP nie może unieruchamiać wszystkich klientów.
109. [DO AUDYTU] Transmisja plików: oddzielne krótkotrwałe tokeny transferowe, HTTP PUT/GET, checksum SHA-256, ograniczenia nazwy/ścieżki/rozmiaru, jednokrotny upload i powiadomienie o końcowym zapisie.
110. [DO ZROBIENIA] Sprawdzić limit i koszt transportu screenshotów, obrazów, odpowiedzi DOM oraz plików, bez wysyłania wielkich payloadów w JSON-RPC tam, gdzie istnieje file transfer.
111. [DO AUDYTU] Nie mieszać Lupus Commander MultiHost z kanałem MCP OpenFill: MultiHost służy do obsługi komputera deweloperskiego, OpenFill do sterowania stronami WWW. To dwa niezależne serwery/narzędzia.

## 10. Zadania, współbieżność i odporność na restart
112. [GOTOWE W PRODUKCJI] Maksymalnie 3 równoległe zadania na różnych domenach w obecnym config, limit 5 kart, blokada współbieżności jednej domeny, historia task_id z callerem i aktualnym stanem.
113. [DO AUDYTU] Testy z 3 zlecającymi modelami równolegle, 3 różnymi domenami i próbą 4. zadania; odmowy mają być czytelne, nie blokować już uruchomionych.
114. [DO AUDYTU] Zmiana strony w ramach jednej domeny nie może utracić blokady, nie może przypadkowo uznać innej subdomeny/tenanta za tą samą domenę (PSL).
115. [DO AUDYTU] Sprzątanie po zakończeniu każdego stanu zadania; oddzielnie karty ręczne, free, running, waiting, finished, interrupted; wznowienie po restarcie i po timeout.
116. [DO ZROBIENIA] Wyczerpujące testy lifecycle procesu: wyłączenie i ponowny start usługi w bezpiecznym środowisku testowym, restart Windows, brak sieci, przerwanie modelu, błąd CDP, odłączenie klienta, brak odpowiedzi użytkownika.
117. [DO AUDYTU] Zachować state/notes/history w data\stable; build/testy źródeł nie powinny zapisywać w profilu produkcji. Upewnić się, że testy tworzą i usuwają własne dane.
118. [DO AUDYTU] Czy tasky zewnętrznego klienta mogą uruchomić ponownie tę samą rejestrację: dopilnować idempotency i nieprzekraczania limitu domeny/kwoty.
119. [DO ZROBIENIA] Przetestować stany waiting_for_user przez realistyczną sytuację CAPTCHA lub potrzebę ręcznej interakcji i ich wznowienie po upływie czasu, bez bezpowrotnego zwolnienia koniecznego formularza.
120. [DO AUDYTU] Widoczna dla użytkownika historia i log każdej operacji musi wskazywać, kto ją zlecił, na jakiej domenie, który task_id, jaki wynik i koszt. Nie pokazywać poświadczeń innych zadań.

## 11. Koszty modeli, liczba kroków, pętle i limity
121. [GOTOWE W PRODUKCJI] CostGuard/RunMeter mierzy tokeny, koszty, stawki modeli, limity na stronę i lifetime, wielokrotne akcje, zapętlenie.
122. [DO AUDYTU] Aktualna produkcyjna konfiguracja z ostatniego potwierdzonego handoffu: model gpt-6-luna, reasoning medium, maxSteps 90, maxStepExtensions 5, costInfoUsd 1, costWarnUsd 2, domainLimitUsd 5, domainLifetimeLimitUsd 15. Przed pokazaniem jako bieżące sprawdzić config, nie czytać README jako źródła parametrów.
123. [DO AUDYTU] Obsługa modeli bez cennika: koszt nie może być przedstawiony jako 0 USD przy nieznanej cenie; w takiej sytuacji prezentować „nieznany” i tokeny.
124. [DO AUDYTU] Czy blokady per domena i lifetime przetrwają restart i czy można je zdjąć tylko przez właściwą akcję użytkownika.
125. [DO ZROBIENIA] Oddzielić limity kosztu prawdziwego zadania użytkownika od kosztu eksperymentalnego testu, odczytu wydarzenia i procesu diagnostycznego.
126. [DO AUDYTU] W panelu pokazać czytelnie tokeny, koszt, progress, ograniczenie rund modelu, powód ewentualnego przerwania; nie obciążać UI dodatkową telemetrią na każdej klatce.
127. [DO ZROBIENIA] Test długiego zadania z małym postępem: sprawdzić ostrzeżenia i przerwanie kosztów zamiast wielogodzinnej pracy bez wyniku. Progi specyficzne dla jednego eksperymentu (np. $0.15) nie zastępują globalnych ustaleń z config.
128. [DO AUDYTU] Agent ma raportować „partial”, gdy nie zdobył dowodu końcowego mimo wielu wywołań, a nie konsumować koszt aż do końca limitu.

## 12. Sekrety, bezpieczeństwo i integralność danych
129. [GOTOWE W PRODUKCJI] Windows Credential Manager jest głównym magazynem importowanych i tymczasowych haseł; klucze OpenAI są chronione DPAPI. Nie przenosić ich do UI technologicznego.
130. [DO ZROBIENIA] Przejść przez przepływy danych i ustalić gdzie występują: hasło tymczasowe, login, token MCP, token Cloudflare, API key, cookie sesyjne, dane formularzy, screenshot z prywatnymi treściami i historii.
131. [DO AUDYTU] Czy redakcja sekretów działa w logach zdarzeń, network/console, tool output, screenshotach, notatkach domenowych, błędach oraz raportach E2E. Inna polityka dla chwilowego zwrotu hasła tymczasowego do autoryzowanego klienta.
132. [DO ZROBIENIA] Test odrzucania traversal w upload, download i folderze współdzielonym; nie wolno czytać plików poza uprawnionymi katalogami.
133. [DO AUDYTU] Ustrukturyzować politykę źródeł: strona WWW i notatki są danymi, a nie nowymi instrukcjami nadpisującymi zadanie; identyfikować prompt injection.
134. [DO ZROBIENIA] Zweryfikować dostępność HTTPS tunelu i autoryzację, ponawianie tokenów, rotację, unikanie ich przypadkowego ujawnienia w komentarzach/deploy logach.
135. [DO AUDYTU] Tylko uprawniony klient może użyć zadań własnej konwersacji; brak ekspozycji poświadczeń albo historii innego projektu w task_info czy screenshot.
136. [DO AUDYTU] Dopuszczalne działania na zewnętrznej stronie i żądania potwierdzenia mają odróżniać zmianę profilu, wysłanie wiadomości, rejestrację i płatność. CAPTCHA/Turnstile nie omijać.
137. [DO ZROBIENIA] Wdrożyć/udokumentować zasady backupu notatek, config, profilu i vault. Nie zakładać, że kopiowanie profilu WebView2 jest tym samym co backup Windows Credential Manager.
138. [DO AUDYTU] Przechowywanie tymczasowych haseł w zadaniach i Trello: decyzje użytkownika co do dodatkowego magazynu muszą być odrębne od technicznej konieczności ich utrwalania. Nie publikować haseł w tekście planu ani panelu.

## 13. Windows, produkcja, autostart, watchdog, backup i rollout
139. [DO ZROBIENIA] Usunąć sprzeczności dokumentacji instalacji: istniejący deploy_launch odnosi się do 1.50 i do nieistniejącej wcześniejszej ścieżki deploy_safely. Ujednolicić źródło aktualnej dozwolonej procedury, lecz nie uruchamiać zabronionej operacji.
140. [DO ZROBIENIA] Zdefiniować powtarzalny proces: source -> restore/build/publish -> osobny staging release -> kontrola zawartości i wersji -> testy -> preflight runtime -> maintenance -> brak cudzych running tasks -> backup/rollback -> wdrożenie w SessionId 1 -> health check -> UI smoke -> telemetry. Każdy etap ma osobny rezultat i blokadę; nie zastępować jej inną drogą.
141. [GOTOWE W ŹRÓDŁACH] Kod 1.53 buduje się w Release, a testy z ostatniego checkpointu dały 634 PASS i 0 FAIL. To przygotowanie źródeł, a nie dowód działania po instalacji.
142. [BLOKER] Staging 1.53: wcześniejsze uruchomienie przygotowanego stage script zostało odrzucone przez zabezpieczenia. Odmowa dotyczy tej konkretnej operacji. Nie wykonywać jej innym narzędziem lub przez rozdrobnienie tej samej czynności.
143. [BLOKER] Safe deployment musi działać w interaktywnej sesji Windows >0. MultiHost shell znajduje się w SessionId 0, więc nie może być bezpośrednim instalatorem GUI. Wymagany dozwolony kanał interaktywnej sesji.
144. [DO ZROBIENIA] Gdy pojawi się dozwolona droga wdrożenia, sprawdzić ponownie openfill_info, openfill_maintenance, aktualne zadania, brak nowych zgłoszeń, stan istniejącego procesu, backup danych i możliwość rollback.
145. [DO ZROBIENIA] Przed aktualizacją sprawdzić, że wersja binarki w stagingu naprawdę odpowiada ostatnio przetestowanemu kodowi (hash, product version, pliki Core/CLI/Assets, daty).
146. [DO ZROBIENIA] Uruchomić nowy proces dopiero po zamknięciu starego i zachowaniu stanu; potwierdzić SessionId 1, właściwe profile/data\stable, panel i MCP. Po błędzie dokonać rollback i sprawdzić wcześniejszą wersję.
147. [DO ZROBIENIA] Zweryfikować działanie Task Scheduler, autostartu użytkownika, watchdog, opóźnienia przy logowaniu Windows, obsługi crash/restart oraz unikania dwóch jednoczesnych instancji.
148. [DO AUDYTU] Czy wdrożenie przypadkowo zmienia aktualny model, flagi WebView2, profil użytkownika, port MCP, tunel, dane i ścieżki. Aktualizacja programu nie może resetować kont.
149. [DO ZROBIENIA] Wydzielić protokół akceptacji: build 0 błędów, komplet testów 0 FAIL, maintenance+0 tasks, wersja po instalacji, UI działa, co najmniej 1 realne poprawne logowanie i 1 pełny przebieg MCP, RAM/CPU porównane z baseline.
150. [DO ZROBIENIA] Rozwiązać ostrzeżenie MSB3277 WebView2.Wpf/WindowsBase w osobnym dozwolonym kroku po zbadaniu referencji pakietów; nie kasować ostrzeżenia „w ciemno”, jeżeli wymagałoby inwazyjnej podmiany DLL.
151. [DO AUDYTU] Test zachowania aktualizatora na profilu z 100+ site notes, kilkoma importowanymi kontami i historią zadań: nie usuwać danych ani haseł.
152. [DO ZROBIENIA] Dodać prosty protokół diagnostyczny „co jest wdrożone” po restarcie: wersja exe, data build, status MCP, liczba tabów, config bez sekretów i link do zatwierdzonego release report.
153. [PROPOZYCJA] Stopniowa publikacja: najpierw techniczny smoke w sesji testowej, potem kontrolowane uruchomienie produkcyjne i obserwacja. Nie zmieniać profilu produkcyjnego tylko dla utworzenia sztucznego demo.
154. [DO AUDYTU] Czy nowy panel technologii i jego assety trafią do paczki publikacyjnej z wersją 1.54 lub kolejną, a nie zostaną tylko w lokalnym źródle 1.53.

## 14. Networking Radar, zdarzenia, Calendar i zewnętrzne workflow
155. [CZĘŚCIOWO] Połączenie Radar -> OpenFill („Zapisz mnie”) jest technicznie dostępne, idempotency_key, polling statusu, uniknięcie dwóch zapisów, limity i rozróżnienie wyników mają testy. Karta Trello 92Jv4POM pozostaje w Review / Test.
156. [DO ZROBIENIA] Jeden prawdziwy, celowo wybrany test rejestracji wydarzenia przez przycisk „Zapisz mnie”: poprawny oficjalny URL, dane konta, formularz, outcome=success i jednoznaczne potwierdzenie od organizatora. Nie tworzyć losowych rejestracji tylko na potrzeby testu.
157. [DO ZROBIENIA] Test odporności na drugi klik i retry po utracie odpowiedzi, ten sam idempotency_key ma zwrócić ten sam task_id, bez drugiego zgłoszenia.
158. [DO ZROBIENIA] Oddzielić Zadanie A: read-only weryfikacja ceny/terminu/miejsca, mały zakres nawigacji, screenshot/DOM gdy potrzebne, od Zadania B: wykonanie rejestracji użytkownika. Różne cele, zakres uprawnień i kryteria sukcesu.
159. [DO ZROBIENIA] Po potwierdzonym fakcie rejestracji backend Radaru ma dodać/sprawdzić zdarzenie na właściwym koncie Google Calendar, z prawidłową datą, strefą, miejscem, organizatorem i URL. Nie oznaczać wpisu w kalendarzu jako dowodu rejestracji, gdy nie ma potwierdzenia organizatora.
160. [DO AUDYTU] Czy Radar prawidłowo podaje mail, projekt, konto użytkownika, wymagane pola, limit ceny i preferencję darmowych wydarzeń; nie przesyłać danych innego projektu/firmy.
161. [DO ZROBIENIA] Obsługa CAPTCHA, odmowy, czekania na mail, wyczerpania limitu oraz rejestracji częściowej, bez automatycznej próby drugi raz.
162. [DO ZROBIENIA] Sanitizowany raport screenshotów i playbooków z karty FV71I2uV, używany do diagnozy trudnych stron, bez sekretów i osobnych nieautoryzowanych loginów.
163. [DO AUDYTU] Integration contract: nazwy argumentów, wersje schema, transport MCP, statusy, informacje o kosztach i błędach. Wersjonowany dokument powinien odpowiadać aktualnej produkcji, a nie tylko 1.49.
164. [DO ZROBIENIA] Przetestować file transfer w scenariuszu autoryzowanego wysłania załącznika i pobrania dokumentu potwierdzenia. Dołączyć hash/nazwę pliku, nie wrzucać pliku wprost do JSON.
165. [ODRĘBNY ZAKRES] Samodzielna budowa modułów źródeł Radaru, heurystyk wydarzeń, ocen i CRM to zadania projektu Networking Radar, nie backlog OpenFill. W OpenFill testować tylko jego funkcję wykonawczą i kontrakt.
166. [ODRĘBNY ZAKRES] Konkretne strony, konta marketingowe, publikacje i aplikacje do pracy nie są funkcjami OpenFill. Będą scenariuszami akceptacji, jeśli ich przebieg ujawni brak możliwości w narzędziu.

## 15. Macierz testów, kryteria jakości i regresja
167. [GOTOWE W ŹRÓDŁACH] Duży zestaw automatyczny .NET z własnym harness, lokalna strona testowa, oddzielne kategorie, wersjonowane logi. Ostatnia liczba testów: 634 PASS, 0 FAIL dla źródeł 1.53 według checkpointu 21:22.
168. [DO ZROBIENIA] Po dodaniu nowego panelu technologii: test źródeł danych, kolejności kategorii, renderingu, elementów expand/collapse, wyszukiwarki, kopiowania, braku sekretów, odczytu aktualnej wersji.
169. [DO ZROBIENIA] Regress: wszystkie testy tab/popup, EndAsync i failure recovery; 3 równoległe zadania, tab free/user/finished, restart, ponowne otwarcie URL, obsługa przy odmowie blank navigation/close.
170. [DO ZROBIENIA] Regress: importer Firefox, NSS, 6 loginów z 3 domen, różne profile, lista Credentials, powtórne importy, created/updated, exact-origin, tenant-family, hasło odrzucone przez serwer.
171. [DO ZROBIENIA] Regress: rzeczywista strona z dwoma kontami, lokalny submit i weryfikacja osoby, logowanie dwuetapowe i brak pola loginu, konto błędne, wieloznaczne, inny origin, brak sekretów w wyniku.
172. [DO ZROBIENIA] Regress: SiteNotesStore dedup, merge, corrections, corrupted JSON, równoległe zapisy, restart oraz poświadczenia na multi-tenant public suffix.
173. [DO ZROBIENIA] Regress: MCP idempotency, auth, start/status/reply/cancel, two requesters, domain busy, maintenance, stale client tools/list, file-transfer, screenshot.
174. [DO ZROBIENIA] Regress: portal trudnych formularzy, upload/download, React, iframe/popup, screenshot, confirm/modal, network log, dom vs realny widok.
175. [DO ZROBIENIA] Regress: CostGuard, pętle, limity, token/cena, przerwania, rollback statusu, bezczynny task, brak dodatkowych samoczynnych wydatków.
176. [DO ZROBIENIA] Test instalacji i rollback w autoryzowanym środowisku interaktywnej sesji Windows, watchdog i zadania autostartu, zachowanie profilu z realnymi sesjami.
177. [DO ZROBIENIA] Testy stabilności długoterminowej: minimum kilka godzin pracy i 30-60 minut bezczynności po instalacji, porównanie z baseline 1.52, brak pamięciowych regresji wraz z nowym panelem.
178. [DO ZROBIENIA] Test integracyjny z Networking Radar: rzeczywista intencjonalna rejestracja, pełny lifecycle, potwierdzenie, brak duplikatów, Calendar, cleanup testowego stanu gdzie to właściwe.
179. [DO ZROBIENIA] Każdy regresyjny przypadek musi mieć numer, dane wejściowe, wynik, a przy błędzie reprodukcję i screenshot/DOM bez sekretów. Nie bazować wyłącznie na 634 testach, które nie zawierają jeszcze nowego panelu.
180. [DO ZROBIENIA] Po każdej zmianie kodu: krótki test odpowiedniego modułu, potem full build i pełna regresja przed release. Powtarzać tylko gdy faktycznie kod się zmienił, nie uruchamiać kosztownych testów w bezcelowej pętli.
181. [DO AUDYTU] Usunąć przyczynę istniejącego ostrzeżenia MSB3277 na podstawie poprawnej referencji WPF/WinForms, jeśli można to zrobić bez destabilizacji. Jest ostrzeżenie, nie przyczyna potwierdzonego zużycia RAM.

## 16. Kod, architektura, dokumentacja i zaufanie do statusu
182. [DO ZROBIENIA] Zaktualizować README, INDEX, instrukcje i diagram architektury do realnej wersji 1.53+ oraz nowego panelu, bez starych dat i wartości limitów z 1.50.
183. [DO ZROBIENIA] Utrzymywać jeden aktualny, wersjonowany handoff opisujący nie tylko „co zrobiono”, ale rzeczywisty stan produkcji, testów, niedokończonych funkcji i blokad; starsze sekcje oznaczać jako historyczne.
184. [DO ZROBIENIA] Dokument „Technology Map” ma mieć jedno źródło prawdy wspólne z panelem na górze aplikacji: komponent, technologia, wersja, rola, diagram, adres repo i sposób rozmowy o nim.
185. [DO ZROBIENIA] Zaktualizować dokument kontraktu Networking Radar do aktualnych możliwości 1.53: nowy importer, lista loginów, screenshoty, idempotency, strukturę statusów.
186. [DO AUDYTU] Zabezpieczyć źródła z wersjami w nazwie. Repo nie miało pełnej historii Git; przeanalizować sens uruchomienia VCS/Git z .gitignore i lokalnym backupem, lecz nie twierdzić, że obecne historyczne wersje są w repo.
187. [DO AUDYTU] Sprawdzić, co w repo jest wersjonowanym kodem, plikiem testowym, danymi produkcyjnymi albo wygenerowanym obj/bin. Nie wprowadzać binarnych sekretów ani prawdziwego profilu WebView2 do repo.
188. [DO ZROBIENIA] Dokumentować decyzje techniczne: dlaczego WinForms/WebView2, dlaczego nie Playwright, dlaczego local agent, zalety/ryzyka MCP i tunelu, exact-origin, koszt modeli, pamięć witryn, procesy WebView2.
189. [DO ZROBIENIA] Przy każdej zmianie wersji: changelog, raport testów, lista zmodyfikowanych plików, hash paczki, procedura odtworzenia, znane błędy i dowód zdrowia po instalacji.
190. [DO ZROBIENIA] Przejrzeć z README „Ideas for later”: osobny zawsze aktywny host i stabilny nazwany tunel. Traktować jako odrębne potencjalne projekty, nie potajemny wymóg relokacji OpenFill z PBOX1.
191. [DO ZROBIENIA] Pokryć w dokumentacji konflikt: przechowywanie ukończonych kart do wznowienia w starej wersji vs zwalnianie kart w 1.53. Opisać, że URL się zachowuje, lecz niesubmitowany stan formularza może zniknąć.
192. [DO ZROBIENIA] Opisać bezpieczeństwo i zakres danych w Windows Credential Manager i DPAPI, w logach zdarzeń, w historii temporary_credentials, w screenshotach i notatkach bez samych sekretów.
193. [DO AUDYTU] Wersjonowanie nazwy każdego pliku (dokumentów, raportów, skryptów), aby użytkownik miał pełny ślad; cofanie starszej wersji nie powinno niszczyć nowszych danych i testów.

## 17. Kolejność realizacji i zależności, bez zatrzymywania niezależnych prac
Faza A: zatwierdzona inwentaryzacja stanu projektu oraz jeden master plan, niniejszy dokument. Po nowych ustaleniach aktualizować plan nową wersją.
Faza B: projekt panelu technologii z potwierdzoną listą źródeł i layoutem MainForm. Następnie implementacja warstwy stale widocznej i pełnego katalogu pojęć; nie wymaga zatrzymania produkcji.
Faza C: niezależnie naprawić niekompletną obsługę PSL, dodać testy tenant isolation i regresji; domknąć widoczność UI Credential oraz diagnostykę screenshotów i domenowych playbooków.
Faza D: osobno przeprowadzić analizę źródeł i dozwolone izolowane testy lifecycle, pamięci i CPU. Każda zmiana kodu ma mieć własny test i raport.
Faza E: zbudować całe źródła App/Core/CLI, uruchomić kompletną regresję, przygotować manifest wersji, kontrolę plików i plan rollback.
Faza F: wykonywać wyłącznie dozwolone etapy publikacji. Blokada konkretnej operacji stagingu nie blokuje kompilacji, dokumentacji, testów i statycznego preflightu. Nie traktować tej zasady jako pozwolenia na ponowienie zabronionej czynności inną drogą.
Faza G: po uzyskaniu dozwolonej interaktywnej sesji Windows, po maintenance/zero running tasks, wykonać istniejącą bezpieczną procedurę z rollback i potwierdzeniem poprawnej wersji.
Faza H: po instalacji przeprowadzić smoke GUI, importu Firefox, listy Credentials, prawdziwego logowania, nowego panelu technologii i komunikacji MCP, a następnie 30-60 min porównawczych pomiarów idle/active CPU/RAM.
Faza I: wykonać świadomy, rzeczywisty scenariusz rejestracji z Radaru bez duplikatów i z potwierdzeniem, a po stronie właściwego projektu sprawdzić wpis Google Calendar.
Faza J: wydać raport o realnie uzyskanej poprawie laptopa, dokumentację architektury do nauki przed rozmowami i zamknąć tylko zweryfikowane zadania.

**Zasada wykonawcza:** nie przerywać dozwolonych niezależnych działań tylko dlatego, że jeden z komponentów oczekuje na odblokowanie lub minęła godzina przypomnienia. Automatyzacja jest mechanizmem wznowienia, a nie ograniczeniem długości pracy.

## 18. Kryterium „cała praca zrobiona”
194. Panel technologii jest stale widoczny NAD paskiem adresu po uruchomieniu rzeczywistej aplikacji i pokazuje pełny, prawdziwy, rozwijalny katalog zastosowanych technologii, architektury i integracji bez sekretów.
195. Uzgodnione funkcje importu/loginów, pamięci domen, przeglądarki, task lifecycle, MCP i kosztów działają w ZAINSTALOWANYM wydaniu, nie tylko w mockach.
196. Test 6 kont z 3 domen przechodzi automatycznie oraz w rzeczywistym oknie importu Firefox; lista i wynik per rekord widoczne bez restartu.
197. Logowanie wybiera właściwy origin/login i oddziela odrzucenie przez serwer od błędu zapisu credentialu.
198. Wydanie ma zweryfikowaną wersję, pełną regresję, zdrowe MCP, działający watchdog/autostart, kopię rollback i poprawny start w sesji Windows użytkownika.
199. Zmierzono zauważalny spadek CPU/RAM w porównywalnych warunkach idle i active, bez utraty pracy równoległej, wznowienia i uwierzytelniania.
200. Radar może wykonać jeden rzeczywisty zapis na polecenie użytkownika, bez duplikatu, z dowodem sukcesu, a potem powstać ma właściwy wpis w Calendar; nie uznawać samego submit za sukces.
201. Istnieje końcowy wersjonowany dokument architektury i technologii służący również nauce przed rozmowami rekrutacyjnymi, komplet wyników testów, raport wdrożenia i lista rzeczy naprawdę jeszcze otwartych.
202. Nie wyłączać automatyzacji tylko dlatego, że źródła się kompilują. Zakończenie następuje po produkcyjnym odbiorze lub po wyczerpaniu wszystkich dozwolonych działań, gdy każdy nieukończony tor ma konkretny udokumentowany blocker.

## 19. Źródła i ostrożność interpretacji
- docs\handoff_complete__v1.3__2026-10-15_1535.md: misja, architektura, domeny, SEOHost/CyberFolks, importer, notatki, telemetria, historia wymagań.
- docs\performance_implementation_status__v0.5__2026-10-15_2122.txt: aktualne ustalenia w kodzie, testy 634/634, nowa poprawka i pozostała luka PSL, produkcja 1.52.
- docs\release_preflight_allowed__v0.2__2026-10-15_2105.txt: oddzielne warunki kompilacji, stagingu, interaktywnej sesji Windows i rolloutu.
- docs\integration_networking_radar__v0.3__2026-10-14_2259.md: JSON-RPC, idempotency, statusy, transfer i integracja; część opisów importerów jest historyczna.
- README__v0.17__2026-10-12_2126.md: ogólna architektura, ograniczenia i Ideas for later. Parametry kosztowe i daty są starsze niż te z konfiguracji 1.52.
- Trello Networking Radar https://trello.com/c/FV71I2uV : screenshot/DOM, domenowe playbooki, sanitizowany raport błędów.
- Trello Networking Radar https://trello.com/c/92Jv4POM : Zapisz mnie, brak zduplikowanej rejestracji, rzeczywisty test integracyjny i follow-up Calendar.
- Trello Moja tablica https://trello.com/c/0J35AKcd : odroczony komunikat o tymczasowym haśle i obowiązku zapisania i zmiany.
- Trello Lupus Consulting https://trello.com/c/ZKS9zb2P : sprawdzony wzorzec zakładania kont, dane projektowe, tymczasowe hasła, oznaczanie właściciela/organizacji.
- Karty Trello o marketingu, SEO, publikacjach, pozyskiwaniu pracy i samym wyszukiwaniu wydarzeń nie są backlogiem funkcjonalnym samego OpenFill. Mogą być wyłącznie scenariuszami jego testów.
- Zawartość tego dokumentu jest snapshotem uzgodnień i stanu z 2026-10-15. Przed wykonaniem pozycji trzeba zweryfikować aktualny kod i środowisko; nie zamykać zadania na podstawie starej deklaracji.

**Koniec planu OpenFill Master Action Plan v1.0.**

## Aktualizacja planu v1.1 — 2026-10-15 23:24
Ta sekcja ma pierwszeństwo przed starszymi statusami pozycji 1–202, jeżeli występuje sprzeczność. Nie stanowi wdrożenia.

1. Panel technologiczny (pozycje 1–30): zrealizowany w źródłach jako Tech Atlas z zawsze widocznym paskiem ponad adresem, rozwijanym katalogiem, wyszukiwaniem, wypowiedziami na rozmowę i kopiowaniem. Potrzebny natywny smoke i screenshot DPI po publikacji.
2. Firefox import (49–69): czyta httpRealm, timeCreated, timeLastUsed, timePasswordChanged, timesUsed i GUID; pokazuje nazwy urządzeń i trzy daty; dopuszcza pusty login; każdy odrębny GUID zachowuje osobne konto Windows Credential Manager. Na PBOX1 znaleziono osiem wpisów dokładnie http://192.168.0.1, m.in. Thomson, SKY Router, NETGEAR, WGR614v7, TP-LINK. Nie zgadywać aktualnego urządzenia z samego IP.
3. Vault OpenFill i standardowe podpowiedzi WebView2 to różne magazyny. W widoku Credentials dodano Use selected; MainForm jednorazowo podejmuje HTTP Basic z kontrolą dokładnego origin oraz realm. Realny test routera po publikacji otwarty.
4. Gdy Firefox nie ma httpRealm, pokazywać (no device label). Nie zgadywać sprzętu.
5. Hasła tymczasowe NIE są odroczone: istniejący save_temporary_password/TemporaryCredentialVault/MCP temporary_credentials z aktywnym przypomnieniem o trwałym zapisaniu, zmianie i aktualizacji hasła. Potrzebny realny test pełnego konta po publikacji.
6. Zamykanie: przycisk Close na Tech Atlas, przygotowanie MCP interrupted z instrukcją continue_task_id i trwałym statusem. MCP to request/response; rozłączony czat dowie się z następnego odczytu, nie z niegwarantowanego push. Test MCP 516 PASS/0 FAIL, test produkcyjnej interaktywnej sesji pozostaje.
7. Gmail: wiadomość użytkownika do Luny została odebrana, lecz pierwotne zadanie skończyło się partial. Nowe zadanie OF-20261015-2319-8261 przełączyło profil już zalogowanego konta, SUCCESS w 8 krokach, zapisało bezosobową notatkę mail.google.com po weryfikacji.
8. Testy: pełna regresja 647 PASS/0 FAIL przed dodaniem parser fixture, ostatnie szybkie testy po nim 65 PASS/0 FAIL. Nową pełną regresję należy uruchomić po ukończeniu kolejnych zmian.
9. Produkcja wciąż OpenFill 1.52, PID 5000 od 22:11:25. Nic nie wdrożono, nie wykonywano zablokowanego stagingu ani alternatywy obchodzącej zabezpieczenia. Nocna zgoda na restart nie zastępuje autoryzowanej interaktywnej sesji Windows 1.

### Następne działania
- Pełna regresja po nowym teście metadanych i pozostałych poprawkach, z logiem.
- Jednoznaczne wybieranie kilku wpisów tej samej domeny/IP przez agenta, w tym konta bez loginu: brak przypadkowego wyboru.
- Krótkie potwierdzenie w UI po wskazówce dla Luny: osobno odebrano i osobno wykonano.
- Staged Release wyłącznie dozwoloną drogą, potem GUI smoke i screenshot Tech Atlas, test BasicAuthenticationRequested na realnym urządzeniu, uruchomienie w Windows Session 1, porównanie RAM/CPU i ewentualny rollback.
- Niezależne działania techniczne nie zatrzymują się z powodu jednego blokera.

**Checkpoint:** docs/implementation_checkpoint__v0.6__2026-10-15_2323.md

## 20. NOWE WYMAGANIE 2026-10-16: kontrolowane wyłączenie / aktualizacja OpenFill

Ta sekcja zastępuje wcześniejszy warunek oczekiwania, aż running_tasks == 0, przy kontrolowanym uaktualnianiu. Użytkownik WYRAŹNIE zezwolił na przerwanie aktywnych zadań OpenFill. Nie upoważnia to do zatrzymywania pozostałych aplikacji, usług ani do omijania blokad narzędzi.

203. [WYMAGANE] Oddzielny tryb „Aktualizacja / zatrzymanie OpenFill” inicjowany lokalnie przez operatora, z podaniem przewidywanej niedostępności w minutach; domyślnie 10, ale operator może zmienić wartość. Operacja może przerwać WSZYSTKIE bieżące zadania OpenFill, również innych czatów.
204. [WYMAGANE] Przed zamknięciem: blokada nowych zadań i jasny komunikat: „Trwa aktualizacja OpenFill. Przewidywany powrót za około N minut. Twoje zadanie przerwano. Po ponownym połączeniu odczytaj jego status i sprawdź, co zostało wykonane. Kontynuuj przez continue_task_id albo zleć ponownie tylko brakujące kroki”.
205. [WYMAGANE] Powiadomić wszystkich osiągalnych klientów, którzy w tej chwili oczekują odpowiedzi na MCP openfill_start_task, openfill_task_status lub openfill_reply, przed zamknięciem transportu. Aktualny MCP jest wyłącznie POST JSON-RPC request/response, bez subskrypcji SSE; nie da się gwarantować push do nieaktywnych lub rozłączonych modeli. Nie opisywać niemożliwego push jako funkcji.
206. [WYMAGANE] Zabezpieczyć metadane wszystkich rozpoczętych zadań: identyfikatory, klient/requester, status interrupted, czas zdarzenia, powód, szacowany czas niedostępności, informację o niepewnych skutkach ostatniego działania i wskazówki bezpiecznego wznowienia. Nie kopiować haseł i tokenów do ogłoszenia.
207. [WYMAGANE] Przerwać zadania agenta, anulować oczekujące pytania, zachować oryginalne identyfikatory i idempotency_key. Nie rozpoczynać automatycznie ponownie operacji z potencjalnymi skutkami ubocznymi.
208. [WYMAGANE] Krótki ograniczony czas na odpowiedzi dla oczekujących połączeń (np. 1–2 sekundy), a następnie kontrolowane zamknięcie HTTP, WebView2 i procesu OpenFill. Nie obiecywać dostarczenia wiadomości, jeśli klient już się rozłączył.
209. [WYMAGANE] Po uruchomieniu nowej wersji klient może wykonać openfill_task_status i uzyskać utrwalony status interrupted, wiadomość o przerwie oraz continue_task_id. Podać bezpieczeństwo ponawiania: sprawdzić zewnętrzne skutki (wysłanie, płatność, rejestracja, zakup) przed powtórzeniem.
210. [WYMAGANE] Upgrade ma wymagać sprawdzonej prywatnej kopii backup/rollback i możliwej do wykonania procedury restore. Zgoda na przerwanie zadań nie oznacza zgody na utratę lokalnych profili, dokumentów lub sekretów.
211. [WYMAGANE] Testy: wiele jednocześnie aktywnych zadań (running, needs_input, waiting_for_user), statusy w aktywnych long-poll, zapis/odczyt po restarcie, błędny lub brak ETA, brak niechcianego auto-retry, ochrona sekretów, odmowa nowych startów i zachowanie profilu po upgrade.

Stany implementacji: obecny McpTaskManager.PrepareShutdown w 1.54-dev zachowuje część mechanizmu interrupted, lecz nie ma konfigurowanego ETA ani gwarancji opróżnienia oczekujących odpowiedzi MCP przed wyłączeniem. Całość wymaga implementacji, testów i prawdziwego wdrożenia w SessionId1.
## 21. Addendum: durable maintenance, portable disaster recovery (2026-10-16)

212. [SOURCE IMPROVED / UNTESTED] Before stopping MCP work, persist each interrupted task status durably using a temporary same-directory file, atomic replacement and read-back validation; never silently ignore failure for controlled upgrades. On a failed record write restore its in-memory status and abort the shutdown, keeping the process alive. Regression test must inject an unwritable task directory.
213. [SOURCE / VERIFIED SYNTHETIC ONLY] Implement a portable, authenticated age encrypted offline backup of OpenFill state with a separate encrypted SHA-256 manifest. Do not emit plaintext manifests or secrets into cloud, GitHub, logs, or public chat; exclude logs and downloads by policy.
214. [VERIFIED SYNTHETIC RECOVERY] Maintain a second recovery identity on SATANAMD (ACL restricted to user/SYSTEM), and a public recipient on PBOX1. Confirm an encrypted cross-host fixture from PBOX1 can be decrypted by SATANAMD. Private recovery identity must NEVER be transported to PBOX1 or uploaded to Drive.
215. [NOT DONE] Before considering recovery complete, encrypt the actual offline production state after controlled shutdown, upload only ciphertext to the private Google Drive OpenFill/Encrypted-Backups, verify a full restore in a segregated directory on SATANAMD, and check application/credentials consistency; do not interrupt the existing production process merely to claim completion.
216. [NOT DONE] Guarantee recoverability if SATANAMD is also lost: owner-held additional offline copy of recovery identity or equivalent separately protected escrow. Until verified, cross-host recovery is better than single-machine DPAPI but not a complete independent disaster-recovery scheme.
217. [NOT DONE] Before release run newly added MCP upgrade/persistence fault-injection tests, full regression and build via an allowed route; conduct real GUI smoke, notify reachable clients, verify reconnect/interrupted history, rollback and actual productive version. Historic OpenAI safety denials are not permissions to work around them.

This supplement clarifies historical v1.2 statuses and does not imply these source-only changes have reached installed OpenFill 1.52.
## 22. Aktualizacja 2026-10-16: crash recovery i rzeczywisty zaszyfrowany backup

218. [SOURCE ONLY / TEST PENDING] Trwałe zadania pozostawione przez awarię jako running, needs_input lub waiting_for_user, których nie ma w aktualnym runtime, są przy odczycie oznaczane interrupted/partial. Instrukcja odzyskania wymusza sprawdzenie skutków zewnętrznych i zabrania ślepego ponawiania.
219. [SOURCE ONLY / TEST PENDING] Lookup po idempotency_key musi po restarcie znaleźć ten sam rekord i nie wystartować powtórnej rejestracji lub płatności. Błędy SaveStrict podczas odzyskiwania muszą dotrzeć do wywołującego, nie zostać zamienione na brak rekordu. Dodatkowy test 8 asercji.
220. [SYNTHETIC VERIFIED] Backup age v0.2 wyklucza logs/downloads, wykrywa zmiany plików podczas kopiowania, szyfruje archiwum i manifest. Restore na SATANAMD v0.2 odrzuca pliki spoza manifestu, linki, specjalne wpisy tar i niebezpieczne ścieżki. Test poprawnego archiwum 2/2; test dodatkowego pliku odrzucony przed rozpakowaniem.
221. [REAL DATA, INTEGRITY NOT FULLY VERIFIED] 2026-10-16 około 13:03 uruchomiono offline age backup C:\Users\pboru\OpenFill\data\stable. Powstało zaszyfrowane archiwum o wielkości 1090191400 bajtów oraz zaszyfrowany manifest 2640047 bajtów w prywatnym Documents\OpenFill-Backups\EncryptedState. Pliki skopiowano na Google Drive H:\OpenFill\Encrypted-Backups. Google Drive API potwierdziło manifest, dużego archiwum jeszcze nie potwierdzono po stronie chmury. Polecenie command_status było blokowane: nie utożsamiać istnienia archiwum z pełnym verified restore.
222. [PRODUCTION RESTORED] Przy zerowej liczbie aktywnych zadań włączono maintenance 15 min, zakończono OpenFill 1.52 i po backupie uruchomiono tę samą zainstalowaną wersję z istniejącego zadania Windows OpenFill. PID 17808, Windows SessionId1, MCP openfill_info status ok. Brak wdrożenia 1.54.
223. [OPEN] Zakończyć synchronizację dużego archiwum do Google Drive i potwierdzić po stronie usługi; wykonać pełną deszyfrację i hash-verified restore rzeczywistych danych na SATANAMD, z zachowaniem poufności klucza odzyskiwania. Nie umieszczać sekretów w publicznym GitHub ani jawnym dysku.
224. [OPEN] Dopiero po nowym legalnym buildzie/regresji i skutecznym restore: autoryzowany upgrade w SessionId1, informowanie osiągalnych klientów i realne testy GUI/MCP oraz pomiary wydajności, następnie pozostałe pakiety master planu.

Wymienione ukończone statusy dotyczą wyłącznie precyzyjnie opisanych elementów. Nowy kod nie jest zainstalowany w produkcji.