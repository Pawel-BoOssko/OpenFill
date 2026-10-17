# OPENFILL — PEŁNY HANDOFF DO NOWEGO CZATU

## Metadata
- Document version: 1.0
- Date: 2026-10-14 22:35
**Dokument v1.0 | 14 października 2026, około 22:04 CEST | PBOX1 | Stan aplikacji 1.50**
**Charakter:** kompletny punkt przejęcia prac, opis produktu, architektury, ścieżek, stanu, powodów decyzji, testów, długów technicznych oraz najnowszych poleceń użytkownika.
**Właściciel i decydent:** użytkownik. **Adresat:** kolejny czat/agent, który przejmuje rozwój OpenFill.
**UWAGA:** Dokument nie jest poleceniem wdrożenia wszystkich pomysłów. Wyraźnie oddziela stan potwierdzony, wymagania i propozycje techniczne. Nie zawiera haseł ani tokenów.
**Najważniejsza zasada przejęcia:** Nie zaczynać od ogólnej analizy produktu. Zacząć od zadań użytkownika z sekcji 1; przed zmianami sprawdzić bieżący stan, równoległe zadania i aktywną sesję Windows.

## 0. STAN POTWIERDZONY W MOMENCIE HANDOFFU

- **OpenFill działa: wersja 1.50**, wersjonowana na 2026-10-14 04:16, build 2026-10-14 04:15; serwer MCP odpowiada na openfill_info. Podczas kontroli o około 22:00 zgłaszał start 2026-10-14 21:57:01, aktywnych zadań MCP: 0. Jest to świeżo uruchomiony proces, NIE reprezentatywny dla wielogodzinnego zużycia RAM/CPU.
- Instalacja produkcyjna: **C:\Users\pboru\OpenFill\app\OpenFill.exe**, proces Windows uruchamiany w interaktywnej sesji 1. Nie uruchamiać go z sesji 0!
- **Pełny zestaw testów zapisany w logu: 529 passed, 0 failed**; patrz tools\testlog__v1.49.1__2026-10-14_0411.txt. Log dotyczy testów przeprowadzonych przed wdrożeniem wersji 1.50. Kompilacja aplikacji WinForms przeszła z 0 błędów i jednym wcześniejszym ostrzeżeniem WindowsBase.
- UI z przyciskiem „Import Firefox” jest wdrożone; użytkownik **sam poinformował teraz, że zaimportował jedno hasło do SEOHost**. To nowsze źródło niż dokument wdrożeniowy 04:25, który mówił, że test z realnym hasłem dopiero czeka.
- Uruchomiono wcześniej MCP obsługujące bezpośrednie zadania z Networking Radar; istnieje mechanizm idempotency_key. Stan Radar zweryfikowany rano 14.10: wersja 0.22.7, 191 testów pass, port 8770, endpoint /api/registration/ready zwracał ready:true/openfill_available:true/profile_ready:true. Przed użyciem zweryfikować ponownie.
- **Automatyczne zadania i przypomnienia ChatGPT wyłączono na polecenie użytkownika.** Nie tworzyć ani nie przywracać takich zadań bez odrębnego polecenia. Harmonogram Windows uruchamiający serwery/backup/watchdog to osobna sprawa i nie jest równoznaczny z przypomnieniami.
- Repozytorium źródłowe OpenFill znajduje się w katalogu wersjonowanym nazwą, ale **nie jest repozytorium Git**: próba git -C w tym katalogu zwraca „not a git repository”. Nie obiecywać commitów/pushy dla tego katalogu.
- Dokument README i INDEX w źródłach są starsze niż aplikacja: README v0.17 (12.10), INDEX v1.46 (13.10), a wdrożenie to 1.50. Dokumentacja handoff i deployment jest nowsza. Przy sprzecznościach preferować żywy MCP, rzeczywisty kod i ostatnie logi.

## 1. OD TEGO ZACZĄĆ — NAJNOWSZE POLECENIA UŻYTKOWNIKA

### 1.1. ZADANIE PIERWSZE: rzeczywisty test logowania SEOHost
Użytkownik już kliknął „Import Firefox”, wybrał jedno konto SEOHost i zatwierdził import do magazynu Windows. Następny czat ma **sprawdzić prawdziwe logowanie do właściwej strony SEOHost w OpenFill**, posługując się tym zaimportowanym hasłem — a nie wymyślając ani resetując hasło. Preferowana droga: przekazać Lunie kompletne zadanie przez openfill_start_task, w którym ma sprawdzić istniejącą sesję, wybrać konto, a w razie potrzeby użyć wewnętrznego narzędzia **use_imported_password** (hasło nie jest ujawniane modelowi). Następnie monitorować openfill_task_status i weryfikować sukces **na rzeczywiście zalogowanym koncie**, a nie tylko po kliknięciu przycisku logowania. Jeśli potrzebne dane brakują, użyć istniejącego procesu needs_input / waiting_for_user, nie tworzyć duplikatu zadania. Nie resetować ani nie zmieniać hasła. Nie mylić SEOHost z Hostido (osobny hosting/poczta); ustalić konkretny adres i login z dostępnego kontekstu i przeglądarki; nie zgadywać danych ani nie wypisywać hasła w logu.

