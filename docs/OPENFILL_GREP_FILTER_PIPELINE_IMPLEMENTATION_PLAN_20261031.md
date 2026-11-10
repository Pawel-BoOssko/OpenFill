> **PLAN NADRZĘDNY OF-IMP-20261031 (31.10.2026):** dla wdrożenia filtrów terminalowych, pamięci i procedur wykonawczych obowiązuje dokument docs/OPENFILL_SCALONY_PLAN_WDROZENIA_20261031.md. Niniejszy dokument pozostaje źródłem wcześniejszych ustaleń i szczegółów technicznych. Nie uruchamiać dwóch równoległych implementacji tego samego zakresu.

# OpenFill: szczegółowy plan wdrożenia filtrowania DOM i plików
Wersja planu: 1.0, 2026-10-31. Host: PBOX1. Status: audyt wykonany, projekt implementacyjny, BEZ ZMIAN LIVE.

## 0. Cel i granice
Cel: Widok WebView2/DOM -> pełny niezmieniony zapis pliku -> lokalny filtr deterministyczny -> mały wynik i odnośnik dla Luny -> działanie -> weryfikacja efektu. Ograniczyć liczbę tur modelu, odczytów identycznego DOM, koszty i rozmiar kontekstu bez obniżenia skuteczności zadania. Unikać ponownego pisania grep w C#. Wykorzystać systemowe narzędzia plus cienką, typowaną warstwę orkiestracji C#.
Nie realizować w tym zadaniu wdrożenia na produkcję, migracji OpenFill z PBOX1, zmian modelu JEV, przebudowy mechanizmu logowania, wyłączania istniejącego get_page ani niezamówionych operacji na kontach. JEV służy ocenie poleceń/injection, nie jest wyszukiwarką DOM. Networking Radar poza zakresem.

## 1. Ustalone źródła prawdy, identyfikacja środowiska
- SOURCE: C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill
- LIVE: C:\Users\pboru\OpenFill\app\OpenFill.exe, FileVersion 1.58.0.0, proces PID 1824 sprawdzony 31.10 o ok. 20:30 EAT. PBOX1, SessionId użytkownika.
- Projekt: .NET 10/C#, WinForms, WebView2, Chromium/CDP, AgentRunner i ToolRegistry, pliki JSON/NDJSON, MCP/HTTP.
- Przepływ bazowy: klient MCP -> McpServer -> McpTaskManager/TaskRun -> AgentRunner (Luna) -> ToolRegistry -> BrowserController -> WebView2 -> rezultat.
- Obowiązujące dokumenty: docs/OPENFILL_SOURCE_FILTER_CAPTURE_ARCHITECTURE_20261030.md, docs/OPENFILL_CAPTURE_FILTER_STAGING_ACCEPTANCE_20261030.md, docs/OPENFILL_STAGING_ACCEPTANCE_ADDENDUM_20261030.md, docs/OPENFILL_CAPTURE_ATOMIC_ACCEPTANCE_20261030.md, docs/OPENFILL_RELEASE_1_58_PRODUCTION_ACCEPTANCE_20261030.md, docs/OPENFILL_TOKEN_WASTE_AUDIT_20261031.md, docs/OPENFILL_ZASADY_AKTUALIZACJI_I_LOKALIZACJI__2026-10-29.md, docs/OPENFILL_WHATSAPP_SPEC_2026-10-29.md oraz master_action_plan v1.7.
- Dowody wdrożenia 1.58: pełna regresja 1461 PASS/0 FAIL, dwie produkcyjne próby page_capture na example.com, pełne pliki i SHA256, manifest z wynikiem regex. Komponenty wdrożone po raportach STAGING; te wcześniejsze raporty nie opisują aktualnego LIVE.
- Rzeczywista sesja zalogowanego LinkedIn /messaging/ w izolowanym QA pozostaje niepotwierdzona: QA trafiało na /login/. Weryfikacja aktualnego autoryzowanego kontekstu obowiązkowa przed capture.

