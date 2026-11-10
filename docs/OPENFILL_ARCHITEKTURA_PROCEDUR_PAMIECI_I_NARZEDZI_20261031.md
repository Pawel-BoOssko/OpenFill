> **PLAN NADRZĘDNY OF-IMP-20261031 (31.10.2026):** dla wdrożenia filtrów terminalowych, pamięci i procedur wykonawczych obowiązuje dokument docs/OPENFILL_SCALONY_PLAN_WDROZENIA_20261031.md. Niniejszy dokument pozostaje źródłem wcześniejszych ustaleń i szczegółów technicznych. Nie uruchamiać dwóch równoległych implementacji tego samego zakresu.

# OpenFill: architektura narzędzi, procedur i trwałej pamięci
## Raport architektoniczny v2.0, 31.10.2026
Status: projekt dla agenta rozwijającego SOURCE. Dokument nie oznacza wykonania zmian ani wdrożenia LIVE.
Host projektu: PBOX1. Referencja wejściowa: docs/OPENFILL_GREP_FILTER_PIPELINE_IMPLEMENTATION_PLAN_20261031.md.
Priorytet właściciela: wykorzystać gotowe CLI i lokalny dysk; unikać przesyłania dużych danych przez model i MCP; przygotować miejsce dla wielokrotnie używanych procedur. Zakres tego raportu: ARCHITEKTURA I LOGIKA, bez projektowania kodu, testów i harmonogramów wdrożenia.

## 1. Decyzje architektoniczne
1. Trzy niezależne pojęcia: NARZĘDZIE wykonuje pojedynczą operację, RECEPTURA opisuje sposób czytania/obsługi konkretnego widoku, PROCEDURA realizuje powtarzalny cel użytkownika jako sekwencję stanów i działań.
2. Czwarty element: PAMIĘĆ OPERACYJNA. Jej rolą jest zachowanie raz ustalonych identyfikatorów, linków, rozmów, planów działań, skutków i niepewności, nie tylko historii rozmowy z modelem.
3. Model zlecający określa cel i dane wejściowe. Luna podejmuje nietypowe decyzje, identyfikuje wyjątki i uczy się metod. OpenFill lokalnie wykonuje standardową procedurę, filtrowanie, zapis i weryfikację.
4. Używać jednego istniejącego silnika WebView2/BrowserController, jednego wykonawcy operacji i jednej Luny. Nie tworzyć Luny Mini, drugiej przeglądarki ani osobnego środowiska sterowania.
5. DOM z przeglądarki tylko raz na logiczny widok, a następnie wiele zapytań do PLIKÓW i lokalnych indeksów. Nie traktować „jeden odczyt” jako gwarancji, że wirtualizowana lub dynamiczna lista jest pełna.
6. Historyczne informacje w plikach i lokalnej bazie danych; pamięć procesu przyspiesza, ale nie jest jedynym źródłem. WebView2 przechowuje sesję witryny i jej własne magazyny, nie stan OpenFill.
7. Wykorzystać ripgrep, jq, GNU grep/sed/awk, Python i SQLite/FTS5. C# ma spajać istniejące funkcje, a NIE implementować wyszukiwarki, bazy, parsera HTML ani nowego języka skryptowego.
8. Instrukcje modelowe to rekomendacje. Powtarzalny wybór procedury, deduplikacja operacji i rejestrowanie wyników muszą być wymuszane w OpenFill, bo model nie jest trwałym mechanizmem pamięci.
9. Zapisy na stronie i wysyłki mają status verified / uncertain / failed. Sam klik, HTTP 200 lub brak błędu nie dowodzą efektu. Nie ponawiać uncertain automatycznie.
10. Nie upychać całej historii do promptu Luny. Odczyt selektywny: zadanie i osoba -> rekordy -> tylko istotne szczegóły. Cała historia jest zachowana na dysku i wyszukiwalna.

## 2. Stan faktyczny i luka poprzedniego planu
### Obecne komponenty, potwierdzone w SOURCE
- PageCaptureService: zapis DOM/tekstu/elementów do plików, SHA256, zwięzły podgląd, pliki tymczasowe.
- CaptureRecipeStore: host/path/selector/mode/pattern, liczniki techniczne i rezultatów.
- TerminalProcessService: uruchamianie programów i zapis stdout/stderr do plików; obsługa nazw rg i jq w konfiguracji, ale obu programów nie znaleziono na PBOX1.
- AgentRunner i ToolRegistry: posiadają capture, terminal_exec, run_js, get_page/act/act_many, ale nie domyślny automatyczny router zadań do procedur.
- WhatsAppBrowser: gotowe dedykowane operacje odczytu podglądów, otwierania rozmowy, odczytu wiadomości, wysyłki; specyfikacja zakazuje Luny Mini i normalnego korzystania z obrazów.
- SiteNotesStore: około 180 plików notatek o domenach w aktualnym profilu. To luźna wiedza o witrynach, nie indeks kontaktów ani historia wykonanych czynności.
- HistoryStore: append-only NDJSON z historią ZADAŃ, start/koniec/summary. Nie jest to rejestr indywidualnych wiadomości, odbiorców i wyników.
- PromptHistoryCompactor: utrzymuje rozmiar kontekstu modelu, nie zastępuje pamięci aplikacyjnej.
- AppPaths: osobna trwała przestrzeń data/stable lub data/dev z profilem WebView2, logs, notes, tasks i runtime.