**Dosłowne polecenie użytkownika z rozmowy:**
> „Słuchaj, przetestowałem, wciągnąłem jedno hasło do SEO Host. Możesz spróbować, czy dasz radę się zalogować OpenAI? To jest pierwsza rzecz, pierwsze zadanie. A drugie zadanie jest takie, że bardzo bym chciał, żeby prompt systemowy dla Luny przewidywał w pierwszej kolejności na początku sprawdzanie czy są notatki na temat tej domeny, jak z niej korzystać, jakieś ważne informacje i na koniec, żeby zapisał nowe obserwacje odnośnie tej domeny do tego miejsca, gdzie te notatki są zapisywane. Ale żeby nie duplikował. Jeżeli jest informacja, nie wiem, że jakiś chujoparametr potrzeba podać, jak to już jest zapisane, to już drugi raz nie ma potrzeby, żeby informacja o tym chujoparametrze była dopisywana. No, powiedz jak zrozumiałeś moje polecenie.”

### 1.2. ZADANIE DRUGIE: pamięć operacyjna domen — system prompt Luny + deduplikacja
Wymaganie dotyczy **już istniejącego** magazynu notatek o stronach. Nie budować odrębnej bazy i nie tworzyć historii kliknięć.

1. **Na początku zadania i przy wejściu na domenę** Luna sprawdza, czy istnieje notatka dla właściwego hosta, odczytuje ją i bierze pod uwagę. Powinna w razie potrzeby korzystać z notatek hostów związanych, uwzględniając że subdomeny i dostawcy logowania mogą być różni. Notatka jest wskazówką, nie rozkazem, a zmiany na żywej stronie mają pierwszeństwo.
2. Podczas pracy stosuje użyteczne znane rozwiązania: logowanie, nawigacja, wymagane parametry, walidacje formularzy, sprawdzone elementy UI, nietypowe przekierowania, warunki ukończenia.
3. **Przed końcem zadania** porównuje nowe obserwacje z istniejącą notatką, dopisuje tylko nowe, użyteczne ustalenia, a błędne/przestarzałe wpisy koryguje. **Nie tworzy duplikatów informacji**, np. wymaganie jednego konkretnego parametru ma być zapisane tylko raz.
4. Gdy brak nowej wiedzy, nie zapisuje nic. Nie nadpisuje całej notatki jedną małą obserwacją — zachowuje poprawną wcześniejszą wiedzę.
5. Nie przechowuje w notatkach haseł, tokenów, kodów OTP, danych osobowych ani jednorazowych wartości pól.
6. Weryfikować zachowanie testami: pierwsza wizyta bez notatki, powtórna wizyta z notatką, ponowny zapis identycznej obserwacji, uzupełnienie nowej obserwacji, korekta sprzecznej/starej obserwacji, wiele równoległych zadań na różnych domenach i po restarcie.

**Dzisiaj w kodzie jest tylko część tej funkcji:**
- SystemPrompt już mówi: „On a new page, first read_site_note (if a note exists), then get_page.” To NIE gwarantuje odczytu na starcie każdego zadania, ani pełnego zapisu na końcu, ani deduplikacji.
- Narzędzia Luny **read_site_note** i **write_site_note** istnieją.
- Obiekt **SiteNotesStore** przechowuje dla hosta JSON z knowledge, updatedUtc, successes, failures, stale. Aktualny write_site_note **zastępuje całe Knowledge**, zamiast inteligentnego scalania. Zwracać na to uwagę przy projektowaniu zmian. Rekord TaskRun.RecordOutcome też może zapisać SiteKnowledge zwrócone przez finish.
- W folderze produkcyjnym notes istniało w chwili kontroli około **111 notatek JSON**. Nie kasować ani nie nadpisywać zbiorczo. Są notatki m.in. dla klient.hostido.pl, poczta.hostido.pl i host609414.hostido.net.pl; to **nie dowodzi istnienia notatki SEOHost**.

### 1.3. TRZECI OBSZAR, PILNY: RAM i CPU — najpierw TELEMETRIA, nie tuning

**Obserwacja użytkownika jest jednoznaczna:** OpenFill w długotrwałej pracy **stale** potrafi zajmować około **5 GB pamięci i 20% CPU**, a nie tylko chwilowo osiąga szczyt. Nie próbować tego kwestionować krótkim pomiarem zaraz po restarcie.

**Dosłowne nowe wiadomości użytkownika:**
> „Dodaj też do tego handoffu informacje do zajęcia się, że.Potrafi zająć 5 gigabajtów pamięci i 20% czasu procesora, więc musimy się tym zająć. Chciałbym w pierwszej kolejności zająć się jakimś logowaniem, monitorowaniem tych zasobów, a w dalszym dopiero kroku będziemy się zastanawiać.”

> „To jest stałe obciążenie. Jakbyś sprawdził ile czasu pracuje ta wersja OpenFila, to byś zobaczył, że jest uruchomiona cztery minuty temu.”

**Ważne pomiary i ograniczenia:** Około 22:00 stwierdzono OpenFill.exe 111,6 MB working set i 26 procesów msedgewebview2 łącznie 988,1 MB working set. Ale proces produkcyjny uruchomiono **21:57:01**, więc pomiar dotyczył zaledwie ok. 3–5 minut pracy; NIE obala obserwacji użytkownika o długotrwałym obciążeniu. Wcześniejszy asystent niesłusznie sugerował, że to stan okresowy; poprawne wymaganie to diagnoza **utrzymującego się obciążenia po upływie czasu**.