## 2. Inwentaryzacja: istnieje, ale trzeba poprawić wykorzystanie
| Komponent | Stan sprawdzony | Precyzyjny brak |
| --- | --- | --- |
| Agent/PageCaptureService__v0.1__2026-10-30.cs | Jeden odczyt JS z querySelector, tryby text/elements/html/all, pełne pliki .txt/.elements.json/.html, opcjonalny .filtered.txt, SHA256, podgląd 400 znaków, pliki .pending | Brak automatycznego uruchamiania dla zwykłych widoków, selekcja ograniczona do obecnego DOM, max 10 mln znaków na dokument, limit 3000 elementów, brak kompletnego trwałego manifestu indeksowego |
| Agent/PageAuthenticationGate__v0.1__2026-10-30.cs | Logowanie przed zrzutem; statusy authentication_required, authentication_unverified, URL_MISMATCH | Konieczna weryfikacja selektorów autoryzacji na realnych witrynach, bez utożsamiania istnienia sesji z zalogowaniem |
| Agent/CaptureRecipeStore__v0.1__2026-10-30.cs | Host/path/mode/selector/regex; licznik sukcesów technicznych, task success/partial/failed, ranking | Brak semantycznego widoku i typu zadania, jakości dopasowań, TTL, odróżnienia dynamicznych list i wersjonowania receptur |
| Agent/TerminalProcessService__v0.1__2026-10-30.cs | TerminalProcessService.ExecuteAsync: powershell/python/bash/grep/sed/awk/rg/jq; stdout/stderr do plików, timeout do 120 s, wyjście max 400 znaków | Brak rg/jq w systemie, brak typowanego interfejsu do typowych filtrów, niejednoznaczne mapowanie exit code i cytowania argumentów; limit 16 mln znaków bez strumieniowego wyszukiwania selektywnego |
| Agent/ToolRegistry__v0.22__2026-10-29_2314.cs | page_capture, capture_recipes, terminal_exec, run_js save_to, shared_save from_overflow i get_page | Luna musi ręcznie wybierać operacje i budować ich argumenty, brak pojedynczego polecenia capture+filter+select+verify |
| Agent/FailedCallGuard__v0.1__2026-10-30.cs | Trzecia identyczna nieudana próba blokowana | Sukces act/navigate może zerować blokadę nawet bez udowodnionej zmiany semantycznego stanu; brak odrębnego wykrywania redundantnych odczytów |
| Agent/AgentRunner__v0.29__2026-10-29_2314.cs | Instrukcje preferowania page_capture i terminal_exec; PromptHistoryCompactor; logi narzędzi | Wciąż modelowa decyzja i dodatkowa tura przy każdym odczycie; systemowy prompt sam nie zapewnia automatycznej filtracji |
| Mcp/McpServer i istniejące openfill_page_snapshot | Fizyczna karta istniejącego task_id, zgodny requester/chat, opcjonalny expected URL, zrzut bez Luny | Nie nawigować samodzielnie przez MCP; brak uniwersalnego zapytania do utrwalonego pliku bez odpalania kolejnej sesji Luny |
| WhatsApp dedykowane narzędzia | whatsapp_open/read_visible/list_all_previews/send wdrożone | Batch numerów i eliminacja ponownego otwierania, detekcja zmian widoku i stanu |
| get_page/diff i OutputLimiter | Zostają jako ścieżka interakcyjna i awaryjna | Nie zastępować ich ślepo pełnym HTML; odróżnić semantyczne elementy od dekoracji i śmieci |

Stan terminala sprawdzony na PBOX1: GNU grep 3.0, GNU sed 4.9, GNU Awk 5.4.1, Git Bash, Python 3.12 i Windows PowerShell 5.1 istnieją. Polecenia rg i jq nie są znalezione. Zapis w przełączniku shell dla rg/jq istnieje, więc aktualnie wywołanie tych dwóch zakończy się błędem uruchomienia.

## 3. Skala problemu i punkt odniesienia
Audyt 31.10: 1512 identyfikatorów przebiegów, 1477 z wierszem kind=task, 73329 wywołań modelu, 1 499 464 216 input tokens, w tym 1 249 851 605 cached (83,35%), 249 612 611 niecache'owanych; output 6 552 358, reasoning 3 615 364, raportowana suma kosztów 40,709 USD. Liczby sumują także nietypowe i niepełne przebiegi, nie są rozliczeniem faktury.
Narzędzia: get_page 19887, act 16663, run_js 8693, wait 5782, read_text 5440, navigate 5047, screenshot 3230, search_logs 2299, get_network 1458, act_many 1420. page_capture użyty w logach dwa razy, nie ma dowodu szerokiej adopcji.
Przypadki regresyjne: LinkedIn 20261031_082614_0002d1: 79 run_js, 24 search_logs, 21 wait; WhatsApp 20261030_112322_236e23: 69 whatsapp_open dla 13 numerów, 56 powtórek; Airbnb 20261030_115301_b53421: 116 search_logs, 90 get_network, 100 get_page, 84 act. Nie interpretować wszystkich powtórzeń jako błędów bez sprawdzenia postępu.
Zadanie człowieka jako wzorzec: otwórz widok raz, odczytaj potrzebne rekordy grupowo, odfiltruj lokalnie, wykonaj serię działań, sprawdź rezultat zbiorczo; ponów odczyt tylko po realnej zmianie stanu lub niepowodzeniu. Cel redukcji to liczba BEZPRODUKTYWNYCH tur, nie arbitralne zatrzymanie działającego procesu.

## 4. Docelowy kontrakt architektury
WEJŚCIE: task_id, requester/chat, faktycznie otwarta karta, URL, cel operacji, wymagany zakres (visible/loaded/whole virtual list), mode, predykat filtrowania, limit wyników, oczekiwany rodzaj danych, konto, stan auth.
CAPTURE: BrowserController czyta jeden spójny snapshot DOM, PageAuthenticationGate sprawdza auth; zapisz surową treść do plików bez modyfikacji. Każdy plik posiada byte_count, SHA256, czas, URL, selector, route, status kompletny/częściowy.
SELECT: deterministyczna ścieżka przypisuje widok i najlepszą potwierdzoną recepturę. Użyj CSS do wstępnego ograniczenia korzenia, rg dla tekstu, jq dla JSON, w razie potrzeby grep/sed/awk/Python dla niestandardowego formatu. Gdy nie ma receptury, użyj neutralnej i nie zatajaj brakujących elementów.
RESULT: do Luny jedynie ustrukturyzowany zbiór dopasowań z polami, kotwicami oraz informacją o kompletności, źródłowym pliku i statusie. Wynik pełny pozostaje na dysku i jest dostępny do odczytu selektywnego bez kolejnego pobierania całej strony.
ACTION: odczyt elementów interaktywnych i działanie nadal odbywają się przez get_page/act/act_many lub dedykowany adapter. Po bloku akcji porównaj stan przed/po i wykonaj jedno uzasadnione sprawdzenie.
STATE: utrzymuj per task i per tab ostatni view fingerprint, wersję snapshotu, listę zwróconych rekordów i wykonanych działań; wynik identycznego widoku można obsłużyć z cache lokalnego, bez kolejnej tury modelu. Zmiana URL, strony, konta, DOM danych lub stanu ładowania unieważnia cache.
RECIPE: zapis metod technicznych bez prywatnych treści witryny, ewaluacja z wyniku CAŁEGO zadania, nie tylko udanego odczytu.