### Fakty o zużyciu, audyt 05-31.10.2026
| Grupa historycznych zadań | Przebiegi | Wywołania modelu | Raportowany koszt USD |
| --- | ---: | ---: | ---: |
| LinkedIn | 247 | 13687 | 12,8548 |
| Google, grupa szeroka | 327 | 23008 | 11,2818 |
| Facebook/Meta | 139 | 10801 | 4,9177 |
| WhatsApp | 167 | 6582 | 2,7457 |
| Airbnb | 25 | 1522 | 0,8141 |
Łącznie: 1477 zadań, 73329 wywołań modelu, około 1,50 mld input tokens, w tym około 1,25 mld cached; około 40,71 USD zapisanych w logach. To historyczne, nieporównywalne bezpośrednio koszty, a nie faktura.
W konkretnym przebiegu WhatsApp 69 otwarć dotyczyło 13 numerów, czyli 56 otwarć powtórnych; inny przebieg wykonał 11 otwarć dla 11 numerów. Istnieją już dobre prymitywy, brakuje trwałego planowania stanu.
WNIOSKI: optymalizować najpierw typowe scenariusze LinkedIn, Google, Facebook i WhatsApp. Sama grupa „Google” nie mówi, czy najczęściej chodziło o GBP, YouTube Studio, Gmail czy wyszukiwarkę: podział na intencje należy wyliczyć z historii, nie zgadywać.
Poprzedni plan szczegółowo rozpisuje capture/filter i testy, ale nie wyznacza schematu tożsamości odbiorcy, trwałych kontaktów, proceduralnego wykonawcy, konsekwencji niepewnej wysyłki, polityki aktualizacji pamięci ani zasad dla zlecającego.

## 3. Architektura warstwowa
Warstwa A, MODEL ZLECAJĄCY: opisuje rezultat, odbiorców, kanał, treść, zakres, ograniczenia, zgodę na operację. Nie steruje pojedynczym klikiem, nie musi znać DOM, nie przekazuje całej historii.
Warstwa B, ROUTER INTENCJI OpenFill: klasyfikuje zlecenie do znanej procedury lub przekazuje je do zwykłego trybu Luny. Tworzy plan i trwały operation_batch_id jeszcze przed pierwszym skutkiem zewnętrznym.
Warstwa C, LUNA: rozumie zadania nieobsługiwane, rozwikłuje niejednoznaczności, dobiera strategię wyjątkową i może zaproponować nową procedurę. Nie musi ponownie przeglądać lokalnej pamięci ani budować każdej komendy.
Warstwa D, EXECUTOR PROCEDUR: mały deterministyczny koordynator istniejących ToolRegistry, BrowserController, WhatsAppBrowser, capture/filter i pamięci. Wykonuje kroki i aktualizuje postęp bez kolejnych tur Luny w typowej ścieżce.
Warstwa E, ADAPTERY WITRYN I NARZĘDZIA: WebView2 JS/CSS/CDP do odczytu i sterowania; ripgrep/jq/grep/sed/awk do selekcji; sqlite3 + FTS5 do trwałych rekordów; Python do przypadków niepokrytych. Zero własnych silników przeszukiwania.
Warstwa F, DYSK I PAMIĘĆ: raw captures, manifesty, znormalizowane rekordy, indeksy, log skutków, procedury, receptury i cache stanu.
Przepływ: INTENT -> resolve account/targets -> select procedure -> ensure authenticated -> fetch only missing/stale data -> local filter -> execute block -> verify outcomes -> persist events and indexes -> compact result.

## 4. Kontrakty odpowiedzialności
### 4.1 Model zlecający
Powinien przekazać: task_id/requester/chat, kanał, account_hint jeśli znany, action_type, listę celów (ID/link/numery lub dane do odnalezienia), payload, wymagany wynik, zasady dla niejednoznaczności, czy wolno wykonać skutkowe działania. W przypadku 10 wiadomości JEDNA operacja wsadowa, nie 10 równoległych poleceń MCP i nie pełen opis DOM.
Reguły opisane w dokumentacji i nazwach/kontraktach narzędzi MCP, tak aby dowolny zlecający model widział możliwości i właściwy sposób ich użycia.
MCP nie narzuci arbitralnie instrukcji systemowych modelowi klienta. Dlatego egzekwowanie obowiązkowych zachowań ma być serwerowe: normalizowanie żądania, wybór procedury, rejestr skutków, kontrola ponowień.
### 4.2 Luna
Dostaje tylko cel + minimum bieżących danych + pytanie, którego standardowa procedura nie potrafi rozstrzygnąć.
Luna NIE dostaje całego historycznego DOM, nie musi ręcznie grepować, nie generuje kolejnej pętli get_page -> JS -> logs przy istniejącej zaufanej recepturze. Może zażądać zawężonego lub rozszerzonego wyniku na podstawie capture_id.
Luna uczy nowej metody po pojawieniu się nieznanej witryny, zmianie widoku lub braku odpowiedniego przepisu. Nowa metoda najpierw jest candidate, a po pozytywnych danych staje się verified.
### 4.3 OpenFill
Sam wybiera zadeklarowaną procedurę po action_type + host + view + kontekst konta; utrzymuje operation_id, postęp, wynik i próbę weryfikacji. Udostępnia jednoznaczne statusy oraz dowód źródłowy.
Przed wysłaniem lub zmianą stanu zapisuje zamiar do dziennika. Po operacji zapisuje observed outcome; nieznany skutek pozostaje unknown/uncertain do pogodzenia z witryną, bez ślepego ponowienia.
Zwykły pojedynczy get_page lub act nadal możliwy, gdy procedura nie pasuje.