**Pierwszy krok implementacyjny w tym obszarze:**
- Wprowadzić trwałe i niedrogie logowanie CPU/RAM **całego drzewa procesów** OpenFill.exe → msedgewebview2.exe oraz innych procesów rzeczywiście powiązanych z aplikacją; NIE sumować wszystkich cudzych WebView2 w Windows.
- Przechowywać próbki w osobnym logu telemetrycznym z timestampem, PID i czasem startu procesu, uptime całej aplikacji, liczbą procesów dzieci, aktywnych kart, zadań i sesji, RSS/Working Set, Private Bytes/Commit, liczbą uchwytów, opcjonalnie pamięcią .NET/GC i rozbiciem na procesy WebView2. Rzetelnie obliczać CPU % na podstawie różnicy Process.TotalProcessorTime / elapsed time i liczby logicznych procesorów; wprost opisać, czy procent jest sumą rdzeni czy udziałem w całkowitej mocy komputera.
- Obserwować zarówno bezczynność przez godziny, jak i wykonywanie rzeczywistych zadań; szukać trendu wzrostowego i kosztu każdej nowej karty / zakładki / zadania / sesji.
- Ograniczyć narzut telemetryczny: rozsądny interwał (np. 15–60 sekund), rotacja plików, maksymalny czas i rozmiar historii, brak sekretów, możliwość analizy i wykresów później.
- **Nie przeprowadzać teraz losowych optymalizacji** — nie zmieniać w ciemno GC, liczby kart, polityki procesów WebView2 ani limitów. Najpierw zebrać dane; dopiero na ich podstawie decyzja o poprawkach.

### 1.4. Polecenie o handoffie i awarii interfejsu
Użytkownik nie widzi normalnie odpowiedzi czatu i przeniósł pracę do nowego czatu. **Ten plik jest podstawowym kanałem komunikacji i źródłem kontekstu**.

**Dosłowne polecenie:**
> „Przygotuj proszę i zapisz do pliku pełen handoff projektu Open Feel, pełen, najpełniejszy, łącznie z treścią tej wiadomości, którą teraz ci nadałem i że od niej chcemy zacząć. Proszę, żebyś podał wszystkie ścieżki, wszystkie podstawy, powody, dla których budujemy ten projekt i tak dalej i tak dalej i zapisz to do pliku na dysku i podaj mi ścieżkę. Problem mam w tej chwili taki, że nie widzę w ogóle twoich odpowiedzi. Nie wczytują się w ogóle, ale to w ogóle. W związku z tym musisz się ze mną skomunikować właśnie przez ten plik, czyli w tym pliku pełen handoff projektu ze wszystkimi informacjami. I przeniosłem cały projekt do nowego czatu.”

**Kolejność rozpoczęcia w nowym czacie:** najpierw szybka kontrola stanu i test SEOHost; potem poprawka odczytu/scalania notatek i testy. W obszarze wydajności pierwszą rzeczą jest telemetryczne logowanie, nie optymalizacja. Użytkownik może zdecydować o równoległości tych niezależnych prac.

## 2. DLACZEGO OPENFILL POWSTAŁ — MISJA I FILOZOFIA

OpenFill to **własna windowsowa aplikacja do wykonywania realnych zadań w internecie za pomocą małego, ekonomicznego modelu AI sterującego przeglądarką**, wystawiona do innych modeli i programów przez MCP. Model nadrzędny (ChatGPT, Claude, aplikacja Networking Radar) rozpoznaje potrzebę i podaje kompletny cel. Osobny agent **Luna** pracuje wewnątrz OpenFill, korzysta z sesji przeglądarki i narzędzi oraz zwraca zweryfikowany rezultat.

Powody architektoniczne i biznesowe:
1. Drogi duży model powinien planować i delegować cel, a nie wykonywać setki banalnych kliknięć, odczytów DOM, zgód cookies i formularzy.
2. Osobny agent w aplikacji lokalnej może pracować tanio, autonomicznie, blisko przeglądarki i już zalogowanych kont użytkownika.
3. Agent ma widzieć **strukturę strony**, a nie domyślnie robić screenshoty; korzysta z DOM, semantyki pól, akcji grupowych, ruchu sieciowego i CDP; screenshot jest fallbackiem.
4. Zadanie jest **wynikowe** („zarejestruj mnie na wydarzenie, potwierdź rejestrację”), nie seria mikro-poleceń do zewnętrznego modelu. Oczekiwany wynik ma potwierdzenie, ewentualnie wyraźne failed/partial.
5. Użytkownik ma zaawansowane doświadczenie informatyczne i sam określa akceptowalne ryzyko. Nie należy blokować poprawnych workflow haseł nadmiernym paternalizmem, wielokrotnymi potwierdzeniami i bezsensownymi resetami.
6. OpenFill ma mieć stałe sesje, ponawialne zadania, logi, limity kosztów, ochronę przed zapętlaniem, wiele zakładek, dobrą ergonomię i integrację z innymi lokalnymi aplikacjami.
7. Po rozbudowie OpenFill ma stanowić **wspólną warstwę obsługi witryn** dla niezależnych aplikacji, np. Radar wyszukuje wydarzenia i „Zapisz mnie” uruchamia rejestrację na stronie organizatora, bez osobnego systemu AI do przeglądarki.

Użytkownik preferuje autonomiczne wykonywanie prac bez drobnych pytań, szybkie decyzje techniczne, minimalne zbędne komentarze; ważna jest rzetelna weryfikacja sukcesu. Pliki tworzone dla projektu mają w **nazwie wersję, datę i godzinę**.