## 5. Etap OF-FLT-00: zamrożenie dowodów i bazowy benchmark [P0]
Działania: zanotować wersję, proces i SHA256 LIVE/Release/SOURCE, odczytać stan komponentów i testów. Zrobić wyłącznie read-only parsowanie istniejących usage_202610.ndjson i app_202610*.ndjson; skorzystać z tools/audit_token_waste__v0.1__2026-10-31.py. Wybrać stałe zestawy scenariuszy: prosty formularz, tabela 100 rekordów, wirtualizowana lista, lista LinkedIn, WhatsApp 10 numerów, Airbnb daty i GBP. Zdefiniować niezależne kryteria rezultatu i punkty pomiarowe.
Artefakty: docs/FILTER_BASELINE_20261031.json i tekstowy raport metryk per scenariusz; identyfikatory przebiegów, model, wersja, domena, liczba model calls, uncached input, full input, tokens output, koszt, czas, pobrania DOM, niezależny sukces. Zapisz też liczbę niezmiennych odczytów.
Gate: nie rozpoczynać optymalizacji bez reprodukowalnej definicji sukcesu, baseline i listy wyłączeń. Raport z 31.10 jest bazą HISTORYCZNĄ, nie zastępuje A/B na tym samym zadaniu.

## 6. Etap OF-FLT-01: dopełnienie narzędzi terminalowych [P0]
1. Zainstalować na PBOX1 do katalogu narzędzi OpenFill (nie zależeć od zmiennego PATH) zweryfikowane, przypięte wersje ripgrep rg.exe i jq.exe. Zapisać pochodzenie, wersję, oficjalny URL, SHA256 oraz zgodność x64 i Windows. Nie nadpisywać systemowych narzędzi Git.
2. TerminalProcessService: zastąpić rozwiązywanie rg.exe/jq.exe z PATH konfiguracją absolutnych ścieżek w katalogu narzędzi, z jawnym testem wersji i dostępności przy inicjalizacji. Zachować fallback GNU grep/sed/awk, Python i PowerShell.
3. Dla poleceń grep/rg/jq/sed/awk użyć ProcessStartInfo.ArgumentList i argumentów przekazanych osobno, nie ręcznie sklejonego ciągu psi.Arguments. Zapewnić prawidłowe ścieżki zawierające spacje, Unicode, cudzysłowy i apostrofy.
4. Rozróżnić normalny brak dopasowania grep/rg (exit 1, zero_matches) od awarii (exit 2 lub wyższy); osobno błędy JSON jq, błędy PowerShell, timeout, cancellation, executable_missing oraz stderr. Zwracać status, exit code, path, bytes, truncated, elapsed_ms.
5. Pilnować strumieniowego wejścia/wyjścia i limitu czasu, zapisywać pełen stdout do pliku (oraz jawnie sygnalizować fizyczny limit 16 mln znaków, dopóki ten limit istnieje), a nie przesyłać go do Luny. Umożliwić wybór tylko n pierwszych dopasowań bez generacji megabajtów.
6. Wprowadzić opisane w jednym pliku statusy tool_available, tool_missing, version_mismatch, invocation_error i integration_ready. Nie ujawniać tylko nieprecyzyjnego "terminal failed".
Gate: test na prawdziwym PBOX1, zweryfikowane wersje/hashy, UTF-8 i polskie znaki, path z odstępami, zero matches, wielki stdout, timeout, stderr, anulowanie i exit code. Żadne zmiany LIVE.

## 7. Etap OF-FLT-02: typowany filtr plików jako usługa C# nad gotowym CLI [P0]
Przygotować Agent/FileFilterService.cs, Agent/FilterPlan.cs, Agent/FilterResult.cs; nie implementować własnego silnika wyrażeń regularnych, indeksu tekstowego ani nowego shella. C# odpowiada za bezpieczne argumenty, dobór narzędzia, uruchomienie, kontrolę kompletności, tworzenie plików i kontrakt rezultatu.
Kontrakt żądania FilterPlan v1:
- source_path lub capture_id, file_kind: text/elements_json/html/ndjson; operacja: lines, regex, literal, fields, json_select, count, context, unique, aggregate; wzorzec/ścieżka JSON; case_sensitive; max_results; max_result_chars; before_context; after_context; sort_key; preserve_order; filter_version; expected_source_sha256.
- Dla regex w tekstach użyć rg, dla prostej zgodności literalnej rg -F, dla struktur JSON jq -c; dla wyjścia NDJSON jq w trybie pojedynczych obiektów bez -s, gdy nie trzeba wczytywać wszystkiego; dla wybranych transformacji sed/awk; Python tylko gdy nie istnieje prosty odpowiednik. CSS do ograniczenia korzenia w WebView2 przed zapisem pliku; nie parsować pełnego HTML regexem.
- Polecenia tworzyć z białej listy operacji i argumentów, parametry przekazywać przez ArgumentList. Nie składać dynamicznych komend bash w modelu dla typowych prac. Terminal_exec pozostawić dla wyjątków i diagnostyki.
- FilterResult: status ok/zero_matches/error/timeout/partial; source_capture_id, source_sha256, source_bytes, source_kind, filter_plan_id i jego hash, matcher, matches_total, returned, omitted, complete, truncated, data_path, data_sha256, preview, elapsed_ms, exit_code, stdout_path, stderr_path, errors; identyfikatory trafień i zakresy wierszy. Wynik status=ok nie oznacza zakończenia zadania.
- Odpowiedzi do modelu mają zawierać pierwsze wybrane rekordy i policzalny count; nie dopisywać CSS/SVG/skryptów i surowego HTML, chyba że zadanie tego wymaga. Każdy rekord posiada ślad do pełnego źródła, nie tylko tekst wycięty do 400 znaków.
- Zapytania do już pobranego capture_id NIE wywołują WebView2; można ponawiać i zmieniać filtry bez ponownego DOM/read_page. Powtórny DOM wymagany dopiero gdy użytkownik wymaga świeżego stanu albo znacznik widoku rzeczywiście się zmieni.
- Obsłużyć Unicode i UTF-8 bez BOM, duże linie (w obecnym PageCaptureService pojedynczy zwrócony wiersz ma max 2000 znaków), regex z 0 dopasowań, niepoprawny regex, JSON niepoprawny, błędy typu source_missing/source_changed i wyjście dla pustego HTML.
- Niezmienione surowe pliki muszą pozostać dostępne; wynik filtra tworzy NOWY plik, nie nadpisuje źródła. Manifest źródła i manifest filtra zapisywać atomowo do JSON z hashem, w katalogu właściwego zadania.
Gate: testy porównujące plik bezpośrednio z przetworzonym wynikiem referencyjnym. Dla wszystkich fixture: 100% recall oczekiwanych rekordów i kontrolowana precyzja przy różnych rozmiarach, bez utraty pierwotnego źródła.