## 5. Narzędzia: co instalować i jak ich używać
### 5.1 Zestaw podstawowy
P0: ripgrep (rg.exe), jq.exe, sqlite3.exe, dostęp do GNU grep/sed/awk z jednoznacznej lokalizacji Git for Windows, Python 3.12 oraz PowerShell. Dla rg/jq/sqlite3 utrzymać zweryfikowane portable binaria w wersjonowanym katalogu narzędzi OpenFill na PBOX1, nie polegać wyłącznie na PATH.
- rg: przeszukiwanie literalne i regex w txt, NDJSON, logach i wyciągach; szybkie wybieranie rekordów i kontekstu, także --json do maszynowego raportu trafień.
- jq: przeszukiwanie i projekcja JSON/NDJSON, selekcja pól, liczników, składanie lekkich raportów; bez bezmyślnego --slurp przy dużych plikach.
- sqlite3 CLI + FTS5: wyszukiwanie kontaktów, wiadomości, procedur, zdarzeń, powiązań i historii według kluczy oraz tekstu; trwały indeks i transakcje, nie zamiennik rg dla jednorazowych plików.
- grep/sed/awk: proste filtry i transformacje linii, istniejące narzędzia Git Bash.
- Python: awaryjna transformacja, gdy nie da się tego zrobić krótko przez jq/rg/sqlite. Nie budować w Pythonie alternatywnego silnika procedur.
- CSS/DOM/CDP: odczyt struktury strony, pól i uchwytów interakcyjnych; grep nie zastępuje poprawnej interpretacji HTML i nie służy do klikania.
Opcjonalnie dopiero na podstawie profilowania: fd do szukania plików, Miller do tabel CSV/TSV. Nie dodawać DuckDB, Elasticsearch, serwera wyszukiwarki, n8n ani zewnętrznej bazy, jeśli nie ma konkretnego problemu nierozwiązywalnego powyższymi narzędziami.
### 5.2 Zasady wykonania
Narzędzia w trybie absolutnych ścieżek i rozdzielonych argumentów, ze wspólnym kontraktem: input_path, operation, output_path, status, exit_code, row_count, sha256, elapsed_ms, stderr_path, truncated/complete.
Dla rg exit 1 = zero dopasowań; nie zamieniać go w błąd programu. Błąd parsowania JSON, brak pliku, brak programu, timeout i brak match mają różne stany.
Duże stdout/stderr tylko na dysk, model dostaje liczniki, maksymalnie kilka istotnych rekordów i ścieżkę/id do selektywnego odczytu. Komenda lokalna nie musi być wywołaniem modelu.
Nie wysyłać przez MCP wielomegabajtowych argumentów i wyników. Niezmienione pliki wejściowe i wyniki pośrednie mają SHA256; nowe filtry tworzą nowe pliki.
Zamiast ogólnego terminal_exec dla typowego scenariusza używać opisanego, typowanego zamiaru filter/capture/query. Surowy terminal pozostaje dla niestandardowej diagnostyki.