## 3. ARCHITEKTURA WERSJI 1.50

**Warstwy:**
- **OpenFill.App** — WinForms i Microsoft Edge WebView2, główne okno zawierające panel, zakładki przeglądarki, formularz zadania i karty pytań, w tym „Import Firefox”. CDP przez WebView2, kontrola interaktywnej sesji.
- **OpenFill.Cli** — alternatywne uruchamianie w konsoli, Chromium/Edge przez CDP, panel przez przeglądarkę; ułatwia testy i automatyzację.
- **OpenFill.Core** — niezależny od UI rdzeń modelu, browser controller, CDP, narzędzia, logi, task manager, koszty, polityki domen, MCP, pamięć witryn, sesje.
- **Model** — OpenAI Responses API. W konfiguracji produkcyjnej podczas handoffu model **gpt-6-luna** z reasoningEffort **medium**; używany klucz OpenAI pozostaje na maszynie, chroniony przez Windows DPAPI. Nie wpisywać klucza do dokumentów.
- **AgentRunner** — cykl wejście strony → instrukcje systemowe + narzędzia → wywołanie modelu → wykonanie akcji → wynik/następna tura. Instrukcja jest statycznym SystemPrompt w C#, nie jest w tej chwili osobnym łatwo edytowalnym plikiem.
- **ToolRegistry** — definicje operacji Luny; wykonanie CDP/DOM, czytanie notatek, wypełnianie hasła, przesyłanie plików, pytania i finish.
- **MCP Server/Task Manager** — Streamable HTTP JSON-RPC 2.0, uruchamianie/wznawianie/status/odpowiedź/anulowanie zadania, aktywna sesja Chrome WebView2 i trwała historia JSON.
- **Witryny** — blokada równoległych zadań na jednej domenie, zachowanie tabów, cache sesji. Mo��liwe kilka zadań równolegle na różnych serwisach.
- **Menedżer poświadczeń Windows** — magazyn haseł importowanych i generowanych; importowana wartość pozostaje poza promptem.
- **SiteNotesStore** — lokalny JSON per host z operacyjną wiedzą o stronie.
- **CostGuard/RunMeter** — mierzenie tokenów/ceny, ostrzeżenia, blokady domen, próg pytań o budżet.
- **Logging/EventLog** — globalne i per-zadanie NDJSON, statystyki wywołań, traffic network, redakcja znanych sekretów.

**Interfejs MCP — aktualne narzędzia serwera:**
openfill_start_task, openfill_task_status, openfill_reply, openfill_cancel_task, openfill_info, openfill_screenshot, openfill_file_transfer, openfill_maintenance.

**Kontrakt ważny dla klientów:**
- openfill_start_task wymaga task + requester + chat. requester/chat to trwałe, spójne identyfikatory nadawcy i wątku; tworzą dwukolorową identyfikację zadań w UI.
- Wynik zawiera task_id. status: running, needs_input, waiting_for_user, done, failed, blocked, cancelled, stopped_by_user; dodatkowo odmowy busy, domain_busy, maintenance itd.
- **status=done nie wystarczy**: analizować outcome (success / partial / failed) i summary z obiektywnym dowodem. W przypadku rejestracji wymagane jest potwierdzenie, np. numer, bilet lub potwierdzający e-mail.
- openfill_reply odpowiada wyłącznie na needs_input; jeśli waiting_for_user, czynność jest wykonywana w lokalnym oknie.
- Parametr **idempotency_key** (od 1.49) jest stabilnym identyfikatorem żądania klienta; identyczne wywołanie (ten sam requester/chat, identyczny task, identyczny key) zwraca wcześniejsze task_id, także po restarcie. Inna treść pod tym samym kluczem → idempotency_conflict. Dzięki temu Radar nie może przez automatyczny retry przypadkowo dwukrotnie zapisać użytkownika na wydarzenie. Możliwość explicit_retry w Radar rozróżnia nowe zlecenie.
- Obsługa plików **openfill_file_transfer**: HTTP PUT/GET z osobno wydawanymi krótkotrwałymi tokenami plikowymi, bez kodowania pliku w MCP JSON. W 1.47 test rzeczywisty 128 KiB, PUT 201, GET 200, zgodność SHA256 i blokada ponownego PUT.
- Screenshot: wynik MCP może zawierać prywatny URL pliku oraz obraz. Nie ujawniać centralnego sekretu MCP. Przy starym połączeniu ChatGPT lista narzędzi może być cache'owana — porównać ze świeżym tools/list serwera.
- **Nie należy przesyłać głównego sekretu URL MCP do frontendowego JS ani umieszczać go w repozytorium.**

## 4. HASEŁKA, FIREFOX, SEOHOST, TRELLO

**Aktualny stan:** użytkownik zaimportował hasło Firefox SEOHost ręcznie. Import działa poprzez przycisk Import Firefox na pasku nawigacji OpenFill, wyszukanie strony/konta w FirefoxPasswordPicker, wybór jednego wpisu i potwierdzenie; FirefoxLogins odczytuje metadane/profil, a jeśli Firefox Primary Password jest ustawione, użytkownik obsługuje okno lokalne. Wybrany wpis trafia do **Windows Credential Manager**, a agent ma narzędzie **use_imported_password**, które dobiera poświadczenie do właściwej strony i wypełnia pole hasła bez ujawnienia wartości modelowi.