## 8. Etap OF-FLT-03: PageCaptureService jako centralne wejście [P0]
W źródłowym PageCaptureService dodać trwały manifest capture_id.json, mapujący wszystkie źródła i utworzone wyniki oraz pełne stany: complete, source_truncated, selector_not_found, authentication_required, stale_view, lazy_content_not_loaded. Zachować obecną kompatybilność wartości zwracanych przez page_capture.
Wprowadzić capture_root i nazwę widoku bez utrwalania prywatnej treści jako receptury. Przy dużym DOM nie wykonywać bezmyślnie mode=all: dobierać text lub elements i w razie potrzeby tylko wybrany root. Przekroczenie 10 mln znaków ma zwracać czytelny status i propozycję węższego zakresu zamiast pozostawienia plików częściowych. Aktualny mechanizm *.pending pozostawić i przetestować awarie na każdym etapie.
Dodać znacznik dom_generation lub observed_at wraz z URL, title, document.readyState, strukturą root, liczbą elementów i signal lazy_loaded/virtualized. Nie nazywać częściowo załadowanej listy kompletną. Dla shadow DOM, iframe i elementów wirtualizowanych pozostawić jawne pole scope i osobny mechanizm dedykowany, nie udawać pełnego zrzutu. Różne korzenie capture mogą być utrwalone w jednym logicznym zadaniu pod wspólnym identyfikatorem.
Auth ma być sprawdzony PRZED zapisem, zgodnie z działającym PageAuthenticationGate. Nie wystarczy redirect do URL chronionej sekcji; użyć markerów wskazujących autoryzowaną sesję. Realny LinkedIn zalogowany pozostaje osobnym gate QA.
Nie dodawać pobrań Blob ani wymagania wielokrotnych automatycznych downloadów do normalnego odczytu DOM. Dotychczasowy realny WebView2 MultipleAutomaticDownloads test jest wyłącznie regresją zachowania witryn wymagających pobierania.
Gate: jeden logiczny snapshot z widocznego obszaru, wszystkie poprawne pliki + SHA256, brak pliku końcowego przy awarii, zapisane metadata; zero dodatkowych rund modelu na tworzenie każdego pliku.

## 9. Etap OF-FLT-04: automatyczna detekcja widoku i przepisy filtrów [P0]
Rozszerzyć CaptureRecipeStore, zachowując migrację istniejących JSON. Klucz receptury to: canonical_host + normalized_path/route + view_type + operation_type + mode + selector + plan_id. Query string zawierający parametry operacji uwzględnić selektywnie w strukturze route (np. data/strona/filtrowanie), nie dopasowywać wszystkich adresów losowo do jednej receptury. Uwzględnić cechy konta/kontekstu bez zapisywania tożsamości i danych prywatnych w recipe.
Receptura w wersji v2: recipe_id, schema_version, host/path_pattern, view_signature, operation_type, scope (loaded/visible/all), css_root, file_kind, filter_plan, expected_columns, expected_record_minmax, technical_ok, task_ok/partial/failed, no_result_count, last_success, last_failure, median_elapsed_ms, last_validation, last_dom_signature, confidence_status (candidate/verified/suspended), provenance. Przechowywać tylko parametry techniczne i statystyki.
Wybór: zweryfikowane exact view+operation > zweryfikowane route > neutralny capture. Preferować kryterium sukcesu końcowego, pokrycia pól i świeżości nad liczbą wywołań; istniejący ranking task_ok/task_partial/task_failed zachować jako sygnał, ale wprowadzić bramkę walidacji widoku. Nie wykonywać złej receptury po zmianie struktury strony.
Nową recepturę zapisywać po poprawnym capture jako candidate; przełączać na verified dopiero po spełnieniu walidatorów pola/kompletności i wiarygodnego wyniku zadania. Przy błędzie struktury nie usuwać receptury, oznaczać suspended i stosować neutralny fallback, zapisując konkretną przyczynę.
Dla witryn regularnych: WhatsApp Web, LinkedIn /messaging/, /mynetwork/, Airbnb kalendarz, Google Business Profile i Facebook osobne definicje widoków oraz zestawy selektorów. Możliwość zapytania capture_recipes zachować, ale domyślne wybieranie powinno zachodzić bez osobnej decyzji Luny.
Gate: co najmniej 3 powtórne testy na tym samym widoku ze skutecznym ponownym użyciem bez generowania JS; negatywny test innej ścieżki, widoku i niezalogowanego stanu musi odmówić nieprawidłowego reuse.