## 6. Trzy zakresy pamięci i właściwe miejsca przechowywania
### 6.1 Pamięć krótkotrwała w procesie OpenFill
Aktualna karta, sesja, view_signature, fingerprint istotnych węzłów, capture_id, podzbiór rekordów, plan bieżącej partii, kolejka działań. Przyspiesza kilkukrotne użycie tego samego widoku. Musi mieć kopię istotnego stanu zadania na dysku, bo restart nie może oznaczać ponownej wysyłki.
### 6.2 Pamięć przeglądarki WebView2
Pozostawić w istniejącym profilu strony: cookies, stan logowania, IndexedDB/localStorage danej witryny i cache Chromium. Nie zapisywać tam podstawowej bazy kontaktów, procedur ani dziennika OpenFill; są to magazyny zarządzane przez witrynę, powiązane z origin i narażone na wyczyszczenie profilu. Dane DOM i wyciągi mogą zostać na krótko w pamięci procesu przeglądarki do jednorazowej lokalnej ekstrakcji.
### 6.3 Pamięć trwała na dysku PBOX1
Katalogi logiczne w istniejącym AppPaths.Root, osobno stable/dev:
- procedures/: deklaratywne wersjonowane procedury, centralne źródło utrzymania definicji dla wszystkich zadań (w SOURCE); w LIVE tylko wydana kopia procedur.
- recipes/: zweryfikowane receptury odczytów i selektorów per host/route/view/operation, stan i skuteczność; migracja istniejących CaptureRecipeStore.
- memory/operational.db: lokalny SQLite, indeks kontaktów, kont, rozmów, wiadomości, operacji, idempotencji, odniesień do plików. WAL na lokalnym dysku PBOX1, nie na Google Drive/H:.
- memory/events/YYYY-MM/*.ndjson: append-only zdarzenia semantyczne; zachować integrację z istniejącym HistoryStore i logami run, a nie drugi niezależny dziennik tej samej treści. Możliwy logiczny strumień w istniejącym systemie logów.
- memory/blobs/: pełne potrzebne treści/payloady i eksporty z hashami, bez wielokrotnych kopii.
- captures/<task>/<capture_id>/: niezmienione surowe zrzuty i wyniki lokalnych filtrów oraz manifest.
- tasks/<task>/state.json: trwały checkpoint bieżącej procedury, operation_batch_id, offset, status i dowody.
- cache/: odtwarzalne dane tymczasowe i index snapshots.
Ważne: proponowane foldery oznaczają LOGICZNĄ strukturę w AppPaths, nie drugi projekt i nie automatyczne przeniesienie dotychczasowych danych. SITE NOTES pozostają oddzielne jako luźne wiadomości o witrynie. HistoryStore pozostaje czytelną historią zadania.

## 7. Model danych: co trzeba pamiętać
Nie zapisywać danych jako „wiadomość dla pana X” bez trwałego identyfikatora. Każdy rekord ma origin, account_id, source, observed_at, schema_version, confidence, provenance i aktualny status.
1. Account: service, browser_profile, provider_account_id albo jednoznaczny odcisk zalogowanej sesji, display_name, identity_verified_at, auth_status.
2. Person/Entity: internal_entity_id, aliasy imion/nazw, firma, źródło powiązania; rozdzielone od kont kanałowych. Zbieżne imię to NIE dowód tej samej osoby.
3. ChannelIdentity: service, account_id, provider_user_id/JID/URL/phone, profile_url, canonical_url, last_verified, source. Osobna identity dla LinkedIn i WhatsApp.
4. Conversation: provider_thread_id lub canonical_thread_url, account_id, uczestnicy, last_message_id, last_observed_at, list_view_state, history_scope, unread, completeness.
5. Message: provider_message_id, thread_id, author, direction, timestamp, content_hash, body_file_ref albo kompaktowy indeks tekstowy, delivery_state, observed_at, source_capture_id. Jeśli brak ID, deduplikacja kombinacją thread/account/direction/time/body_hash z oznaczeniem niepewności.
6. OperationBatch: procedure_id+version, requester/chat/task_id, target_count, payload_hash, created_at, status, intended_scope, counters.
7. OperationItem: operation_id, batch_id, target_identity_id, channel, action, payload_hash, idempotency_key, status (planned/prepared/sent_unverified/verified/failed/uncertain/skipped), attempts, source/evidence/last_update.
8. Capture: source_file, SHA256, URL, route, account_id, view_type, scope (visible/loaded/all), generation, completeness, observed_at, selector/recipe_version, record_count.
9. ProcedureDefinition i RecipeDefinition: patrz rozdział 8; oddzielić logikę zadania od selektorów aktualnego DOM.
10. ProcedureRun: program counter, target_cursor, last_verified_item, input_hash, resume_token, pending_uncertain_items i ostatni znany stan konta.
Indeksy: unique(account_id, provider_identity_id), unique(account_id, provider_thread_id), unique(batch_id, target, action, payload_hash), wyszukiwanie po URL/aliasach, FTS5 dla treści pozwalających wyszukać rozmowę. Relacje cross-channel wyłącznie na podstawie jawnego powiązania lub wiarygodnego ID.
Przy zapisie zmian faktów nie kasować poprzedniej obserwacji bez śladu. Obecny display_name może się zmienić, ale profil URL i historia powiązania pozostają. Zachować historię zleceń i działań niezależnie od czatu, który je uruchomił.

## 8. Receptura, procedura i sekwencja: ścisłe rozdzielenie
### Receptura = „jak wyczytać ten widok”
Key: host + route/view_signature + operation_type + scope + wersja. Zawiera CSS root, ścieżkę odczytu, plan jq/rg, schemat wyjściowych rekordów, selektory elementów akcji, marker sesji, marker kompletności, confidence i datę ostatniego potwierdzenia.
Jest techniczna, bez danych osobowych i treści wiadomości. Przykład: linkedin.com /messaging/ conversation_list -> read visible rows -> extract profile links and preview.
### Procedura = „jak osiągnąć efekt”
Stałe definition_id, version, intent, provider, input_schema, preconditions, stages, used_recipes, action_bindings, verification_rule, error_routes, resume_rule, output_schema, persistence_policy, owner, confidence.
Etapy nie są dowolnym skryptem programistycznym. To krótka deklaracja operacji z istniejącego zestawu: resolve, ensure_auth, navigate_known_url, observe, query_stored, select_targets, execute_action, verify, record, report, escalate_to_luna. Bez uniwersalnego interpretera pętli i bez skomplikowanego DSL.
Przykład: linkedin.send_messages.batch wywołuje linkedin.ensure_auth, linkedin.resolve_recipients, linkedin.send_one i linkedin.verify_one dla kolejnych celów, ale Luna nie dostaje osobnej tury na każdy krok.
### Proces/Workflow = „co użytkownik chce zrobić w szerszej sprawie”
Opcjonalna kompozycja procedur, np. znajdź 20 kontaktów -> wybierz 10 -> przygotuj treść -> wyślij -> raportuj. Dojdzie później, po sprawdzeniu katalogu prostych procedur. Nie budować od razu ogólnego orkiestratora workflow.

## 9. Katalog procedur: miejsce docelowe
W SOURCE logiczny katalog:
- procedures/core/: ensure_auth, resolve_identity, capture_and_query, batch_checkpoint, verify_external_effect, report_status.
- procedures/sites/whatsapp/: list_previews, open_by_id, read_loaded, read_recent, read_all_previews, send_one, send_batch, read_batch.
- procedures/sites/linkedin/: ensure_auth, resolve_profile, resolve_thread, list_threads, send_one, send_batch, invite_one, invite_batch, read_inbox.
- procedures/sites/airbnb/: resolve_listing, read_calendar, update_prices_range, verify_prices_range.
- procedures/sites/google/: dopiero po rozdzieleniu intencji na GBP, Gmail, YouTube itd.
- procedures/sites/facebook/: page_list, message_thread, post_or_edit_page dopiero po rozpoznaniu powtarzalności.
- procedures/registry.json: wersje, aliasy intent, profil domeny, wymagane argumenty, dojrzałość i dostępne procedury.
- recipes/sites/<host>/: oddzielnie selectors + filter plans, nadzorowane przez CaptureRecipeStore. Nie mieszaj ich z SiteNotes.
Definicje preferować w JSON, zgodne z jq i stabilnym schema_version, zamiast mieszaniny opisów Markdown, luźnych promptów i własnego YAML DSL. Dokumentacja opisowa może leżeć obok, ale JEDYNĄ definicją wykonywalnej procedury jest wersjonowany manifest.
Procedury napisane ręcznie lub zaproponowane przez Lunę są candidate; procedury używane automatycznie muszą być zatwierdzone i kompletne co do zakresu i efektu. Nie uczyć automatycznie „wysyłania do wszystkich” na podstawie jednego skutecznego kliknięcia.

## 10. Przebieg standardowej procedury
Krok 1. Przyjmij INTENT i wygeneruj stały batch ID/idempotency key, bez utraty requester/chat/task_id.
Krok 2. Zapisz plan w dysku: pełny zbiór celów, payload, kolejność, status planned. Sprawdź wcześniejsze OperationItem dla identycznych celów i treści z bieżącego zlecenia.
Krok 3. Resolve account: na aktywnej karcie odczytaj minimalistyczny znacznik zalogowanej tożsamości. Nie polegaj wyłącznie na URL ani na tytule strony. Jeśli brak sesji, uruchom procedurę auth; jeżeli wymaga challenge/MFA, oddaj waiting_for_user i zapisz punkt kontynuacji.
Krok 4. Resolve target: najpierw trwała pamięć (account+service+provider ID/URL). Dopiero przy braku lub niespójności pobierz z witryny. Nie wyszukuj ponownie już znanego profilu.
Krok 5. Observe/capture: tylko wymagany widok; receptura dobiera CSS/DOM/CDP i filtr. Raw zapisany na dysku, indeks znormalizowanych rekordów do SQLite.
Krok 6. Lokalna analiza kompletu celów, przygotowanie przedziałów/batcha, kontrola zakresu/wirtualizacji, wyeliminowanie zbędnych powtórek.
Krok 7. Wykonaj działanie lokalnie w istniejącej karcie. Bez cyklu Luny dla każdego targetu. Przy liście 10 celów utrzymuj cursor i status każdego elementu.
Krok 8. Weryfikuj skutek zgodnie ze specyfiką serwisu. Po bloku może być jedno zbiorcze odczytanie, ale nie zastępuje dowodu indywidualnego skutku. Jeżeli UI gwarantuje tylko potwierdzenie jednostkowe, można zrobić jednostkowy lokalny odczyt bez modelu.
Krok 9. Każdy wynik i jego źródło zapisuj po otrzymaniu. Brak dowodu to uncertain, nie success ani automatyczna powtórka.
Krok 10. Raport w postaci X verified, Y uncertain, Z failed, W skipped; ścieżka do wyników, operation IDs i ewentualne pytanie do Luny.

## 11. Szczegółowy przykład: wysłanie 10 wiadomości LinkedIn
Przykładowy INPUT: source_account, 10 docelowych profile_url albo provider_id, dziesięć treści lub jedna treść szablonu, idempotency_scope, request_id, do_send=true.
### Faza A. Przygotowanie, bez wywołania Luny
1. Rejestruj zlecenie i dziesięć pozycji. Normalizuj canonical LinkedIn URL, usuwaj wyłącznie zbędne parametry śledzące. Identyfikator profilu nie może być dedukowany tylko ze wspólnego imienia.
2. Załaduj istniejące mapowania profile -> account/thread URL z operational.db. Dla znanych 10 kontaktów nie wywołuj wyszukiwarki LinkedIn.
3. Jeden zwięzły check aktualnego zalogowania i tożsamości konta w istniejącej karcie. Jeżeli karta otwarta na innym widoku, sprawdzenie może nastąpić na pierwszym docelowym widoku: nie wymuszaj dodatkowego wejścia na homepage i dwóch nawigacji.
4. Gdy login nie jest potwierdzony, wywołaj linkedin.ensure_auth. Procedura odróżnia already_logged_in, login_needed, challenge, error i account_mismatch. Sesja innego konta nie jest domyślnie właściwa.
5. Dla brakujących w pamięci profile/thread URL pobierz minimalną stronę/profil i zapisz wynik JEDEN RAZ. Jeśli 10 URL profili jest już w pamięci, brak fazy przeszukiwania kontaktów.
### Faza B. Sekwencyjne wykonanie skutków zewnętrznych
6. Dla każdego targetu użyj zapamiętanego thread URL albo przejdź z profilu do wątku. Utrzymuj stały expected_identity, expected_thread i payload_hash.
7. Przed wysłaniem rejestruj status prepared. Jeśli OperationItem jest already verified, pomiń. Jeśli uncertain, najpierw sprawdź rozmowę, nie wyślij ponownie.
8. Sprawdź, że aktywny rozmówca odpowiada oczekiwanej tożsamości, a composer jest właściwy; użyj receptury linkedin.message_compose.
9. Wpisz i wyślij treść przez istniejące akcje UI. Nie generuj od zera skryptu JS dla każdego celu.
10. Potwierdź rezultat: identyfikator/tekst/timestamp wysłanej wiadomości w właściwym wątku, o ile widoczne, i zapisz evidence. Sam przycisk send nie wystarcza. Jeśli serwis nie ujawnia jednoznacznego ID, stosuj source_thread+payload_hash+czas z oznaczeniem poziomu pewności.
11. Utrwal verified/uncertain/failed i identyfikator wątku po KAŻDYM targetcie, by restart po 6/10 nie zaczynał od 1/10.
12. Zmiana konta, wylogowanie albo challenge: zapisz postęp, uruchom wspólną gałąź auth/waiting, wróć do następnej nieukończonej pozycji po ponownej walidacji.
### Faza C. Wynik
13. Ukończ serię: raport 10/10 lub np. 7 verified, 2 uncertain, 1 failed, lista linków do wątków z odpowiadającymi operation_id.
14. Kolejne zlecenie dotyczące tych samych profili rozpoczyna od pamięci linków i historii kontaktu. Nie od otwierania listy wysłanych wiadomości dla wszystkich.
15. Ponowienie identycznego batcha z tym samym identyfikatorem nie powiela verified. Identyczne słowa wysłane później jako NOWE, jawne zlecenie mają nowy request_id i stanowią nową operację, nie są blokowane globalnie na zawsze.
Korekta pierwotnego pomysłu „jeden odczyt strony głównej”: lepiej minimalny dowód tożsamości z aktualnej karty lub pierwszego potrzebnego widoku, bez narzucania homepage. Największe oszczędności pochodzą z ominięcia rozpoznawania celów i pętli modelowych.

## 12. Procedura login: tylko wspólny szkielet plus adapter
ensure_auth(service, expected_account, task_tab):
A. Sprawdź istniejący profil WebView2 i faktyczny marker zalogowanej tożsamości.
B. Jeśli prawidłowy, zwróć verified i nie odwiedzaj login page.
C. Jeśli login required, użyj obecnych mechanizmów uwierzytelnienia przewidzianych w aplikacji i konkretnej witrynie. Nigdy nie zakładaj, że zapisany credential oznacza zalogowanie.
D. MFA/challenge/manual verification -> waiting_for_user, checkpoint i wznowienie z tego samego kontekstu, bez wielokrotnego submit.
E. Po login sprawdź account_id. Zmiana konta musi zmienić zakres pamięci i wykluczyć użycie cudzych thread IDs.
F. Zwróć jeden status z konkretną przyczyną: authenticated, authentication_required, challenge_pending, account_mismatch, auth_failed.
Procedury strony są wariantami wspólnej operacji, a nie osobnym dużym silnikiem logowania.

## 13. WhatsApp: wykorzystać istniejący działający moduł
- whatsapp.list_previews: przy wejściu zwięzły odczyt wszystkich wyrenderowanych wierszy, bez obrazu; do pamięci lista identity, last preview, observation_scope=visible, observed_at.
- whatsapp.read_batch: dla numerów/JID bezpośrednie open, jeden read loaded messages na unikalny target. Deduplikacja wejścia; 10 numerów nie może dawać 69 wejść bez wykazanego powodu.
- whatsapp.send_batch: otwórz direct URL, sprawdź header i właściwego odbiorcę, wyślij, jeden lokalny odczyt skutku; utrwal outgoing message i status. Dalsza seria może użyć już poznanego JID.
- whatsapp.read_all_previews: tylko wyraźnie żądane rozszerzenie całej wirtualizowanej listy, ze statusem rzeczywistej kompletności.
- whatsapp.resolve_by_name: najpierw bieżąca pamięć + wyrenderowane preview; jeżeli nazwa niejednoznaczna, nie zgaduj numeru. Pełna lista dopiero gdy rzeczywiście potrzebna.
Nie zastępować WhatsAppBrowser nowym systemem C#. Procedura jedynie komponuje te istniejące funkcje i zapisuje stan. Nie wprowadzać Luny Mini.

## 14. Airbnb, Google, Facebook i inne rodziny operacji
Airbnb: listing_id+account -> calendar range -> jednorazowy structured capture cen -> lokalne obliczenie zmian dat -> zapis planu i starej/nowej ceny -> wykonanie skutków -> rekonsyliacja każdego dnia -> raport. Ceny są wartościami z konkretnego konta, waluty, listing_id i daty; historia zmian istotniejsza niż pełne kopie HTML.
Google: najpierw rozbić szeroką grupę z logów na Gmail, Business Profile, YouTube Studio i inne. Nie projektować jednej „procedury Google”. Wspólna jest biblioteka primitives i pamięć tożsamości, ale schema obiektów i wykonanie są różne.
Facebook/Meta: rozbić na pages, messages, ad/account management i operacje administracyjne. Procedury „wyszukaj/odczytaj/wykonaj/zweryfikuj” dopiero dla istotnych podtypów.
W nowej witrynie: standardowe observe_view/query_capture -> Luna analizuje niewiadome -> nowa receptura candidate -> zarejestrowanie skutku -> ewentualnie propozycja procedury. Nie generować osobnej klasy parsera dla każdej witryny.

## 15. Kiedy pamiętać, kiedy odświeżać, kiedy nie ufać pamięci
Dziel dane na:
A. Fakty stabilne: provider ID, historia wykonanych działań, task/requester, source account. Utrwalać długoterminowo; można aktualizować alias i URL, ale nie usuwać dowodów wysyłek.
B. Fakty półtrwałe: profil URL, powiązanie thread URL, podpisy przycisków, selektory i receptury. Trzymać i używać ponownie, walidować przy pierwszej operacji po zmianie struktury.
C. Dane zmienne: unread_count, ostatnie preview, widoczne wiersze, stan composer, bieżąca cena, autoryzacja. Zapis jako obserwacja z datą i zakresem; przy skutkowej operacji potwierdzać stan na żywo.
D. Surowe DOM: zachować jako materiał referencyjny i diagnostyczny według limitów powierzchni dysku, ale nie traktować starych zrzutów jako obecnego widoku ani koniecznie jako trwałej pamięci kontaktu.
Zamiast „im więcej pełnych kopii tym lepiej”: mieć pełną, bezstratną historię ważnych DZIAŁAŃ, a dużym zrzutom dać budżet, indeks i deduplikację treści po hashu. Sprzątanie surowego capture nie usuwa wyniku działania ani tożsamości.
Przy sprzeczności z bieżącą stroną nie nadpisywać automatycznie niewyjaśnionej poprzedniej obserwacji. Przechowywać oba źródła i status conflict/stale.
Fulltext: SQLite FTS5 wyszukuje indeksowane ważne rekordy, rg przeszukuje niezaindeksowane pełne pliki. Dla kontaktu najpierw ścisły provider ID, później normalizowany URL, potem alias/FTS; pełna analiza DOM jako ostatnia.
Unikać przecieków między kontami: wszystkie identity, rozmowy i skutki mają account_id. Klient MCP ma uprawnienie do własnego tasku i zgodnego requester/chat; zapis globalnej pamięci nie znosi tej izolacji.

## 16. Operacje skutkowe, niepewność, wznowienie
Statusy przedmiotowe: planned -> prepared -> attempted -> verified / uncertain / failed / skipped. Stan attempted opisuje moment naciśnięcia send lub zapisania formularza, nie jest dowodem ukończenia.
Każda pozycja ma własny operation_id i idempotency_key = request+account+target+action+payload_hash. Dla specjalnie powtórzonej kampanii nowy request_id.
Crash po kliknięciu, przed potwierdzeniem: stan uncertain. Po restarcie RECONCILE: odczyt właściwej rozmowy/rekordu z witryny, porównanie dowodów, dopiero potem zmiana na verified/failed; gdy niejednoznaczne, oczekuje rozstrzygnięcia. Brak automatycznego ponownego wysłania.
Zapis dziennika ma poprzedzać nieodwracalny efekt. Dane po stronie witryny nie są lokalną transakcją SQLite; exactly once dla nieudostępniających kluczy idempotency API nie da się obiecać. Można osiągnąć kontrolę ponowień, wykrywanie niepewności i rekonsyliację.
Postęp kampanii jest trwałym stanem 0..N, z wynikiem każdej pozycji. Karty mogą być odtworzone z URL, ale to nie zastępuje odczytu skutku z serwisu.
Nie dodawać sztucznych opóźnień, globalnych limitów kliknięć ani pętli diagnostycznych „na zapas”. Jeśli widok jest niezmieniony i wynik nie przyrasta, przejdź do konkretnej alternatywy, nie raportuj sukcesu.

## 17. Obserwacja strony bez przepalania tokenów
Szybka ścieżka: aktualny URL/route, marker auth, fingerprint istotnego root, render-state, ostatni capture_id. Jeżeli świeży rekord w cache dotyczy tego samego konta/widoku/zakresu i nie było zmiany, query_capture czyta wyłącznie zapisany plik.
Na zmianę nawigacji, account_id, właściwego regionu DOM, paginacji lub zakresu virtualized: nowy zrzut. Zmiana zegara, dekoracji CSS, animacji i nieważnych elementów nie powinna unieważniać całej reprezentacji.
Dla wirtualizowanych list oddzielnie scope=visible / loaded / traversed_all; full=true tylko przy faktycznym osiągnięciu końca i określonej metodzie kompletności.
Preferować semantyczny DOM, selektory CSS, a11y/accessibility i CDP, jeżeli potrzebne; pełny surowy HTML nie jest domyślną metodą wyszukiwania nazw, wątków i formularzy.
Sukces działania można weryfikować przez bezpośredni lokalny znacznik lub zbiorczy odczyt bez pytania modelu o interpretację oczywistego stanu. Wątpliwości trafiają do Luny z krótkimi dowodami.

## 18. Kontrakty MCP dla zlecającego, bez mnożenia narzędzi
Na zewnątrz zachować istniejące proste openfill_* i dodać najwyżej niewielką liczbę logicznych punktów:
1. openfill_procedures: lista dostępnych intentów, wymagane pola, zakresy, confidence; model może odkryć co jest automatyzowane.
2. openfill_execute: jeden intent + args + request_id; router wybiera procedurę lub standardowe wykonanie Luny. Dla 10 osób jeden batch i jeden task.
3. openfill_memory_query: rozwiąż tożsamość, znajdź wątki, kontakt/ostatnie akcje, zwróć mały wynik i referencje. Nie inicjuj przeglądarki, jeśli informacja historyczna wystarcza.
4. openfill_capture_query: przeszukaj istniejący capture_id lokalnymi narzędziami, bez ponownego DOM i modelu.
5. openfill_operation_status: postęp/rezultaty/błędy i wznowienie znanego batcha, zgodnie z aktualnymi mechanizmami task_id i requester/chat.
Nazwy są PROPOZYCJAMI architektonicznymi; agent ma utrzymać zgodność z istniejącymi kontraktami MCP, nie tworzyć zduplikowanego osobnego serwera i nie rozbudowywać lekkiego openfill_info.
Dla każdego wyniku: status, resolved_account, procedure_id/version albo mode=luna, items_total/verified/uncertain/failed, elapsed, output_ref, evidence_ref, reason_code. Nie pakować pełnych rekordów do standardowej odpowiedzi MCP.

## 19. Wykrywanie najczęstszych czynności i rozwój katalogu
Faza analityczna czyta historyczne run NDJSON i HistoryStore bez uruchamiania stron. Odpowiedź nie może opierać się tylko na domenie: klasyfikować task_intent według polecenia i sekwencji narzędzi.
Tworzyć agregaty: provider, account scope, intent, frequency, liczba targetów, koszt/czas, liczba get_page/run_js/act/search_logs, powtórzenia tej samej operacji, rezultat końcowy i liczba powrotów na tę samą stronę.
Priorytet do budowy procedury = częstotliwość × możliwa redukcja model calls × powtarzalność struktury × znaczenie biznesowe. Nie mnożyć procedur dla jednorazowych, niestandardowych operacji.
Raport kandydatów np. „LinkedIn.send_batch”, „WhatsApp.read_batch”, „GBP.update_hours”, z liczbami i reprezentatywnymi anonimowymi sekwencjami działań. Na razie z logów DOWIEDZIONO ranking GRUP, nie dokładnych intencji; agent ma policzyć ranking intencji przed rozszerzeniem poza LinkedIn/WhatsApp.
Luna może oznaczać zakończony przebieg jako procedure_candidate, ale nie dopisywać samoczynnie dużych instrukcji do system prompt i nie ogłaszać verified po samym parse/capture.
Co jakiś czas odświeżać ścieżki i selektory na podstawie rzeczywistych zmian i wyników. Zachować wersje historyczne zamiast nadpisywania bez śladu.

## 20. Co wdrożyć w jakiej kolejności: handoff dla agenta
### P0. Fundament narzędzi terminalowych: wdrożyć najpierw
P0.1. Zapewnić gotowe portable rg.exe, jq.exe, sqlite3.exe oraz jednoznaczny dostęp do grep/sed/awk/Python, ze sprawdzalnym stanem tool_available. Nie pisać zamienników w C#.
P0.2. Poprawić TerminalProcessService jako wspólny, stabilny transport input-file -> executable -> output-file -> manifest, z przekazywaniem argumentów jako listy, rozróżnieniem no_match i rzeczywistego błędu.
P0.3. Dodać typed filter/query na już zapisanych plikach, bez nowego odczytu DOM; zachować raw i provenance.
P0.4. Wykorzystywać automatyczny capture+filter w zwykłym AgentRunner dla tabel/list zamiast ręcznego terminal_exec przez Lunę. Szybka ścieżka get_page/act dla formularza pozostaje.
### P1. Fundament trwałej pamięci
P1.1. Utworzyć lokalny operacyjny indeks SQLite z rozróżnieniem account/identity/conversation/message/action, FTS5 i referencjami do plików; import danych tylko w zakresie wiarygodnych istniejących zdarzeń, bez wymyślania provider IDs.
P1.2. Rozszerzyć istniejący event logging / HistoryStore o semantyczne rezultaty operation item, trwałe batch ID i punkty wznowienia, bez dublowania gigantycznych treści.
P1.3. Oddzielić trwałe identity i action history od zmiennych DOM cache i recipe store; zgodnie z istniejącym stable/dev.
### P2. Definicje i wykonywanie procedur
P2.1. Wprowadzić deklaratywny registry JSON, schemat procedury i mały executor korzystający z istniejącego ToolRegistry. Nie tworzyć frameworka programowania ani osobnej Luny.
P2.2. Dodać core ensure_auth, resolve_identity, capture_query, execute_and_verify, batch_progress oraz resume/reconcile.
P2.3. Opisać kontrakt zlecającego i Luny; wybór procedury po intencie oraz serwerowe wymuszenie zapisów i statusów.
### P3. Najczęściej używane konkretne procedury
P3.1. WhatsApp: odczyt podglądów, batch read, batch send, resolve by ID, wykorzystanie już istniejących funkcji.
P3.2. LinkedIn: ensure auth, resolve profile/thread, send batch, invite batch, inbox batch; szczególnie reuse linków i historii wcześniejszych wysyłek.
P3.3. Airbnb: zbiorczy odczyt i edycja cen w zakresie dat; nie zgubić starej ceny ani stanu pojedynczego dnia.
P3.4. Google/Meta: dopiero po rozdzieleniu rzeczywistych intencji i potwierdzeniu dużej powtarzalności.
### P4. Uczenie i rozwój
P4.1. Analiza historycznych intencji i kosztu operacji, lista kandydatów.
P4.2. Automatyczna sugestia receptur i procedur, wersjonowanie candidate -> verified/suspended, na podstawie wiarygodnego wyniku całego zadania.
P4.3. Krótki katalog w MCP pozwalający klientom odkrywać procedury, bez silniejszych wymagań wobec ich własnych promptów.

## 21. Rozgraniczenia i zakazy nadmiernej rozbudowy
NIE tworzyć: nowej wyszukiwarki tekstu; nowego parsera JSON; własnego silnika SQL; Luny Mini; nowej przeglądarki; drugiego modelu do samego klikania; globalnej monolitycznej procedury „wszystkie witryny”; wielkiego parsera HTML regexem; dużej warstwy promptów z całą pamięcią; synchronizacji SQLite-WAL bezpośrednio na Google Drive; automatycznych ponowień uncertain send; drugiego niezależnego logu tego samego zdarzenia; samoczynnego generowania i wdrażania skryptów JS z każdej wizyty.
Nie utożsamiać: sukcesu get_page z sukcesem zadania, logowania z samą obecnością URL, otwartego profilu z potwierdzoną tożsamością, widocznej listy z pełną historią, capture recipe z procedurą, danych cache z trwałym źródłem prawdy.
Granica ma być wąska: własny kod tylko koordynuje gotowe narzędzia, stan i minimalną logikę specyficzną dla strony, której nie zapewni grep/jq/SQLite.

## 22. Uwagi krytyczne do wcześniejszego planu i decyzje do utrzymania
Poprawić wcześniejszą kolejność: samo instalowanie rg/jq bez wymuszenia domyślnej ścieżki nie zmniejszy liczby tur Luny. Po P0.1 musi nastąpić P0.2-P0.4.
Rozdzielić pamięć technicznych capture recipes od historii osób i działań. Receptura bez historii wysłanych wiadomości nie rozwiązuje problemu ponownego poszukiwania tych samych wątków.
Nie wymuszać homepage jako oddzielnej nawigacji w każdej procedurze. Prawidłowy marker konta w aktualnej karcie daje ten sam cel taniej.
Nie narzucać „jedno sprawdzenie po całej serii”, jeśli jest potrzebny dowód jednostkowy. Sprawdzenie lokalne nie jest nową turą Luny, więc może być tak częste, jak wymaga poprawność.
Nie przechowywać wszystkich wielkich HTML bez ograniczeń: indeksuj zdarzenia, identyfikatory i potrzebne treści trwale, a surowe capture traktuj jak materializowany, odtwarzalny materiał diagnostyczny.
Nie planować login automation jako jednego uniwersalnego „kliknij i wyślij”: witryny mają inne markery kont, challenge i sesje.
Nie robić przedwcześnie kilkudziesięciu procedur domenowych. Najpierw rozliczalny executor i trwała pamięć, potem dwa wzorce z dużymi zyskami: LinkedIn batch i WhatsApp batch.

## 23. Mapa źródeł projektowych
- docs/OPENFILL_GREP_FILTER_PIPELINE_IMPLEMENTATION_PLAN_20261031.md, szczegółowy poprzedni plan filtrów i CLI.
- docs/OPENFILL_TOKEN_WASTE_AUDIT_20261031.md oraz docs/OPENFILL_TOKEN_AUDIT_DATA_20261031.json, statystyki i konkretne przebiegi.
- docs/OPENFILL_WHATSAPP_SPEC_2026-10-29.md, ustalenie jedna Luna i dedykowane operacje.
- docs/OPENFILL_SOURCE_FILTER_CAPTURE_ARCHITECTURE_20261030.md, zapis aktualnej architektury.
- docs/OPENFILL_ZASADY_AKTUALIZACJI_I_LOKALIZACJI__2026-10-29.md, istniejące granice SOURCE/STAGING/LIVE.
- SOURCE: Agent/TerminalProcessService, PageCaptureService, CaptureRecipeStore, SiteNotesStore, AgentRunner, ToolRegistry; Hosting/HistoryStore; Browser/WhatsAppBrowser; Config/AppPaths; Mcp/McpTaskManager.
- Oficjalne materiały referencyjne: https://ripgrep.dev/docs/getting-started/ , https://jqlang.org/manual/ , https://sqlite.org/fts5.html , https://sqlite.org/wal.html , https://sqlite.org/json1.html , https://chromedevtools.github.io/devtools-protocol/tot/DOMSnapshot/ , https://learn.microsoft.com/en-us/microsoft-edge/webview2/how-to/chromium-devtools-protocol .