**Zasada dla nowego konta:** gdy Luna generuje hasło tymczasowe, wywołuje **save_temporary_password(service,login,password)** i zapisuje je w Windows Credential Manager. Wersja 1.48 dodała zwracane przez MCP pole **temporary_credentials** (service, login, exact password, vault_saved, temporary, change_immediately), zachowujące informację nawet jeśli naturalny opis modelu pominie hasło. Dane strukturalne mogą być zapisane **jawnym tekstem w lokalnej historii zadania JSON**; świadoma cecha bieżącej implementacji, odrębna od zaszyfrowanego vaulta. Nie ujawniać tych danych osobom postronnym.

**Decyzja użytkownika:** główny magazyn haseł to Windows Credential Manager. **Trello może być dodatkowym magazynem nawet jawnych haseł tymczasowych**, na odpowiedzialność i według oceny użytkownika; nie nadpisywać jego decyzji ogólnym zakazem. Jednocześnie nie kopiować rzeczywistych haseł do niniejszego handoffu. W Trello istnieje karta backlogu „OpenFill: przypomnienie o zapisaniu hasła tymczasowego” na liście „Później” w „Moja tablica Trello”; zadanie dotyczy wyraźnego komunikatu o zapisaniu nowego hasła do ustalonego miejsca i późniejszej aktualizacji. **Nie wdrożono jeszcze wymogu automatycznego komunikatu ani integracji zapisującej hasło do Trello.** Nie mylić wymogu z działającym zapisem do Windows.

Użytkownik zwrócił uwagę, że nadmierne ograniczenia narzędzi zarządzania hasłami paradoksalnie wymuszają ręczne zapisywanie ich w niebezpiecznych miejscach; oczekuje efektywnej automatyzacji haseł, nie porad o podstawowym bezpieczeństwie. Bezpieczne, ale praktyczne rozwiązanie: nie ujawniać zaimportowanego hasła w modelu, pozwolić z niego korzystać w poświadczonym kontekście.

## 5. PEŁNA MAPA ŚCIEŻEK NA PBOX1 (RZECZYWISTE)

**Źródła:**
- Katalog główny: **C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill**
- Solution: …\OpenFill\OpenFill__v0.1__2026-10-04_1911.sln
- Wersja produktu: …\OpenFill\Directory.Build.props
- README: …\OpenFill\README__v0.17__2026-10-12_2126.md
- Historyczny indeks pakietu: …\OpenFill\INDEX__v1.46__2026-10-13_1106.md
- Dokumenty: …\OpenFill\docs\
- Testy: …\OpenFill\tests\OpenFill.Tests\
- Skrypty i logi buildów: …\OpenFill\tools\
- Kluczowe projekty: …\OpenFill\src\OpenFill.App\, …\OpenFill\src\OpenFill.Cli\, …\OpenFill\src\OpenFill.Core\

**Kod, exact relative paths od katalogu źródeł:**
- src\OpenFill.Core\Agent\AgentRunner__v0.23__2026-10-13_1035.cs — **SystemPrompt** Luny, pętla narzędzi. Główne miejsce zmiany zadania 1.2.
- src\OpenFill.Core\Agent\ToolRegistry__v0.18__2026-10-13_1035.cs — read_site_note, write_site_note, use_imported_password, save_temporary_password, finish i dane site_knowledge.
- src\OpenFill.Core\Agent\SiteNotesStore__v0.4__2026-10-07_1732.cs — odczyt/zapis/notatki hostów; obecny zapis podmienia Knowledge. Ważne dla deduplikacji i scalania.
- src\OpenFill.Core\Agent\ImportedCredentialVault__v0.1__2026-10-13_2232.cs — wykorzystanie importowanych poświadczeń.
- src\OpenFill.Core\Agent\TemporaryCredentialVault__v0.1__2026-10-13_2000.cs — generowane hasła tymczasowe.
- src\OpenFill.App\FirefoxLogins__v0.1__2026-10-13_2229.cs — wykrywanie profilu Firefox / loginów.
- src\OpenFill.App\FirefoxPasswordPicker__v0.1__2026-10-13_2236.cs — wybór hasła i zatwierdzanie importu.
- src\OpenFill.App\MainForm__v0.23__2026-10-14_0414.cs — UI i przycisk „Import Firefox”.
- src\OpenFill.App\WebView2CdpConnection__v0.5__2026-10-13_1035.cs — interfejs CDP WebView2.
- src\OpenFill.Core\Browser\BrowserController__v0.8__2026-10-13_1035.cs — obsługa przeglądarki/akcji.
- src\OpenFill.Core\Hosting\TabManager__v0.6__2026-10-12_2200.cs — zarządzanie tabami; potencjalnie istotne dla RAM.
- src\OpenFill.Core\Hosting\TaskRun__v0.1__2026-10-07_1732.cs — zakończenie pracy/RecordOutcome, zapis site_knowledge.
- src\OpenFill.Core\Logging\EventLog__v0.5__2026-10-07_1732.cs — logowanie; punkt integracji telemetrycznej.
- src\OpenFill.Core\Agent\CostGuard__v0.3__2026-10-07_1732.cs — limity kosztów.
- src\OpenFill.Core\Mcp\McpServer__v0.21__2026-10-13_1106.cs — schematy narzędzi, HTTP MCP, idempotency_key.
- src\OpenFill.Core\Mcp\McpTaskManager__v0.16__2026-10-12_1917.cs — trwałe task_id, statusy, historia, idempotentność.
- src\OpenFill.Core\Mcp\McpService__v0.3__2026-10-05_1838.cs — startup usługi i adresów.
- src\OpenFill.Core\Mcp\FileTransfer__v0.1__2026-10-13_2000.cs — transfer GET/PUT.
- src\OpenFill.Core\Mcp\CloudflareTunnel__v0.1__2026-10-05_1348.cs — tunel.
- src\OpenFill.Core\Model\OpenAIClient__v0.2__2026-10-05_1200.cs — klient OpenAI Responses.
- src\OpenFill.Core\Config\AppConfig__v0.13__2026-10-12_1133.cs — konfiguracja i domyślne limity.