## 10. Etap OF-FLT-05: tryb automatyczny i oszczędzanie tur modelu [P0]
Dodać do ToolRegistry jeden zintegrowany punkt semantyczny, np. observe_view / query_capture. Wariant observe_view: zidentyfikuj aktualną kartę, zweryfikuj auth, wybierz recepturę, wykonaj capture (jeśli potrzebny), zapisz raw, zastosuj lokalny FilterPlan, zwróć wynik kompaktowy i fingerprint. Wariant query_capture: ten sam capture_id, inny filtr, BEZ WebView2 i BEZ modelu.
W AgentRunner/AppHost narzędzie preferować przy pierwszym większym widoku lub prośbie o listę/tabelę, bez rozbijania operacji na: get_page -> run_js -> zapisz -> grep -> przeczytaj. Dla formularzy i kontrolek nadal get_page + act/act_many. Uruchamianie pojedynczej lokalnej usługi filtrującej może być częścią istniejącego wywołania, a nie kolejną turą Luny.
Dla zwykłego zadania nad tabelą pipeline ma skończyć się jednym logicznym odczytem i filtrem, następnie działaniem, następnie jedną zbiorczą kontrolą. Zachować dostęp do odnośników i odczytu kontekstowego dla wskazanych rekordów. Całego źródła nie wkładać do Responses API.
Wprowadzić przed każdym ponownym expensive read detekcję stanu: task_id + tab_id + URL canonical + selected DOM region hash + loaded_items_count + loading_state + watched business fields. Jeśli fingerprint identyczny i brak nowego celu odczytu, zwrócić cache wraz z oznaczeniem unchanged; nie wysyłać ponownie dużego payloadu. Po akcji zweryfikować prawdziwy efekt, nie zakładać zmiany po ok/kliknięciu.
Dotychczasowy FailedCallGuard ogranicza trzecie identyczne NIEUDANE wywołanie. Dodać oddzielną kontrolę bezproduktywnego postępu, bez sztywnego blokowania działań przynoszących nową wiedzę. Reset ochrony wyłącznie po zmianie adekwatnego stanu, nie dowolnym act. Dla tego samego error+args przechowywać kontekst przyczyny i alternatywy. Warunki stopu nie mogą maskować nieudanej operacji.
Dla nieudanego select/filter: nie generować wielokrotnie nowych JS; przyczyną może być brak logowania, zmiana view, brak załadowania lazy albo błędny CSS. Rozpoznawać kategorie, wybierać jedną alternatywę, potem wykazać co pomogło i co nie pomogło.
Gate: w logach pojedynczego zadania widoczne capture_id, filter_plan_id, hash źródła, view_fingerprint, reason_for_read, task_result; brak nieuzasadnionych kolejnych odczytów identycznego widoku.

## 11. Etap OF-FLT-06: integracja przez MCP i retencja [P1]
Zachować obecny openfill_page_snapshot działający tylko na fizycznej istniejącej karcie odpowiedniego task_id/requester/chat oraz kontrolę expected_url. Rozszerzyć kontrakt o opcjonalny filter_plan lub osobny file_query(capture_id, filter_plan), działający bez modelu, a w trybie file_query bez nowego WebView2 odczytu.
Nie tworzyć dowolnej nawigacji URL bez kontekstu zadania ani nie omijać izolacji kart. Podpis metadanych i właściciela utrwalonego pliku: task_id, requester/chat, capture_id i zakres. Przy braku fizycznej karty snapshot zwraca tab_not_found, natomiast wcześniej utrwalony plik jest czytelny tylko przez uprawniony kontekst.
Retencja: przechowywać manifesty i wyniki przez żywot zadania i udokumentowany okres po zakończeniu; ustawić jawne limity liczby plików, łącznych bajtów, wiek, sygnały sprzątania oraz brak sprzątania plików w trakcie operacji. Utrzymać historię sukcesu receptury niezależnie od wygaszania surowych capture.
Zachować istniejący kontrakt openfill_info (krótki) i openfill_full_status (pełny, nieogłaszany) bez ponownego rozbudowania standardowej odpowiedzi.
Gate: requester/chat isolation, stale_task, tab_not_found, auth redirect, correct file retrieval, brak model calls, brak regresji HTTP/MCP i prawidłowe czyszczenie zakończonych zadań.

## 12. Etap OF-FLT-07: specyficzne przepływy domenowe [P1]
### WhatsApp Web
Wejście: numer z prefiksem kraju lub jednoznaczny identyfikator; dla wyszukiwania po nazwie najpierw podglądy wyrenderowanej listy. Nie zgadywać numeru. Zbiorcze list_visible_previews dla wszystkich aktualnie załadowanych wierszy, a list_all_previews tylko na wyraźne żądanie, z oznaczeniem virtualized/complete. Po otwarciu rozmowy odczytać jeden raz wyłącznie załadowane wiadomości, ewentualnie ostatnie 5-10, bez obrazu, CSS i SVG. Dla wielu numerów batch_open_read, po jednym otwarciu i odczycie na numer, lokalnie zdeduplikować wejście; per numer status opened/loaded/empty/error i hash ostatnich wiadomości. Nie zwijać braku załadowania do pustej historii. Wysyłka nadal wymaga potwierdzonego efektu, nie samego kliknięcia. Zachować obowiązującą specyfikację z 29.10, bez koncepcji Luna Mini.
### LinkedIn
Oddzielić message_list, invite_list, profile. Capture wyłącznie po pozytywnej weryfikacji sesji. Strukturalne rekordy: widoczna nazwa, URL/profil, status, data, treść podglądu, identyfikator interaktywny i znacznik zakresu. Jedno dopasowanie receptury na widok; ponowny pobór tylko przy zmianie strony/konta/filtra. Akcje do wielu osób jako act_many, potem zbiorcza weryfikacja. Osobny test zalogowanego E2E izolowanej sesji wciąż pozostaje otwarty.
### Airbnb
Przechwycić kalendarz/tabelę cen dla żądanego zakresu dat jako jeden zbiór, nie czytać get_network dla każdej daty. Lokalnie przekształcić dane do rekordów date, old_price, new_price, currency, source, write_status. Dokonać zmian poprzez istniejące autoryzowane UI i potwierdzić każdą datę zbiorczo. Przy asynchronicznym UI zrobić jedną diagnostykę źródłowego błędu, nie powtarzać setek search_logs.
### Google Business Profile, Facebook i pozostałe
Wybrać dwa często powtarzane widoki na podstawie usage log, opisać root CSS/JSON, strukturalne rekordy, selektory akcji, warunki widoczności i signaturę DOM. Nie tworzyć osobnej biblioteki parsującej dla każdej witryny, jeżeli wystarcza FilterPlan + recipe. Adapter dopiero gdy standardowy pipeline nie reprezentuje treści lub interakcji.

## 13. Etap OF-FLT-08: testy izolowane i kryteria akceptacji [P0 przed LIVE]
Testy statyczne i jednostkowe, rozszerzenie tests/OpenFill.Tests/CapturePipelineTests__v0.1__2026-10-30.cs oraz testów ToolRegistry, MCP i AgentRunner:
T01: rg literal/regex, case sensitivity, Unicode PL, ścieżki z odstępem i Unicode, 0/1/10000 dopasowań; exit 1=zero_matches, exit 2=error.
T02: jq JSON i NDJSON, selekcja pól, zliczenie, filtrowanie po liczbie i stringu, invalid JSON; bez przypadkowego slurp wielkiego pliku.
T03: grep/sed/awk/batch bash, Python i PowerShell nadal działają; stderr, exit code, timeout, cancellation, brak programu, brak danych; rezultat do pliku, a modelowy preview ograniczony.
T04: pełny capture text/elements/html/all, regex, record IDs, poprawność hash, UTF-8, manifest, atomic cleanup; awaria przy zapisie drugiego i ostatniego pliku, brak pozostałości .pending. Obecny zestaw 1461 PASS z 1.58 jest punktem odniesienia, a nie zastępnikiem nowej regresji.
T05: Source unchanged: liczba bajtów i SHA256 surowego pliku przed/po filtracji identyczne; 0 utraconych oczekiwanych rekordów fixture; wynik filtra wskazuje źródło i wiersze, a nie zastępuje pełnego materiału.
T06: maxMatches, max chars, 10 mln znaków, 3000 interaktywnych elementów, długie wiersze, 16 mln znaków terminal stdout, sygnalizacja częściowości i truncation na KAŻDEJ warstwie, nie fałszywy complete.
T07: recipe: exact host/route/view/mode, inne konto/widok odrzucane, pomyłka rankingu wykryta, task_ok wygrywa z częstymi nieudanymi technicznie receptami; migracja plików v1 bez utraty liczników.
T08: fingerprint: niezmienione DOM -> reuse bez modelowych pełnych odczytów; zmienione rekordy, query, auth, URL, paging, laziness -> refresh; po act bez zmiany stan nie może udawać sukcesu.
T09: auth: public, logged out, login form, redirect, checkpoint, stale session, poprawne auth marker; na błędzie 0 plików eksportu; nie pomijać logowania.
T10: WebView2 MainForm, fizyczne okno QA i prawdziwy HTTP MCP, 4 pliki snapshot, requester/chat isolation, 3/3 pobrania WebView2 (regresja), 0 dodatkowych model calls do zrzutu.
T11: frame/shadow DOM i wirtualizowana lista: stan partial/loaded oznaczony; test ponownego załadowania danych i brak podszywania się pod pełną listę.
T12: test przeciążeniowy: 100 / 1000 / 10000 wierszy, co najmniej 10 MB pliku, profil CPU/RAM/IO, cleanup, brak wielokrotnego przesyłania tego samego tekstu do modelu.
T13: regresja interakcji: get_page, diff, act/act_many, run_js, WhatsApp; nie wolno pogorszyć login flow, formularzy, kart, downloadów i obsługi dialogów.
T14: regresja MCP/HTTP: client disconnect/reconnect, stale capture, file not found, cancellable task, przerwanie maintenance, session identity, brak dostępu do cudzych kart.
T15: prompt i real model: ten sam cel w starym i nowym wariancie; testy nie mogą opierać się wyłącznie na sztucznym HTTP/DOM fixture.

Realne scenariusze E2E w izolowanym STAGING:
A: Strona testowa, 100 wierszy, 1 zrzut, grep/rg, jq, kontrola pełnej i filtrowanej treści bez Luny.
B: Realna publiczna strona, kilka różnych korzeni, zmiana sortowania/paginacja, stale content.
C: Realna zalogowana witryna, LinkedIn messaging i lista zaproszeń, bez wysyłek do innych osób; udowodniony prawidłowy login i 3 pliki. Nie oznaczać pass, gdy otworzył się login.
D: WhatsApp 10 jawnie wybranych numerów (bez wysyłki) i 10 rozmów po nazwie, porównać liczbę otwarć z liczbą unikalnych numerów, kompletność ostatnich wiadomości i podglądów.
E: Airbnb pełny zakres dat na testowym/sandbox koncie lub autoryzowany bezskutkowy podgląd, odczyt hurtowy i poprawne wnioski bez edycji realnego kalendarza.
F: Google Business lub Facebook, widok listy i edycja tylko w autoryzowanym środowisku QA.
G: Jedno zadanie z nieoczekiwanym błędem selektora: max jedna przyczynowa diagnostyka oraz uzasadniony fallback, brak pętli narzędzi.