**Instalacja i dane produkcyjne:**
- Katalog aplikacji: **C:\Users\pboru\OpenFill\app\**
- EXE: **C:\Users\pboru\OpenFill\app\OpenFill.exe**
- Bazowy katalog danych: **C:\Users\pboru\OpenFill\data\stable\**
- Konfiguracja: …\data\stable\config.json (NIE kopiować w całości do dokumentów — mogą być ustawienia wrażliwe)
- Profil przeglądarki/sesje/cookies: …\data\stable\profile\
- **Notatki domenowe:** …\data\stable\notes\ — pliki **[host].json**, 111 plików przy kontroli. Jest to faktyczne miejsce zadania 1.2.
- Stan/identyfikatory i JSON tasków: …\data\stable\tasks\
- Historia: …\data\stable\history\
- Logi: …\data\stable\logs\
- Per task: …\data\stable\logs\runs\
- Usage modelu: …\data\stable\logs\usage_202610.ndjson
- Runtime MCP: …\data\stable\runtime\ (m.in. mcp-url.txt i mcp-secret.txt; **wrażliwe, nie wypisywać do czatu ani dokumentu**)
- Pobrane pliki: …\data\stable\downloads\
- Screenshoty: …\data\stable\screenshots\
- Wspólny katalog wymiany plików: **C:\Users\pboru\OpenFill\shared\**
- Sekrety/poświadczenia OpenFill: **C:\Users\pboru\OpenFill\secrets\**, plus Windows Credential Manager / DPAPI — nie wyciągać i nie kopiować w handoffie.
- Rollback: **C:\Users\pboru\OpenFill\app__rollback__v1.49__2026-10-14_042208**
- Inne kopie rollback i backups znajdują się pod C:\Users\pboru\OpenFill\. Przed sprzątaniem sprawdzić co jest aktualne.
- **Uwaga audytowa:** dokument deployment 1.50 wymienia ścieżkę wydania C:\Users\pboru\OpenFill\releases\app__v1.50__2026-10-14_0416, ale podczas obecnej kontroli katalogu NIE udało się potwierdzić jej istnienia. Produkcyjny EXE i działające MCP są potwierdzone. Nie zakładać, że historyczna paczka wydaniowa wciąż jest na miejscu.

**Wersjonowane dokumenty historyczne w repo:**
- docs\handoff__v1.47__2026-10-13_2024.md
- docs\handoff__v1.48__2026-10-13_2140.md
- docs\handoff__v1.48.1__2026-10-13_2154.md
- docs\integration_networking_radar__v0.2__2026-10-13_2239.md
- docs\integration_networking_radar__v0.3__2026-10-14_0024.md
- docs\handoff__v1.50__2026-10-14_0417.md
- docs\deployment__v1.50__2026-10-14_0425.md

**Skrypty:**
- tools\deploy_safely__v0.1__2026-10-13_2153.ps1 — bezpieczne wdrożenie w **sesji interaktywnej** i potwierdzenie MCP.
- tools\deploy_launch__v0.1__2026-10-14_0422.ps1 — skrypt uruchamiania przy wdrożeniu.
- tools\install__v0.12__2026-10-12_2126.ps1 — instalacja.
- tools\publish__v0.3__2026-10-05_2211.ps1 — publikacja.
- tools\release__v0.6__2026-10-07_1432.ps1 — wersjonowanie plików/INDEX/audyt.
- tools\run-dev__v0.2__2026-10-05_1515.ps1 — DEV.
- tools\watchdog-openfill__v1.0__2026-10-07_1432.ps1 oraz watchdog-install/uninstall — watchdog.
- tests\OpenFill.Tests\McpTests__v0.11__2026-10-12_2200.cs — m.in. sprawdzanie kluczy i cyklu MCP.
- tests\OpenFill.Tests\Program__v0.19__2026-10-13_0600.cs — testy Firefox/NSS i pozostałe.
- tools\testlog__v1.49.1__2026-10-14_0411.txt — referencyjny pełny log 529/529.

**Networking Radar (osobny projekt, odbiorca OpenFill):**
- Repo: **C:\Users\pboru\Documents\Projects\Networking-Radar**
- Serwer użytkowy: **http://127.0.0.1:8770/**
- Front-end „Zapisz mnie” i backend: app\main.py, app\static\app.js, app\templates\index.html (ścieżki odniesione do repo Radar).
- Klient MCP: **app\services\openfill_mcp.py**; logika rejestracji: **app\services\event_registration.py**; profil prywatny **config\local\openfill_registration.json**.
- Endpointy: GET /api/registration/ready; POST /api/registration/start; GET /api/registration/status; POST /api/registration/reply.
- Committy powiązane z integracją z porannej kontroli: e7ebac4 (przekazywanie stabilnego idempotency_key), 738d869 (dokumentacja), b0d99b4 (log testów); Git branch master. Testy 191 passed/1 warning. W Radar były niescommitowane równoległe modyfikacje, więc przed zmianą kodu sprawdzić status Git, nie nadpisać cudzych prac.

## 6. WERSJE, CO ZROBIONO I CO ZOSTAŁO PRZETESTOWANE

- **1.46 i wcześniej:** podstawa aplikacji, WinForms + WebView2, CDP, ekran/panel, Luna, narzędzia, koszty, stronowe notatki, MCP, historia, autostart. INDEX v1.46 jest archiwum układu plików, nie listą plików obecnej 1.50.
- **1.47 (13.10):** prywatne URL screenshotów i transfer raw file GET/PUT; lepsze przekazywanie danych uprawnionym użytkownikom; Windows Credential Manager; save_temporary_password; test transferu 128 KiB i hasha. W tej wersji zaszła istotna poprawa obsługi haseł i transferu.
- **1.48 (13.10, 21:37):** pole temporary_credentials przekazywane przez RunConclusion → McpTaskManager → MCP, zapis w historii zadania. Testy 511 zaliczonych. Naprawiono procedurę wdrożeniową po incydencie uruchomienia procesu w sesji Windows 0 (w takiej sesji MCP nie działał poprawnie).
- **1.49 (13/14.10):** interfejs idempotency_key do zewnętrznych aplikacji; wybieranie i import pojedynczego loginu z Firefox, magazyn Windows i narzędzie use_imported_password Luny; testy OpenFill rozbudowane.
- **1.50 (14.10, 04:16):** UI „Import Firefox” podłączone do FirefoxPasswordPicker; wdrożone przez bezpieczny skrypt w interaktywnej sesji; **529 passed, 0 failed** oraz potwierdzony żywy serwer MCP 1.50.
- **14.10 wieczorem:** użytkownik zaimportował prawdziwe hasło do SEOHost. Jeszcze nie ma w tym handoffie potwierdzonego logowania przez Lunę. Nie wpisywać „test zaliczony” dopóki realne logowanie nie zostanie sprawdzone.

Wcześniejsza integracja Radar–OpenFill jest gotowa technicznie; **pełny scenariusz rzeczywistej rejestracji na wydarzenie z potwierdzeniem nie został dotąd odnotowany jako pomyślnie przetestowany**. To osobny test akceptacyjny, nie powód do przebudowy OpenFill.

## 7. KONFIGURACJA PRODUKCYJNA (ODSŁONIĘTE WYŁĄCZNIE NIEWRAŻLIWE PARAMETRY)

Z rzeczywistego config.json podczas handoffu:
- model: gpt-6-luna
- reasoningEffort: medium
- mcpEnabled: true, mcpPort: 48731, mcpTunnel: true
- maxParallel: 3, maxTabs: 5
- maxSteps: 90, maxStepExtensions: 5
- costInfoUsd: 1; costWarnUsd: 2; domainLimitUsd: 5; domainLifetimeLimitUsd: 15
- toolOutputLimitChars: 50000
- historyCompactThresholdChars: 240000
- taskTimeoutMinutes: 30.

Te dane **różnią się od starszych wartości README** (np. $0.10/$0.15, 60 kroków, low reasoning). Kierować się realną konfiguracją i kodem w czasie pracy, nie domyślnymi wartościami opisanymi 12.10.

Adres lokalnego MCP jest oparty na **http://127.0.0.1:48731/mcp/[sekret]** (nie wpisywać sekretu w żaden dokument). Klient zewnętrzny poza PBOX1 musi używać już skonfigurowanego, autoryzowanego endpointu HTTPS/tunelu. Łącze i sekret tylko w backendzie klienta, nie w frontendzie i nie w Git.

## 8. PROCEDURY PRACY, WDROŻENIA, LOGOWANIA I DIAGNOSTYKI

1. **Przed zadaniem lub aktualizacją**: pobrać openfill_info i sprawdzić wersję, aktywne zadania, dostępność domeny, historię błędów; nie przerywać bieżących zadań innych czatów.
2. **Do uruchomienia w SEOHost**: jedno całościowe zlecenie przez MCP, ze stałym requester i chat; bez niepotrzebnych restartów, resetów haseł lub zapętlonego ponawiania. Odczytać state needs_input/waiting_for_user i właściwie obsłużyć; zweryfikować konto.
3. **Przed zmianą kodu**: wykonać backup dotykanych plików (wersjonowany), sprawdzić istniejące testy, nie przestawiać całych folderów ani nie kasować istniejących notatek/poświadczeń.
4. **Zmiana system prompt / notes**: zadbać o minimalny koszt dodatkowych wywołań; nie mnożyć tokenów przy każdym przejściu na URL. Testy jednostkowe i integracyjne; duplikaty deduplikować semantycznie lub poprzez ustrukturyzowane wpisy; zachować spójność przy równoległych zadaniach i atomowy zapis JSON.
5. **Monitoring wydajności**: zbierać próbki przez dłuższy czas i podczas idle/aktywności, zachować osobny log, potem analiza. Nie restartować aplikacji tylko po to, by powtórzyć krótki pomiar „zimnego” startu.
6. **Testy**: dotnet run --project tests\OpenFill.Tests\OpenFill.Tests__v0.1__2026-10-04_1911.csproj (w katalogu źródeł); uruchamiać faktyczny projekt w repo po sprawdzeniu ścieżki. Rejestrować liczbę testów i błędów. Testować również build WinForms i aktywne MCP po deploy.
7. **Wdrożenie**: użyć openfill_maintenance dla zatrzymania przyjmowania nowych zadań, **nie anulować** istniejących, poczekać aż running_tasks=0. Publish do odrębnego release, kopia rollback, uruchomienie **tylko w sesji Windows użytkownika (SessionId > 0)**, zdrowie MCP openfill_info po starcie, zgodność wersji i UI. Skrypt deploy_safely już ma kontrolę sesji; nie uruchamiać nowego procesu przez Lupus MultiHost w sesji 0. Remote Desktop Commander wykonywał komendy w sesji 1.
8. **Udokumentowanie efektu**: nowy plik z wersją + datą + godziną, testlog, ścieżki, stan wdrożenia, znane ograniczenia. Nie twierdzić, że coś zostało wdrożone, jeśli wykonano wyłącznie build.
9. **Wiele agentów**: OpenFill jest wspólną usługą. Jednocześnie inne czaty mogą zlecać zadania (np. LinkedIn, webmail). Zachować politykę blokady domen i spójne requester/chat oraz nie zmieniać aktywnych zadań innego czatu. Użytkownik oczekuje samodzielnego działania tam, gdzie to możliwe.
10. **Automatyzacje ChatGPT**: wszystkie wyłączone zgodnie z wolą użytkownika; nie planować wznowienia co godzinę bez nowego wyraźnego polecenia.