Oczekiwany dowód PASS: rzeczywiste artefakty z pliku, hash, ścieżka, task_id, model/version, liczba calls, koszt, wynik aplikacji, porównanie efektów i kontraktów. Brak domyślnego sukcesu wyłącznie na podstawie exit 0 kompilatora.

## 14. Etap OF-FLT-09: pomiar jakości i efektywności [P0]
Per task i per logical operation zapisywać: task_id, model, site, view_type, source_capture_id, source_bytes, filtered_bytes, number_of_unique_dom_reads, duplicate_unchanged_reads, recipe_hit/miss, local_filter_count, local_filter_duration, terminal_exit_code, tool_calls by name, model_calls, uncached_input_tokens, cached_input_tokens, output_tokens, cost_usd, wall_elapsed_ms, result (success/partial/failed/unknown) i proof_source. Odróżniać koszt Luny od transportu MCP i operacji WebView2.
Metoda A/B: ten sam scenariusz, ta sama wersja modelu i identyczny efekt końcowy, bez modyfikowania prawdziwych danych w wariancie kontrolnym. Porównać medianę, P90, P95; oddzielić network latency i cache modelu. Dla niepełnych logów outcome=unknown zamiast failed.
Mierniki akceptacji obowiązkowej: brak utraty wymaganych rekordów w fixture, brak nowych błędów, brak regresji wyniku w realnym QA, pełne source files i manifest zgodny SHA256, nieprzekazywanie całego HTML do Luny w typowym zadaniu, brak identycznego DOM dostarczanego modelowi bez przyrostu, przy braku wyniku jawne partial/failed.
Cele optymalizacyjne do sprawdzenia, a nie deklaracje osiągniętych wyników: w zadaniach tabelarycznych median uncached input tokens <= 50% baseline, calls do modelu <= 60% baseline, get_page/run_js/search_logs na tym samym widoku <= 50% baseline; zadania WhatsApp 10 numerów bez powtórnych otwarć, jeśli w stanie załadowanym nic się nie zmieniło. Nie przyjmować redukcji kosztu za sukces, jeśli efekt gorszy.
Efekty raportować konkretnie: baseline/result/ratio, stopień kompletności oraz przyczyny nieosiągniętych progów. Zero arbitrarnych progów uniemożliwiających rzeczywisty postęp.

## 15. Etap OF-FLT-10: release, rollback, utrzymanie [P1 po E2E]
1. SOURCE: osobne zmiany w niewdrożonym kodzie; build dotnet i pełne testy, log exit code, liczba PASS/FAIL, hash binarek.
2. STAGING: publish do osobnego katalogu release, oddzielny profil i instancja; zweryfikować realną kartę WebView2, narzędzia rg/jq, MCP, auth, retencję i wyniki filtrów. Wymóg: zielone pełne T01-T15 i E2E A-G lub jawnie zakwalifikowane scenariusze jako nieukończone, bez ukrywania tego w ogólnym PASS.
3. Preflight: backup aktualnego LIVE i danych, sprawny mechanizm rollback, prawidłowy instalator, autostart w interaktywnej sesji PBOX1, kompatybilne receptury v1, zachowanie otwartych kart i interrupted states.
4. Po odrębnej decyzji wydania stosować obowiązujące zasady 29.10: blokada nowych startów, komunikat dla osiagalnych klientów dokładnie "Teraz nastąpi aktualizacja. Wróć do nas za kilka minut.", oznaczenie aktywnych jako interrupted i zachowanie idempotency_key, kontrolowane zatrzymanie, instalacja, restart, smoke. Nie czekać na zero zadań i nie tworzyć zastępczego serwera MCP.
5. LIVE: sprawdzić PID/wersję/hash, połączenia MCP, panel GUI, JEV niezależnie, get_page, page_capture, rg/jq, read-only public capture, filtr 0 i 1 match, recipes i bezpieczną kontynuację istniejącej karty. Odnotować rzeczywisty stan po każdym kroku, bez udawania testu LinkedIn.
6. Rollback: przy nieudanym smoke, brakach plików, awarii auth, regresji rezultatów albo problemach z kontynuacją wrócić do znanego poprzedniego release. Receptury v2 powinny być wstecznie kompatybilne lub mieć oddzielną kopię i migrację odwracalną. Nie wznawiać automatycznie skutkowych czynności o niepewnym wyniku.
7. Obserwacja: porównać pierwsze rzeczywiste zadania ze scenariuszami baseline, nie twierdzić o skuteczności do czasu otrzymania kilku kompletnych wyników; wykrywać nieznany DOM/recall regression i przełączać jedną witrynę na stary get_page bez wyłączania całości.
8. Nie wykonywać ręcznego git commit/push/reset ani zmian w Networking Radar. LIVE nadal na PBOX1.