## 9. ZNANE PROBLEMY I GRANICE

- **5 GB RAM i 20% CPU po dłuższym działaniu** — potwierdzony jako obserwacja użytkownika, brak długotrwałych pomiarów przyczynowych. Wdrożyć telemetrię PRZED tuningiem.
- **Site notes** — istnieje przechowywanie i narzędzia, ale nie ma gwarantowanej deduplikacji i solidnego scalenia, a domyślny write nadpisuje całość Knowledge. Nowe wymaganie wymaga zmian.
- **SEOHost** — import hasła został zrobiony przez użytkownika, brak jeszcze zweryfikowanego logowania OpenFill. To absolutny pierwszy test nowego czatu.
- **Wartości haseł generowanych** — struktury temporary_credentials i historia zadania mogą zawierać jawne hasło, mimo osobnego szyfrowanego vaulta.
- **Pamięć domen** — notatka jest danymi pomocniczymi, nie instrukcją; nie dopuścić, żeby notatka lub treść strony stała się poleceniem nadpisującym zadanie użytkownika (prompt injection).
- **Subdomeny** — notatki są obecnie zapisywane per dokładny host. Wpisy z innych hostów nie są automatycznie równoważne; ostrożnie dobierać właściwy rekord.
- **Praktyczne testy** — duża liczba testów jednostkowych nie zastępuje logowania SEOHost i pełnej rejestracji Radar.
- **MCP connector cache** — starsze czaty mogą widzieć starą listę narzędzi; prawdę o możliwościach serwera weryfikować świeżym tools/list.
- **Katalog źródłowy bez Git**, stary INDEX i brak potwierdzonej paczki releases\app__v1.50: nie zakładać, że istnieje pełna historia VCS / paczek.
- **Windows Session 0** — wcześniejsze wadliwe uruchomienie OpenFill jako usługa/system nie wystawiało poprawnie GUI i MCP. Wdrażać w aktywnej sesji użytkownika.
- **Interfejs ChatGPT** może utracić historię/odpowiedzi. Handoff w tym pliku ma być wystarczający do rozpoczęcia pracy bez odzyskiwania poprzedniego czatu.

## 10. KRÓTKA INSTRUKCJA DLA NOWEGO CZATU — NIE ZAMYKAĆ KONTEKSTU

1. Otwórz i przeczytaj ten plik oraz najnowsze dokumenty docs\handoff__v1.50__2026-10-14_0417.md i docs\deployment__v1.50__2026-10-14_0425.md. Przeczytaj README, ale traktuj jego parametry liczbowe jako przestarzałe.
2. Skorzystaj z Lupus Commander MultiHost albo Remote Desktop Commander na **PBOX1**. Sprawdź openfill_info, procesy i zadania. Nie publikuj ani nie żądaj MCP master secret. Potwierdź, że użytkownik nadal jest w interaktywnej sesji.
3. **Pierwsze zadanie biznesowe: SEOHost** — sprawdzenie loginu po realnym imporcie z Firefox do Windows, bez resetowania hasła. Raportuj obiektywny wynik, nazwę serwisu i konto, bez hasła.
4. **Drugie zadanie rozwojowe: popraw SystemPrompt Luny i mechanizm odczytu/scalania/zapisu notatek dla domen**. Nie duplikuj wcześniejszej wiedzy, nie gub istniejących notatek. Testy przed deploy.
5. **Równolegle lub bezpośrednio po tych krokach przygotuj logging wydajności** długodziałającego OpenFill z WebView2 i miarami RAM/CPU w czasie; nie optymalizuj bez danych. Nie myl świeżego stanu po starcie z długotrwałym obciążeniem.
6. Nie ruszaj samoczynnie kalendarza przypomnień ChatGPT: użytkownik je wszystkie wyłączył.
7. Wszystkie wyniki, raporty i aktualizacje zostaw w **wersjonowanych, datowanych plikach na dysku PBOX1**, aby użytkownik mógł je odczytać nawet wtedy, gdy odpowiedzi czatu się nie renderują.

**KONIEC HANDOFFU v1.0.**