## 16. Mapowanie plików SOURCE i zakres zmian
| SOURCE | Zamierzona zmiana |
| --- | --- |
| src/OpenFill.Core/Agent/TerminalProcessService__v0.1__2026-10-30.cs | Absolutne ścieżki narzędzi, osobne ArgumentList, exit status, dojrzały stdout/stderr/limit |
| src/OpenFill.Core/Agent/PageCaptureService__v0.1__2026-10-30.cs | Trwały capture manifest, fingerprint, stale/lazy markers, indeks kotwic |
| src/OpenFill.Core/Agent/CaptureRecipeStore__v0.1__2026-10-30.cs | Recipe v2, widok/operacja, walidacja, ranking, migracja |
| src/OpenFill.Core/Agent/ToolRegistry__v0.22__2026-10-29_2314.cs | observe_view, query_capture, zgodność page_capture i terminal_exec |
| src/OpenFill.Core/Agent/AgentRunner__v0.29__2026-10-29_2314.cs | Routing do typowanego odczytu, reason_for_read, brak redundancji, zachowanie skuteczności |
| src/OpenFill.Core/Agent/FailedCallGuard__v0.1__2026-10-30.cs | Reset wyłącznie z udowodnionym state change, oddzielny unchanged guard |
| src/OpenFill.Core/Browser/BrowserController__v0.10__2026-10-29_2314.cs | Metadane snapshotu; zachowanie istniejącego WebView2/DOM/CDP |
| src/OpenFill.Core/Mcp/McpServer__v0.24__2026-10-28_1648.cs | Opcjonalny filter plan / file_query, bez zmiany izolacji tożsamości |
| Nowe Agent/FileFilterService.cs, FilterPlan.cs, FilterResult.cs, ViewFingerprintService.cs | Typowana orkiestracja, zliczanie, kotwice, cache i walidacja |
| tests/OpenFill.Tests/CapturePipelineTests__v0.1__2026-10-30.cs oraz MCP/QA | T01-T15; testy integracyjne i fizyczny WebView2 |
| tools/diagnose_filter_tools.ps1, tools/benchmark_capture_filter.py | Diagnostyka wersji/sha/komend i reprodukowalny A/B |

Nazwy nowych plików są propozycją implementacyjną, NIE deklaracją ich obecności. Zmiany należy prowadzić przez istniejącą strukturę projektów i pliki csproj, bez tworzenia drugiego hosta OpenFill.

## 17. Kolejność prac, zależności i odbiór
F00 baseline -> F01 zainstalowane i zdiagnozowane CLI -> F02 FileFilterService -> F03 capture manifest -> F04 recipes -> F05 automatyczny AgentRunner/ToolRegistry -> F06 MCP file_query -> F07 adaptery częstych widoków -> F08 testy (narastająco po każdym etapie) -> F09 A/B i mierniki -> F10 STAGING, decyzja wydania, LIVE i raport.
Najpierw odseparować akceptację poprawności filtra od testów skuteczności Luny. Dla wszystkich etapów zapisywać SOURCE/TEST/STAGING/LIVE, wskazane pliki, komendy, exit code i przyczynę ewentualnej blokady. Nie traktować wejścia w etap następny jako zaliczenia poprzedniego.
Najważniejsza luka do zamknięcia: obecny pipeline jest NARZĘDZIEM DOSTĘPNYM DLA LUNY, ma stać się DOMYŚLNĄ ŚCIEŻKĄ PRZETWARZANIA POWTARZALNYCH WIDOKÓW, z lokalną selekcją, stałym manifestem i wykrywaniem niezmienionego stanu. Użytkownik nadal musi otrzymywać poprawny i zweryfikowany rezultat, nawet jeśli filtry nie przyniosą zakładanej oszczędności.

## 18. Źródła do dalszej walidacji
Lokalne pliki wymienione w sekcjach 1-3 są źródłem faktów o bieżącym projekcie i wdrożeniu. Oficjalne źródła techniczne do weryfikacji flag i semantyki narzędzi podczas implementacji: ripgrep User Guide https://github.com/BurntSushi/ripgrep/blob/master/GUIDE.md oraz jq Manual https://jqlang.org/manual/. Konkretne wersje i SHA256 instalowanych binarek należy utrwalić dopiero po pobraniu z wiarygodnego wydania i lokalnej weryfikacji.

## 19. Weryfikacja uzupełniająca 31.10.2026, około 20:36 EAT
Niezależny lokalny smoke na PBOX1 przez tools/release_smoke__v0.3__2026-10-29_2235.ps1, TimeoutSeconds 3: OPENFILL_PID=1824, MCP_STATUS_OK=True, ACTIVE_TASK_COUNT=0, JEV_LOCAL_DPAPI_KEY_PRESENT=True, JEV_DIRECT_API_TRANSPORT_REACHABLE=True, JEV_MODEL_SCORE_TESTED=False, LAUNCH_TASK=Ready, FAILED_CHECK_COUNT=0, LIVE_SMOKE=PASS, SMOKE_EXIT_CODE=0. HTTP GET 404 dla nieużywanego diagnostycznego URL JEV jest zgodny z osiągalnym transportem i nie jest testem funkcji oceny. Lokalny MCP OpenFill odpowiedział prawidłowo.
Oddzielnie wywołanie konektora OpenFill z tego czatu zakończyło się dokładnie: UNAVAILABLE: Error code: UNAVAILABLE; Error: McpServerError: Connection failed. Nie dowodzi to awarii aplikacji, skoro lokalny MCP zwrócił odpowiedź. Nie zweryfikowano warstwy pośredniej konektora zewnętrznego ani przyczyny jego błędu. Tę trasę zweryfikować osobnym testem integracyjnym przed oznaczeniem pełnego zdalnego E2E jako PASS.
Przy próbie odczytu HEAD w SOURCE polecenie git rev-parse --short HEAD zwróciło fatal: Needed a single revision (exit 128), a git status --short wskazał pliki źródłowe jako untracked. Lokalny katalog .git nie dowodzi istniejącego punktu przywracania. Backup i rollback muszą opierać się na zweryfikowanych kopiach release, dopóki nie zostanie osobno uporządkowana historia Git.
