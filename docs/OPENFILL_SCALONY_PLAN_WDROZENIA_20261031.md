# OpenFill: scalony plan wdrożenia filtrów, pamięci i procedur
## Plan wykonawczy v3.0, 31.10.2026, PBOX1

**AKTUALNA DECYZJA WŁAŚCICIELA, 2026-11-02:** Airbnb całkowicie wyłączone z backlogu. Nie projektować, nie programować, nie testować, nie wdrażać i nie uruchamiać operacji Airbnb. D03 poniżej i późniejsze checkpointy Airbnb są WYŁĄCZNIE ARCHIWUM. Pozostałe istniejące binaria/kod pozostają bez zmian.
Status: PLAN DLA CZATU PROGRAMISTY. Nie wykonano zmian w kodzie ani LIVE na podstawie tego dokumentu. Dokument opisuje architekturę, logikę, zakres i kolejność, nie projektuje testów ani kodu.

### 0. JEDNO ŹRÓDŁO PLANU I ZASADY PRACY
Ten dokument jest nadrzędnym planem wykonania tego zakresu i **scala** trzy źródła:
1. docs/OPENFILL_GREP_FILTER_PIPELINE_IMPLEMENTATION_PLAN_20261031.md: dotychczasowe etapy OF-FLT-00 do OF-FLT-10, techniczne szczegóły terminala, DOM, filtrów, MCP i release.
2. docs/OPENFILL_ARCHITEKTURA_PROCEDUR_PAMIECI_I_NARZEDZI_20261031.md: architektura narzędzi, receptur, procedur, pamięci i wysyłek.
3. Raport Deep Research, 31.10.2026: narzędzia CLI, SQLite/FTS5, procedury oraz pamięć. Jest materiałem analitycznym, nie ważniejszym niż sprawdzony SOURCE i specyfikacja właściciela.

Pozostałe istotne źródła: docs/OPENFILL_TOKEN_WASTE_AUDIT_20261031.md, docs/OPENFILL_WHATSAPP_SPEC_2026-10-29.md, docs/OPENFILL_SOURCE_FILTER_CAPTURE_ARCHITECTURE_20261030.md, docs/openfill_master_action_plan__v1.7__2026-10-17_1145.md i obowiązujące zasady aktualizacji projektu. Stare dokumenty zachować i oznaczyć jako dokumenty składowe. Jedno zadanie kodowe nie może być realizowane dwa razy pod starym i nowym identyfikatorem.

**Zasady dla programisty:**
- Na początku odczytaj aktualne SOURCE, STAGING, LIVE, backlog i równoległe prace. Stan w tym planie to ustalenia na 31.10, nie gwarancja aktualnego LIVE.
- Zacznij od rzeczywistych narzędzi terminalowych i pipeline file-first. Nie buduj rozbudowanej platformy procedur przed ich ukończeniem.
- Własny kod tylko do koordynacji, kontraktów, modelu stanu i koniecznych adapterów witryn. Nie pisz własnego grep, jq, SQLite, parsera HTML, nowej przeglądarki, drugiego MCP ani Luny Mini.
- Zapisuj postęp po każdym etapie, z SOURCE/STAGING/LIVE, wynikami i konkretnymi blokadami. Proces wydawniczy, testy, backup i rollback pozostają według dotychczasowych zasad projektu.
- Żadnych ręcznych git commit/push/reset, przemieszczeń LIVE z PBOX1 ani zmian Networking Radar.
- Jedna Luna rozstrzyga wyjątki. Zwykłe sekwencje i filtry wykonuje OpenFill lokalnie, bez nowej tury modelu na każdy klik.
- Duże dane: WebView2 -> pełny plik na lokalnym dysku -> deterministyczna lokalna filtracja -> mały wynik i referencja do pliku. MCP i model nie transportują całego DOM.

### 1. WERYFIKOWANY PUNKT WYJŚCIA
SOURCE: C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill.
Historyczny LIVE: C:\Users\pboru\OpenFill\app\OpenFill.exe, 1.58 w audycie z 31.10. Agent potwierdza wersję przed wdrożeniem.
Istniejące komponenty, które trzeba wykorzystać zamiast zastępować:
- Agent/TerminalProcessService__v0.1__2026-10-30.cs: wykonanie CLI i zapis stdout/stderr, obecnie niepełna obsługa narzędzi.
- Agent/PageCaptureService__v0.1__2026-10-30.cs: snapshot DOM/tekstu/elementów na dysk, SHA256, atomowe pliki.
- Agent/CaptureRecipeStore__v0.1__2026-10-30.cs: host, path, mode, selector, pattern, ranking technicznych i zadaniowych sukcesów.
- Agent/AgentRunner__v0.29__2026-10-29_2314.cs: Luna, prompty i PromptHistoryCompactor.
- Agent/ToolRegistry__v0.22__2026-10-29_2314.cs: page_capture, terminal_exec, capture_recipes, run_js save_to, get_page, act, act_many i WhatsApp tools.
- Agent/SiteNotesStore__v0.5__2026-10-14_2259.cs: notatki o domenach, nie pamięć kontaktów.
- Browser/WhatsAppBrowser__v0.1__2026-10-29_2310.cs: gotowe operacje read previews/open/send/list all; zachować.
- Hosting/HistoryStore__v0.4__2026-10-12_1750.cs: historia całych tasków jako NDJSON, nie historia każdej wysyłki.
- Config/AppPaths__v0.7__2026-10-14_2259.cs: istniejący podział data/stable i data/dev.
- Mcp/McpServer i McpTaskManager: istniejący kontrakt serwera, identyfikacja requester/chat/task.
- PageAuthenticationGate, FailedCallGuard i PromptHistoryCompactor: nie tworzyć ich odpowiedników.

PBOX1: Git Bash obecny, znane ścieżki GNU grep/awk w C:\Program Files\Git\usr\bin; Python 3.12 obecny. Podczas audytu rg, jq i sqlite3 nie znaleziono w PATH. To nie dowodzi, że brakuje ich we wszystkich możliwych katalogach, więc agent ustala realne ścieżki i stan procesu GUI. Zalogowany LinkedIn w izolowanym QA nie był potwierdzony. Nie traktować logowania jako działającego tylko dlatego, że poprawnie otwiera się /login.

Historia: 1477 zadań, 73329 model calls, ok. 1,499 mld input tokens, w tym 1,250 mld cached i ok. 40,71 USD zgłoszonych w logach. Grupy: LinkedIn 247 zadań/13687 model calls, Google 327/23008, Facebook 139/10801, WhatsApp 167/6582, Airbnb 25/1522. To nie jest ranking INTENCJI. Jeden przebieg WhatsApp otwierał 13 numerów 69 razy, z tego 56 powtórek. W innym 11 numerów otwarto 11 razy. Widać zatem, że problem nie wymaga napisania drugiego sterownika WhatsApp, lecz dobrej orkiestracji i pamięci.

### 2. ZATWIERDZONA KOLEJNOŚĆ WDROŻENIA
Wszystkie pakiety mają jeden identyfikator strumienia: OF-IMP-20261031. Ta kolejność zastępuje kolejność w dawnym OF-FLT, pozostawiając dawne szczegóły jako materiał wykonawczy.

| Pakiet | Priorytet | Zakres | Zależności | Wynik |
| --- | --- | --- | --- | --- |
| A00 | P0 | Audyt aktualnego kodu i uniknięcie dublowania | brak | aktualne statusy komponentów |
| A01 | P0 | Wszystkie terminalowe narzędzia wyszukiwania | A00 | rg, jq, SQLite/FTS5, GNU, Python gotowe |
| A02 | P0 | Uruchamianie CLI z wynikiem na dysku | A01 | file -> process -> file + manifest |
| A03 | P0 | Typowana filtracja tekst/JSON/NDJSON | A02 | query_capture bez nowego DOM |
| A04 | P0 | Capture i pełny manifest zakresu/źródła | A03 | DOM -> pliki i SHA256 |
| A05 | P0 | Receptury v2, fingerprint i cache | A04 | automatyczny reuse znanego widoku |
| A06 | P0 | Routing zwykłych odczytów i MCP file query | A03+A05 | model dostaje mały wynik |
| B01 | P1 | SQLite: trwała pamięć kont i kontaktów | A01 | indeks tożsamości, rozmów, historii |
| B02 | P1 | Ledger działań, idempotencja, resume | B01 | bez powtórek skutków po restarcie |
| B03 | P1 | Lookup pamięci, świeżość i retencja | B01+A05 | pamięć przed ponownym szukaniem |
| C01 | P1 | Katalog procedur i kontrakt definicji | A06+B01 | JSON registry i procedury |
| C02 | P1 | Router intencji i wykonawca sekwencji | C01+B02 | jedna kampania jako jedno zadanie |
| C03 | P1 | Procedury bazowe auth/resolve/verify/resume | C02+B03 | wspólna logika dla wszystkich witryn |
| D01 | P1 | WhatsApp: batch read i batch send | C03 | bez przepisywania WhatsAppBrowser |
| D02 | P1 | LinkedIn: znane profile i wysyłka wsadowa | C03 | 10 adresatów, 10 wyników |
<!-- ARCHIWUM: D03 Airbnb ANULOWANE przez właściciela 2026-11-02, poza backlogiem. -->
| E01 | P1 równolegle | Rozpoznanie rzeczywistych intencji w historii | A00 | ranking konkretnych działań |
| D04 | P2 | Google i Meta: procedury wybrane z E01 | E01+C03 | nie budować dziesiątek w ciemno |
| E02 | P2 | Uczenie i wersjonowanie procedur | C03 | candidate/verified/suspended |
| E03 | P2 | Metryki kosztu i skuteczności | A06+B02 | rezultat per operacja |

**Ścieżka do rozpoczęcia teraz: A00 -> A01 -> A02 -> A03 -> A04 -> A05 -> A06.** Prac nad C01-C03 nie wolno użyć jako powodu odłożenia A01-A06. E01 można wykonać równolegle od A00. B01 można projektować równolegle z A04, ale nie dublować istniejących danych.

### 3. A00: ROZPOZNANIE STANU
1. Ustal dokładny host, ścieżki, proces i wersje SOURCE/STAGING/LIVE.
2. Odczytaj aktywne źródła, nie przepisuj istniejących SiteNotesStore, WhatsAppBrowser, CaptureRecipeStore, HistoryStore, AgentRunner i PageCaptureService.
3. Sprawdź, czy inny czat programista nie wdrożył już pakietu; stan może się zmienić po opracowaniu planu.
4. Ustal, jak działa aktualny runtime profile, logi i zadania oraz gdzie będą zapisywane nowe artefakty.
5. Nadaj każdemu pakietowi status PLANNED / SOURCE IN PROGRESS / SOURCE DONE / STAGING / LIVE / BLOCKED, z dokładnym reason_code dla blokady.
Efekt: stan wyjściowy zidentyfikowany bez zgadywania i bez drugiego projektu implementacji.

### 4. A01: KOMPLET TERMINALOWY
1. Zapewnij gotowe binaria ripgrep (rg.exe), jq.exe i sqlite3.exe w przewidywalnych, wersjonowanych lokalizacjach na PBOX1.
2. Zapewnij bezpośrednie ścieżki do GNU grep, sed, awk, sort i uniq z istniejącego Git for Windows. Python 3.12 i PowerShell pozostają.
3. Rozpoznanie wersji i zdolności funkcjonalnych, w tym SQLite FTS5 i JSON1, ma odbywać się bez zależności od przypadkowego PATH terminala.
4. Dla każdej nazwy logicznej narzędzia określ absolutną ścieżkę, wersję, availability i konkretny błąd uruchomienia.
5. Korzystaj z gotowych programów, a nie z własnych zamienników regex, SQL i JSON w C#.
6. Instalacja jest na PBOX1. Nie przenoś LIVE ani nie zmieniaj konfiguracji niezwiązanych z tym zakresem.
**Wynik:** narzędzia gotowe do uruchomienia przez proces OpenFill, a nie tylko interaktywny shell programisty.
**Mapowanie starego planu:** OF-FLT-01.

### 5. A02: FILE-FIRST PROCESSING
1. Rozszerz TerminalProcessService zamiast tworzyć nowy terminal. Żądanie opisuje tool_name, input_path, output_path, operation, args, timeout i task context.
2. Argumenty przekazuj w bezpiecznej liście argumentów, nie ręcznie sklejonej linii shell; obsługa polskich znaków, spacji i ścieżek Windows.
3. Pełen stdout i stderr trafiają do plików; wywołujący dostaje status, exit_code, elapsed, path, bytes, hash, truncated i krótki preview.
4. Nie zakładaj, że exit 1 rg/grep to awaria: to często zero_matches. Wyróżnij zero_matches, invalid_input, missing_binary, source_missing, timeout, cancelled, partial i runtime_error.
5. Pliki raw pozostają niezmienione. Kolejny filtr tworzy nowy artefakt z parent_ref, a nie nadpisuje źródła.
6. Nie kopiuj dużych danych między Luną i programem CLI; przekazuj ścieżkę lokalną w tej samej instancji.
7. Unikaj tworzenia setek pojedynczych procesów dla 100 rekordów: jednorazowe przetwarzanie pliku jest celem.
**Wynik:** file in -> gotowy proces -> file out, bez wielkiego tekstu w modelu.
**Mapowanie:** OF-FLT-01/02.

### 6. A03: TYPOWANE OPERACJE FILTRÓW
**Kontrakt semantyczny:** source_file/capture_id, file_kind, operator, literal/regex/json_query, projection, order, max_results, offset, expected_source_hash i żądany format wyjścia. Wynik: total_matches, returned, omitted, complete, source_ref, output_ref, output_hash, status i reason_code.
- Text literal/regex/context/count: rg z odpowiednimi flagami.
- JSON/NDJSON fields/filter/aggregate/count: jq, bez niepotrzebnego wczytania całego NDJSON naraz.
- Operacje wierszowe: grep, sed, awk, sort, uniq.
- Historia kontaktów i wiadomości: SQLite/FTS5, nie grep po całym globalnym folderze dla każdego kontaktu.
- Python: pomocniczo, jeśli standardowe polecenia nie rozwiązują konkretnego przypadku.
- DOM HTML: najpierw CSS w WebView2 i zapis semantycznych rekordów do JSON, potem jq. Regex HTML nie jest parserem struktury drzewa.
**Wynik:** query_capture działa na historycznym capture_id bez WebView2 i bez nowego wywołania modelu.
**Mapowanie:** OF-FLT-02/06.

### 7. A04: BOGATSZY CAPTURE
1. Zachowaj PageCaptureService i PageAuthenticationGate, weryfikacja konta PRZED zrzutem.
2. Dodaj manifest: capture_id, task_id, tab_id, provider, account_id/scope, URL, normalized route, view_type, observed_at, source_kind, root, view_signature, fingerprint, source_paths, size, SHA256.
3. Oddziel complete od partial, visible, loaded, traversed_all, virtualized, lazy_not_loaded, selector_not_found i auth_required.
4. Jedna logiczna obserwacja może tworzyć tekst, elementy i HTML pod jednym capture_id, ale nie trzeba wymuszać mode=all, jeśli wystarczą wybrane rekordy.
5. Ograniczenia wielkości mają być jawne. Sam krótki wynik dla modelu nie oznacza ucięcia surowego pliku.
6. Zapis source i manifestu ma być spójny, zgodnie z już istniejącymi atomowymi plikami.
7. Trwałe dane z DOM mają przechodzić do normalizowanych rekordów lub zewnętrznego pliku; duże HTML nie powinno być w prompt.
**Wynik:** jeden logiczny read z faktycznie potrzebnego widoku i możliwość wielokrotnej lokalnej analizy.
**Mapowanie:** OF-FLT-03.

### 8. A05: RECEPTURY I CACHE
1. Zachowaj CaptureRecipeStore i migruj obecne JSON. Klucz receptury: host, route, view signature, operation, account scope, capture mode, CSS root i filter plan id.
2. Receptura to wyłącznie techniczna metoda widoku: selector, expected fields, filter CLI, expected scope, auth marker, action anchors, last_verified i confidence.
3. Wybór: verified exact view+operation > verified route > neutral capture, po zweryfikowaniu struktury.
4. Cache może zostać użyty, jeśli account, tab/route, istotny fingerprint, pagination i zakres danych nadal odpowiadają żądaniu.
5. Zmiana konta, wątku, danych, zakresu lazy loaded lub paginacji unieważnia cache; zmiany czysto dekoracyjne nie powinny wymuszać pełnego read.
6. Nową metodę oznacz candidate; verified tylko po skutecznej interpretacji i wiarygodnym wyniku zadania. Nieskuteczną suspended z konkretną przyczyną, potem kontrolowany fallback.
7. Nie wysyłaj Luny osobno do capture_recipes przed każdym znanym widokiem. Dobór odbywa się lokalnie.
**Wynik:** ponowne użycie bez kosztownej pętli get_page/run_js.
**Mapowanie:** OF-FLT-04/05.

### 9. A06: INTEGRACJA W AGENTRUNNER I MCP
1. W AgentRunner i ToolRegistry duże widoki list/tabel/inbox kieruj domyślnie do observe_view/capture/filter; dla formularzy pozostaw get_page/act/act_many.
2. Rozdziel observe_current_view (nowy DOM) i query_existing_capture (wyłącznie plik). Lokalne pytanie o inne pole nie generuje kolejnego DOM.
3. W MCP dodaj czytelny kontrakt query_capture dla wcześniej zapisanych artefaktów z istniejącą izolacją requester/chat/task_id.
4. Odpowiedź do Luny: kilka rekordów, total_count, kompletność, source_ref, reason. Nie przekazuj CSS/SVG/skryptów, chyba że zadanie ich dotyczy.
5. Przed ponownym get_page porównaj widok/zmianę stanu. Nie nagradzaj kolejnego identycznego read tylko dlatego, że poprzedni tool zakończył się technicznie poprawnie.
6. Dla powtarzającego się błędu zwróć jeden reason_code z krótkim dowodem, potem inna metoda albo Luna.
7. Zachowaj zgodność z lekkim openfill_info i dotychczasowym openfill_start_task; nie twórz drugiego MCP.
**Wynik:** standardowe przetwarzanie list nie jest sekwencją ręcznych decyzji modelu.
**Mapowanie:** OF-FLT-05/06/09.


### 10. B01: TRWAŁA PAMIĘĆ OPERACYJNA
**Wyraźnie rozdziel trzy warstwy:**
- Profil przeglądarki WebView2: sesje, cookies, IndexedDB/localStorage i stan strony. Nie jest trwałym rejestrem operacji OpenFill.
- Pamięć procesu: bieżący tab, view fingerprint, capture_id, aktualny cursor batch i krótkotrwały cache. Jej utrata nie może powodować ponownej wysyłki.
- Pamięć na dysku PBOX1: konta, profile, powiązania osób, znane thread URLs, historia wiadomości, historia skutkowych akcji i stan wznowienia. To źródło prawdy dla OpenFill.

**Docelowe lokalizacje logiczne w istniejącym AppPaths.Root, osobno stable/dev:**
- memory/operational.db: SQLite jako kanoniczny rejestr kont, identity, rozmów, wiadomości, wysyłek i operacji.
- memory/blobs: pliki większych treści, z referencjami i hashami.
- captures/task_id/capture_id: surowy snapshot, jego transformacje i manifesty.
- procedures: kopia opublikowanych, wersjonowanych definicji procedur z SOURCE.
- recipes: techniczne receptury, z zachowaniem migracji obecnego CaptureRecipeStore.
- istniejące notes, logs, history i tasks zachować, nie tworzyć drugiego konkurencyjnego systemu tego samego rodzaju.

**Struktura danych do utrwalenia:**
| Encja | Istotne pola i klucze |
| --- | --- |
| ProviderAccount | account_id, provider, browser_profile, provider_account_id, auth_last_verified |
| Person/Entity | entity_id, nazwy/aliasy, firma, pochodzenie powiązania |
| ChannelIdentity | provider, account_id, provider_user_id/JID/phone, canonical_profile_url, observed_at, source_ref |
| Conversation | account_id, provider, thread_id/thread_url, participants, last_message_id, observed_at, scope |
| Message | provider_message_id lub jawnie niepewne ID zastępcze, thread_id, direction, author, timestamp, body_ref/hash, delivery_state |
| Observation | capture_id, URL, account, view, scope, complete, observed_at, data_hash |
| OperationBatch | request_id, task_id, requester/chat, procedure_id/version, targets_count, progress |
| OperationItem | operation_id, batch_id, account, target, action, payload_hash, state, evidence_ref, error_code |
| ProcedureRun | version procedury, step, cursor, pending_uncertain, resume info |
| Artifact | file_ref, parent_ref, sha256, size, kind, retention_class, creation_time |

**Reguły kluczowe:**
1. Każdy rekord przynależy do konta nadawcy i dostawcy. To samo imię, wyświetlana nazwa lub tekst podglądu nie jest jednoznacznym ID odbiorcy.
2. Dla osoby z LinkedIn i WhatsApp powiązanie cross-platform powstaje tylko z wiarygodnego źródła, nie przez podobieństwo nazw.
3. Dane trwałe i zmienne mają osobne daty: observed_at, last_verified_at, updated_at; ostatni preview WhatsApp nie jest pełną historią czatu.
4. Link do profilu/rozmowy zachowuje źródło i historię zmian; ponowna wizyta w witrynie może go odświeżyć, ale nie powinna skasować wcześniejszych dowodów.
5. Dla wyszukiwania: najpierw provider ID/JID, potem kanoniczny URL, później alias/FTS5, a dopiero na końcu nowa eksploracja witryny.
6. W relacyjnych, transakcyjnych zapisach używać gotowej biblioteki SQLite dla .NET. Narzędzie sqlite3 CLI instalujemy dla hurtowych zapytań, importów, diagnostyki i historii, ale nie uruchamiamy osobnego procesu dla każdego nowego wpisu.
7. FTS5 służy do wyszukiwania indeksowanych treści, rg/jq do szybkiego przeszukiwania niezaindeksowanych plików. Nie wybierać jednego z nich „zamiast wszystkiego”.
8. HistoryStore pozostaje historią całych tasków z referencją do batch_id; ledger operacji znajduje się w SQLite. Nie dublować kanonicznego ledgera w osobnym pliku NDJSON, którego stan mógłby się rozjechać.
9. Do Luny pobieraj tylko konkretne wyniki zapytania pamięci. Nie wstrzykuj automatycznie całego indeksu kontaktów do promptu.
**Wynik:** po restarcie pamięć profili, rozmów i operacji nie ginie i jest odpytywalna bez przeglądarki.

### 11. B02: LEDGER, IDEMPOTENCJA I WZNOWIENIE
**Cykl logiczny:** planned -> prepared -> attempted -> verified / uncertain / failed / skipped / blocked.
- planned: zaplanowana pozycja na liście, jeszcze bez działania;
- prepared: zweryfikowane konto, target i treść, zamiar zapisany przed akcją;
- attempted: wykonano techniczną akcję w UI, ale sukces nie musi być potwierdzony;
- verified: konkretny dowód efektu w zewnętrznym serwisie, z evidence_ref;
- uncertain: działanie mogło się udać lub nie, brak dowodu; nie powtarzać automatycznie;
- failed: udowodniony brak efektu, z reason_code;
- skipped: celowo pominięto w ramach tego zlecenia;
- blocked: brak warunków wykonania, np. auth/account mismatch.

1. Przed pierwszą wiadomością zapisz batch request_id i osobne OperationItem dla każdego odbiorcy. Zapisz konto, target, treść/payload_hash, action i idempotency key.
2. Idempotencja jest zależna od TEGO SAMEGO zlecenia, konta, targetu, rodzaju działania i treści, a nie globalnej reguły „do X nigdy nie wysyłaj ponownie”.
3. Użytkownik może zlecić nową, identyczną wiadomość następnym razem. Nowy request_id oznacza nową operację. Ten sam request_id wznowiony po awarii nie może prowadzić do podwójnego wysłania.
4. Po kliknięciu Send istnieje luka pomiędzy skutkiem w serwisie a lokalnym zapisem. Nie da się zagwarantować exactly-once na dowolnej witrynie bez serwerowego idempotency API. W tej luce status uncertain jest prawidłowy.
5. Przy wznowieniu: verified i skipped pomiń, uncertain najpierw reconcile przez odczyt wątku/wyniku, failed przeanalizuj po przyczynie, pending wykonaj.
6. Nie przyjmuj za dowód samego exit code narzędzia, UI click, kliknięcia Save czy HTTP 200, jeśli operacja skutkowa ma własny widoczny rezultat.
7. Aktualizuj stan po każdej pozycji, nie dopiero na końcu 10 wiadomości. Postęp ma przetrwać restart oraz archiwizację czatu zlecającego.
8. Raport zawiera osobno total, verified, uncertain, failed, skipped, pending; do każdej pozycji link do profilu lub wątku i krótki powód statusu.
**Wynik:** przerywanie i ponowne wywołanie nie wymazuje historii ani nie rozpoczyna całej listy od nowa.

### 12. B03: POLITYKA PAMIĘCI I ŚWIEŻOŚCI
1. Dane stabilne: provider IDs, stany operacji, historyczne wiadomości i ich dowody. Przechowuj długoterminowo.
2. Dane półtrwałe: profile URLs, thread URLs, selektory, receptury. Używaj ponownie, ale przed działaniem zweryfikuj aktualną tożsamość i widok.
3. Dane zmienne: stan logowania, unread, ostatnie preview, bieżące ceny, formularze i composer. Przechowuj jako historyczne obserwacje, przy potrzebie aktualnego stanu odczytaj ponownie.
4. Zrzuty raw: potrzebne do powtórnej analizy i diagnostyki; można je katalogować i usuwać później według budżetu dysku, ale ich usunięcie NIE może usunąć ledgera i istotnych rekordów kontaktów.
5. Wydziel manifesty i niewielkie dowody od masy plików HTML; ogranicz duplikowanie identycznych dużych blobs po SHA256, zachowując stabilne referencje.
6. Nie używaj starych DOM jako jedynego dowodu bieżącego stanu. Dla wysyłki, ceny i logowania niezbędna jest minimalna aktualna obserwacja.
7. Nie zakładaj, że cache WebView2 jest miejscem dla globalnej pamięci operacji, bo jest zarządzany przez przeglądarkę i origin, nie przez OpenFill.
**Wynik:** znane fakty są trwałe, zmienne stany odświeżane wyłącznie wtedy, gdy to potrzebne.

### 13. C01: KATALOG PROCEDUR, OSOBNO OD RECEPTUR
**Ścisłe pojęcia:**
- Narzędzie: pojedyncze rg/jq/DOM act/whatsapp_send.
- Receptura: jak odczytać dany widok LinkedIn/WhatsApp/Airbnb, jakie selektory i filtry wybrać.
- Procedura: jak wykonać cały cel, np. send_batch, od autoryzacji do dowodu i rejestru.
- Workflow: późniejsza kompozycja różnych procedur w proces biznesowy, bez rozbudowy w pierwszym wdrożeniu.

**SOURCE, proponowany logiczny układ definicji:**
- procedures/registry.json: lista intentów, nazwy, wersje, wymagane pola, provider, confidence;
- procedures/core: ensure_auth, resolve_identity, find_known_thread, observe_capture, query_file, verify, record_result, resume;
- procedures/sites/whatsapp: list_visible, read_loaded, read_batch, send_one, send_batch, list_all_previews;
- procedures/sites/linkedin: ensure_auth, resolve_profile, resolve_thread, list_messages, send_one, send_batch, invite_batch;
- procedures/sites/airbnb: resolve_listing, capture_calendar, change_price_range, verify_price_range;
- procedures/sites/google i procedures/sites/meta: wyłącznie wybrane po raporcie E01;
- recipes/sites/host: techniczne root, view_signature, CSS, jq/rg, marker kompletności i locators.

**Jedyny wykonywalny kontrakt procedury w wersjonowanym JSON:**
procedure_id; schema_version; version; status candidate/verified/suspended; provider; intent; input_schema; required_identity; preconditions; stages; recipe_refs; state_persistence; output_schema; verification_rule; error_routes; resume_rule; last_verified; change_reason.

**Dozwolone typy etapów:** ensure_auth, resolve_target, navigate_known, observe, query_stored, select_batch, execute_existing_action, verify, persist, report, escalate_to_luna. Nie projektować wielkiego własnego języka skryptowego, pętli i instrukcji warunkowych konkurencyjnego z Pythonem/C#.
Procedura już rozpoczęta pozostaje powiązana z konkretną wersją definicji. Zmiana pliku w SOURCE nie zmienia niejawnie aktywnego batcha w LIVE.
SiteNotesStore nie jest wykonywalnym registry, choć może dostarczać dodatkowe notatki Lunie.
**Wynik:** procedury są w jednym przewidywalnym katalogu, łatwo przeglądalne i edytowalne przez programistę, bez napompowywania promptu Luny.

### 14. C02: ROUTER INTENCJI I WYKONAWCA
1. Model zlecający opisuje cel, konto, odbiorców, treść, zakres, request_id. Nie przekazuje 100 interakcji i nie zarządza sam kliknięciami.
2. Router OpenFill dopasowuje jednoznaczny intent do dostępnej verified procedure. Jeśli nie ma dopasowania, pozostaje dotychczasowy autonomiczny AgentRunner/Luna.
3. Przed skutkową operacją lokalny wykonawca zapewnia trwały batch i idempotency scope.
4. Wykonawca używa obecnego ToolRegistry i BrowserController. Dla 10 targetów wykonuje sekwencję w jednej logicznej procedurze, bez modelowej tury per target.
5. Wszystkie widoki czyta przez A04/A05/A06. Z bazy bierze już znane dane i wątki, nie żąda ich automatycznie z DOM.
6. Luna dostaje wyjątki: brak jednoznacznego targetu, nieznany DOM, awaria logowania, niepewna wysyłka wymagająca decyzji. Dostaje mały manifest z możliwymi alternatywami.
7. Po rozwiązaniu wyjątku executor wznawia zachowany batch, a nie zaczyna od początku.
8. Wynik i postęp są udostępniane przez istniejący task_status; nie pisz drugiego zadaniowego scheduler.
9. Wymagane reguły zapisów, tożsamości, dopasowania procedury i idempotencji muszą być egzekwowane przez OpenFill, nie tylko wyłącznie tekst promptu.
**Wynik:** model zlecający deleguje jedno zadanie biznesowe, a Luna nie odtwarza całej technicznej sekwencji dla każdej osoby.

### 15. C03: WSPÓLNE PROCEDURY BAZOWE
**ensure_auth**
- Odczytaj krótki marker sesji i aktualnie zalogowanej tożsamości z bieżącej karty lub pierwszego potrzebnego widoku.
- Nie wymuszaj przejścia na stronę główną, jeśli bieżący widok wystarcza. Sprawdzenie homepage jest wariantem, nie obowiązkową dodatkową nawigacją.
- Dla braku sesji uruchom procedurę login danego provider; challenge/MFA -> waiting_for_user i checkpoint.
- Po login potwierdź właściwy account_id. Sama obecność formularza, hasła w magazynie czy URL /messaging/ to nie dowód autoryzacji.
- Stany: authenticated, authentication_required, challenge_pending, account_mismatch, failed, unknown.

**resolve_target**
- Najpierw SQLite: provider identity, known profile URL, known thread URL.
- Potem minimalny bieżący widok/lista previews, jeśli wpisu brak lub jest stale.
- Przy niejednoznacznym imieniu, profilu albo numerze nie zgaduj, nie wysyłaj.
- Utrwal raz poznany identyfikator z provenance i account scope.

**observe_and_query**
- DOM tylko gdy rzeczywiście potrzebny; raw -> plik -> filtr CLI -> mały JSON/manifest.
- Inny filtr na tym samym capture nie wywołuje WebView2.
- Nie nazywaj widocznych kilkunastu wierszy „wszystkimi” bez rozstrzygnięcia wirtualizacji.

**execute_and_verify**
- Używaj istniejących działań przeglądarki, rejestruj attempted i dowód dla odpowiedniego provider.
- Wysyłka wymaga rozpoznania właściwego rozmówcy, a UI sent oznacza co najwyżej obserwację outgoing w UI, nie doręczenie.
- Blok akcji może mieć zbiorczą kontrolę, ale skutek każdej pozycji musi mieć przypisany stan.

**resume_and_reconcile**
- Po restarcie załaduj batch, konto, procedure_version i status każdej pozycji.
- Verified pomijaj, uncertain weryfikuj w zewnętrznym serwisie przed ewentualnym ponowieniem.
**Wynik:** wszystkie serwisy używają tej samej logiki ogólnej, różnice mają w cienkich adapterach.

### 16. D01: WHATSAPP, PIERWSZY WZORZEC PROCEDURY DOMENOWEJ
Wykorzystaj obecny WhatsAppBrowser, nie twórz Luny Mini. Zgodność ze specyfikacją z 29.10 ma pierwszeństwo przed uproszczeniami raportu Deep Research.

**Funkcje:**
1. whatsapp.observe_sidebar: automatyczny zwięzły odczyt wszystkich wyrenderowanych previews, bez obrazu, bez otwierania wszystkich rozmów. Zapis scope=visible, observed_at, account.
2. whatsapp.resolve_contact: znany numer/JID -> bezpośrednie otwarcie; po nazwie najpierw pamięć i previews. Nie otwierać losowych czatów, aby odgadnąć odbiorcę.
3. whatsapp.read_loaded: jeden odczyt tylko aktualnie załadowanych wiadomości. Odróżnij loaded od complete, zachowaj message IDs i direction, gdy są dostępne.
4. whatsapp.read_batch: deduplikuj listę numerów, otwórz unikalny numer raz w zwykłym przebiegu, lokalnie zachowaj wynik każdego. Nie odtwarzaj 69 otwarć dla 13 numerów.
5. whatsapp.send_batch: open by ID, potwierdzenie rozmówcy, send, lokalny odczyt rezultatu, wpis do ledgera; kolejne pozycje bez osobnej tury Luny.
6. whatsapp.list_all_previews: pełne przejście po wirtualizowanej liście tylko na wyraźne zlecenie, nie wykonywać domyślnie i nie mieszać z otwieraniem rozmów.
7. Nie dodawać warunku „najpierw zamknij wszystkie czaty”. Nie jest potrzebny i jest sprzeczny z wcześniejszym ustalonym przepływem.
8. Nie uznawać samego nowego outgoing bubble za doręczenie do telefonu odbiorcy. To status UI sent.
**Wynik:** WhatsAppBrowser pozostaje jedynym adapterem, a batch/progress/pamięć powstają w warstwie wspólnej.

### 17. D02: LINKEDIN, 10 WIADOMOŚCI
**Kontrakt wejścia:** expected_sender_account, request_id, 10 jednoznacznych targetów (profile URL/provider ID/znany thread), treść dla każdego lub jawny szablon, wykonanie zgodne z zakresem zlecenia.

**Sekwencja:**
1. Rejestracja batch i 10 pozycji OperationItem w trwałym SQLite przed pierwszą wysyłką.
2. Minimalny check zalogowania i actual sender account z bieżącej karty; jeśli brak, wspólna ensure_auth LinkedIn.
3. Jedno lokalne zapytanie do pamięci o 10 provider identities i thread URLs. Wynik dzieli cele na resolved/unresolved.
4. Tylko dla unresolved wykonanie realnej minimalnej eksploracji profilu/listy i utrwalenie nowych IDs/links, bez każdorazowego przeszukiwania całego LinkedIn.
5. Przed każdą akcją potwierdź expected target i właściwy thread, wykorzystując aktualny semantyczny marker z widoku. Stara kotwica DOM to nie aktualny uchwyt.
6. Dla verified z tego samego request_id pomiń. Dla uncertain najpierw reconcile, nie ponawiaj automatycznie. Pozostałe pending -> prepared.
7. Otwórz thread, wpisz treść, wykonaj send przez obecne kontrolki browser. Oznacz attempted.
8. Lokalnie rozpoznaj outgoing wiadomość w właściwym wątku. Utrwal provider_message_id albo mniej pewny dowód (tekst+czas+thread) z odpowiednią oceną niepewności.
9. Zapisz wynik pozycji i źródłowy wątek, przejdź do następnego. W przypadku challenge/login/innego konta checkpoint i wznowienie, nie restart listy.
10. Raport po serii: requested, verified, uncertain, failed, skipped, thread URLs i operation IDs.
11. W przyszłym NOWYM zadaniu można ponownie napisać do tych samych 10 osób. To nie jest duplikat starego request_id.
**Oddzielne procedury:** linkedin.ensure_auth, linkedin.resolve_profile, linkedin.resolve_thread, linkedin.send_one, linkedin.send_batch, linkedin.invite_batch, linkedin.read_inbox. Zaproszenie i wiadomość mają inne warunki, inne stany i inne dowody.
**NIE koduj niezweryfikowanego dziennego limitu wysyłek z reportu zewnętrznego jako trwałej reguły.** Faktyczną dostępność akcji określa odpowiedź serwisu. Nie projektuj zamiennego, niepotwierdzonego API LinkedIn zamiast WebView2.
**Wynik:** dla 10 wcześniej znanych thread IDs nie występuje 10 niepotrzebnych wyszukiwań kontaktów, a wszystkie wyniki są zapisane osobno.

### 18. D03: AIRBNB, ANULOWANE 2026-11-02 (wyłącznie historia)
1. Klucz operacji obejmuje account_id, listing_id, walutę, rate plan (jeśli istnieje) i zakres dat.
2. Odczytaj właściwy autoryzowany listing i jednym logicznym capture pobierz dostępny zakres kalendarza, z complete/partial.
3. Zapisz na dysku dla każdej daty old_price, new_price, currency, source_ref i planowany efekt.
4. Przed edycją zarejestruj intencję i OperationItem per dzień/logiczna zmiana.
5. Użyj istniejących akcji interfejsu i możliwości batch bez modelowej pętli per data.
6. Zweryfikuj wartości zapisane na witrynie dla konkretnego zakresu, a nie wyłącznie klik Save.
7. Po przerwaniu najpierw ustal stan dat uncertain, potem kontynuuj pending. Nie modyfikuj ponownie dat już potwierdzonych.
8. Raport zawiera daty zweryfikowane, niepewne i błędne oraz wartości przed/po.
**Wynik:** jedna strukturalna operacja dla wielu dat bez setek search_logs/get_network w tym samym stanie.

### 19. E01: NAJCZĘSTSZE RZECZYWISTE INTENCJE, NIE TYLKO DOMENY
Analizuj lokalnie historyczne NDJSON z logów i HistoryStore za pomocą rg/jq/SQLite, nie wczytuj pełnej historii do modelu.
1. Oprócz provider wyprowadź klasy: read/list, read_thread, send_message, invite, update_profile, edit_price/calendar, comment/post, search/contact lookup, export, diagnose, other/unknown.
2. Rozdziel szerokie Google na GBP, Gmail, YouTube Studio, pozostałe. Meta na Facebook Pages, Messenger, inne; nie zakładaj identycznego DOM.
3. Dla każdej intencji policz: zadania, średnią liczbę targetów, model_calls, tokens, cost, get_page, run_js, act, repeated operations, confirmed outcome, unknown outcome.
4. Podaj reprezentatywne run IDs i poziom pewności klasyfikacji. Nie oznaczaj unknown jako failed.
5. Zidentyfikuj najbardziej powtarzalne sekwencje, które da się sprowadzić do procedury bez Luny, oraz najczęściej odzyskiwane od nowa identyfikatory.
6. Priorytet dalszych procedur wyznacz jako funkcję częstotliwości, potencjału oszczędności, stabilności widoku i znaczenia biznesowego.
7. Wynikiem ma być ranking KANDYDATÓW na procedury z liczbami, nie tylko opinia, że „Google jest największe”.
**Wynik:** podstawa do D04; E01 może być realizowane niezależnie od B/C/D.

### 20. D04: GOOGLE I META DOPIERO PO E01
1. Wybierz konkretne 1-2 powtarzalne intencje na platformę z najwyższą opłacalnością z E01.
2. Nie projektuj jednego adaptera „Google”, bo Gmail, GBP i YouTube to inne serwisy i inne obiekty.
3. Użyj standardowego ensure_auth, resolve, capture/filter, verify, ledger i resume. Tylko view recipes i konkretne action bindings są domenowe.
4. Dla niepowtarzalnego działania nie twórz procedury na siłę; zwykła Luna pozostaje dostępna.
5. Przykładowe kandydatury, nie potwierdzony ranking: odczyt/odpowiedź w korespondencji, aktualizacja danych Google Business, odczyt komentarzy, zarządzanie stroną Facebook. Realny wybór zależy wyłącznie od E01.
**Wynik:** mały katalog naprawdę wykorzystywanych procedur zamiast wielkiej nieweryfikowanej biblioteki.


### 21. E02: UTRWALANIE SPRAWDZONYCH METOD
1. Receptura odczytu: candidate -> verified po skutecznej ekstrakcji odpowiednich danych i potwierdzonym rezultacie; suspended w razie zmiany witryny lub jakości wyniku.
2. Procedura całego zadania ma niezależne statusy candidate/verified/suspended i wersję. Techniczny sukces page_capture nie oznacza sukcesu wysłania 10 wiadomości.
3. Po zakończeniu zadania zapisuj wyłącznie nowe trwałe metody, identyfikatory i wiarygodne wyniki; nie zapisuj nieselektywnie całych reasoning traces ani dekoracji DOM.
4. Gdy istniejący selektor przestaje działać, zwróć konkretny reason, użyj neutralnego fallback lub Luny; nie powtarzaj identycznej nieskutecznej receptury.
5. Luna może zaproponować nową candidate procedure, ale nie wykonuje automatycznego wdrożenia niezweryfikowanego przepisu w LIVE.
6. Notatki SiteNotesStore pozostają faktami o witrynie; nie są wykonywalnym poleceniem i nie zastępują osobnego katalogu procedur.
**Wynik:** zdobywana wiedza poprawia kolejne przebiegi, bez mechanizmu samoczynnej niekontrolowanej edycji systemu.

### 22. E03: MIARY SKUTECZNOŚCI BEZ PROJEKTOWANIA TESTÓW
Nie budować nowego modułu raportowego, lecz dopisać do istniejących logów spójne pola:
- provider/account/intent/procedure_id/version/batch_id/operation_id;
- model_calls, total/cached/uncached input_tokens, output, cost;
- capture_id, bytes in/out, filter_elapsed, recipe hit/miss, reused_snapshot;
- liczba unikalnych odczytów DOM i liczba identycznych nieuzasadnionych powtórek;
- per item: verified/uncertain/failed/skipped i proof_ref;
- reason_code awarii oraz źródłowy task/run id.
Porównuj koszty i efekty na podobnych zadaniach. Cel z OF-FLT-09: hipoteza redukcji median uncached tokens do 50% baseline i wywołań modelu do 60%, nie sztywny limit blokujący realny postęp. Nie oznaczaj niekompletnego przebiegu jako failed, jeśli nie ma dowodu. Zadanie jest dobre wtedy, gdy rzeczywisty rezultat jest poprawny, nie wtedy, gdy ma mało tokenów.
**Wynik:** można ustalić koszt i wynik jednej logicznej czynności, nie tylko całej domeny.

### 23. KONTRAKT MODELU ZLECAJĄCEGO, LUNY I OPENFILL
**Model zlecający:**
- opisuje jedno pełne zlecenie: intencja, service/account_hint, target IDs/URLs, payload, zakres, request_id;
- dla 10 adresatów uruchamia jeden batch, nie 10 niezależnych tasków;
- gdy potrzebne dane historyczne, pyta lokalną pamięć, nie musi ponownie nawigować po stronie;
- nie narzuca sekwencji mikrokroków i nie transportuje całego DOM przez MCP.

**Luna:**
- wyjaśnia wyjątkowe niejednoznaczności i projektuje nową candidate recipe;
- dostaje kompaktowy stan + kilka konkretnych rekordów + reason_code, nie nieprzetworzony ogromny HTML;
- nie jest prawdą źródłową o wysyłkach i kontaktach;
- gdy wykonawca ma sprawdzony przepis, nie musi wykonywać odrębnej tury dla każdego kliknięcia.

**OpenFill:**
- obowiązkowo egzekwuje dobór konta, target identity, ledger, idempotency, record/update, stale_view i dowody skutków;
- udostępnia listę procedure intents w zwięzłej formie, zachowuje istniejący openfill_start_task/openfill_task_status i lekkie openfill_info;
- opcjonalne logiczne operacje discover_procedures, execute_procedure, query_memory, query_capture, batch_status mogą być rozszerzeniami istniejącego MCP, nie obowiązkowo pięcioma nowymi tools;
- utrzymuje dotychczasowe requester/chat/task_id i izolację tasków.

**Wymuszanie zasad:** opis MCP i prompty klientów służą do poprawnego wywołania. Krytyczne reguły, takie jak niepowtarzanie uncertain, są implementowane serwerowo, bo model zlecający może zignorować opis narzędzia.

### 24. DOKŁADNE POWODY NIEPOWODZEŃ I ROUTING WYJĄTKÓW
Przykładowe reason_code: executable_missing, invalid_tool_args, zero_matches, source_missing, source_hash_mismatch, capture_partial, auth_required, challenge_pending, account_mismatch, target_ambiguous, thread_not_found, composer_missing, stale_view, virtualized_incomplete, action_not_observed, action_uncertain, timeout, service_blocked, external_connector_unavailable.

Każdy reason powinien mieć stage, operation_id, provider, current URL/route, expected vs observed marker i referencję do dowodu. Nie deklarować sukcesu tylko z powodu exit 0. Nie wykonywać długich pętli get_page/run_js/search_logs bez nowej informacji. Jeśli poprzednia metoda nie wnosi postępu, wybierz inną opisaną metodę lub przekaż Lunie konkretny problem. Sama udana nawigacja nie jest dowodem zmiany danych i nie powinna zerować guard redundantnych odczytów.

Status lokalnego MCP OpenFill i status zewnętrznego konektora należy rozróżniać. W audycie z 31.10 lokalny smoke PASS współistniał z błędem konektora UNAVAILABLE / Connection failed; nie wolno utożsamiać tego z awarią PageCaptureService bez sprawdzenia warstwy pośredniej.

### 25. MAPA KOMPONENTÓW I ODPOWIEDZIALNOŚCI
| Obecny komponent lub miejsce | Docelowy zakres | Pakiety |
| --- | --- | --- |
| Agent/TerminalProcessService | uruchamianie CLI, stdout/stderr pliki, statusy i argumenty | A01-A03 |
| Agent/PageCaptureService | manifesty snapshotów, zakres, źródła, fingerprint | A04 |
| Agent/CaptureRecipeStore | v2, route/view/operation/account/quality, migracja | A05,E02 |
| Agent/ToolRegistry | observe/query, reuse existing actions, WhatsApp primitives | A06,C02,D01 |
| Agent/AgentRunner | automatyczny routing dużych widoków, wyjątki, krótki kontekst | A06,C02 |
| Agent/FailedCallGuard | bezcelowe powtórzenia i brak zmiany stanu | A05,A06 |
| Agent/SiteNotesStore | luźne notatki, oddzielone od recipes/procedures | B03,E02 |
| Agent/PromptHistoryCompactor | zachować, nie kopiować całej pamięci DB do modelu | B03 |
| Browser/BrowserController | istniejący WebView2, DOM/click i rozpoznanie widoku | A04,C03 |
| Browser/WhatsAppBrowser | istniejące direct open/read/send, kompozycja batch | D01 |
| Hosting/HistoryStore | historia całych tasków, referencje do batcha | B01,B02 |
| Config/AppPaths | stable/dev, memory, captures, procedures, recipes | B01,C01 |
| Mcp/McpServer i McpTaskManager | spójne query i status, bez nowego serwera | A06,C02 |
| Nowy minimalny interfejs SQLite | account/identity/message/ledger, gotowa biblioteka | B01-B03 |
| Nowy mały registry/executor | deklaratywne procedury, wznowienie, skutki | C01-C03 |

Nazwy nowych plików, klas i nazw endpointów są funkcjonalnymi opisami, nie poleceniem tworzenia konkretnej liczby plików. Agent programista stosuje aktualną strukturę repo i nie dubluje istniejących serwisów.

### 26. MAPOWANIE POPRZEDNIEGO PLANU OF-FLT DO NOWEGO
| Stara pozycja | Nowe wykonanie | Decyzja integrująca |
| --- | --- | --- |
| OF-FLT-00: baseline | A00+E01 | istniejący audyt wykorzystać, ranking intencji doprecyzować |
| OF-FLT-01: rg/jq | A01 | rozszerzyć o sqlite3 CLI i narzędzia GNU, pełne ścieżki |
| OF-FLT-02: FileFilterService | A02+A03 | typowany kontrakt, filtr realizowany przez CLI |
| OF-FLT-03: PageCaptureService | A04 | autoryzacja, kompletność, hash, pliki |
| OF-FLT-04: recipe v2 | A05 | osobne receptury i niezależne procedury |
| OF-FLT-05: observe_view/query_capture | A05+A06 | domyślne wykonanie, nie tylko sugestia dla Luny |
| OF-FLT-06: MCP file query | A06 | mały wynik, kontrola task_id, brak nowej Luny |
| OF-FLT-07: domeny | D01+D02+D04 (D03 anulowane) | oparte na B01-B03 i C01-C03, bez kopii parserów |
| OF-FLT-08: testy | dotychczasowy proces programisty | użytkownik nie zamawia osobnego planowania testów |
| OF-FLT-09: pomiary | E03 | koszt i rzeczywisty skutek, bez fałszywego sukcesu |
| OF-FLT-10: wydanie | dotychczasowy proces SOURCE/STAGING/LIVE | brak automatycznego wdrożenia z samego dokumentu |

Stare ID zachować do śledzenia historii projektu, ale odtąd pakiety A00-E03 wyznaczają faktyczną kolejność prac. Jeden pakiet może wskazywać stare numery, nie należy wykonać dwóch implementacji tej samej funkcji.

### 27. ROZSTRZYGNIĘCIA KRYTYCZNE WZGLĘDEM RAPORTÓW
1. Nie wymuszać homepage jako oddzielnego kroku przed każdą procedurą. Uwierzytelnienie potwierdza najtańszy wiarygodny marker rzeczywistego konta z bieżącego lub pierwszego docelowego widoku.
2. Nie zamykać wszystkich otwartych WhatsApp chats tylko po to, by czytać listę. Specyfikacja WhatsApp z 29.10 jest nadrzędna.
3. Nie blokować NOWYCH wiadomości do każdego adresata, który kiedykolwiek wystąpił w tabeli sent_messages. Idempotencja dotyczy konkretnego request_id i działania.
4. Nie przyjmować niezweryfikowanego twierdzenia o dziennych limitach LinkedIn z obcego materiału jako stałej reguły.
5. Nie utożsamiać wielkiego HTML z „więcej przydatnej historii”. Surowe zrzuty są materiałem, a długotrwale wartościowa historia to konta, identity, wątki, wysyłki, efekty, dowody i źródła.
6. Nie implementować drugiej pełnej bazy danych historii obok SQLite. Historia tasków pozostaje w dotychczasowym HistoryStore, a ledgers działań trzymane są w SQLite.
7. Nie tworzyć wielkiego frameworka workflow. Najpierw niewielki rejestr procedur i executor oparty na gotowych operacjach.
8. Nie robić drugiej Luny ani Luny Mini. Jedna Luna rozpoznaje wyjątki; deterministyczne czynności wykonuje OpenFill.

### 28. CHECKPOINT DLA CZATU PROGRAMISTY
Po każdym pakiecie zapisz w jednym projekcie:
- ID OF-IMP-20261031 + numer pakietu;
- co istnieje i co rzeczywiście zmieniono w SOURCE;
- czy wdrożono na STAGING, czy LIVE pozostało nietknięte;
- jakie są nowe kontrakty, schematy, foldery i formaty danych;
- jakie są rzeczywiste blokady i dokładne komunikaty błędów;
- jakie są niedokończone batch i uncertain operations;
- następny pakiet i zależności;
- gdy inny czat już zmienił te same pliki, co weryfikowano i jak uniknięto utraty jego pracy.

Nie usuwać wcześniejszych dokumentów, historii ani notatek. Nie pisać w checkpoint „DONE” tylko dlatego, że plan został opracowany. Zgłaszać oddzielnie SOURCE DONE, STAGING DONE i LIVE DONE zgodnie z procesem projektu.

### 29. KRYTERIUM FUNKCJONALNE CAŁOŚCI
System przyjmuje od zlecającego jedno zadanie, np. 10 wiadomości dla wskazanych kontaktów LinkedIn. OpenFill z pamięci pobiera aktualne znane konto, profile, powiązane thread links i historię operacji. Gdy brakuje danych, wykonuje minimalny read WebView2 do pliku, następnie lokalnie używa jq/rg/SQLite bez wysyłania gigantycznego DOM do Luny. Uruchamia wersjonowaną procedurę. Dla każdej pozycji rejestruje zamiar, wykonanie i dowód wyniku. Po restarcie nie powiela confirmed i nie ponawia automatycznie uncertain. Kolejne zadanie korzysta z raz poznanych identyfikatorów. Luna wchodzi w interakcję tylko przy nierozstrzygniętych przypadkach. Zlecający otrzymuje krótki wynik łączny wraz z linkami i statusami poszczególnych odbiorców.

**Natychmiastowy pakiet do wykonania przez programistę: A00 -> A01 -> A02 -> A03 -> A04 -> A05 -> A06.** Następnie B01-B03 -> C01-C03 -> D01-D02; E01 niezależnie po A00; D04 po E01; E02-E03 po zbudowaniu podstaw. D03 anulowane, bez wykonania.

### 30. REFERENCJE
- Pierwotny plan implementacji CLI/DOM: docs/OPENFILL_GREP_FILTER_PIPELINE_IMPLEMENTATION_PLAN_20261031.md.
- Raport architektury pamięci i procedur: docs/OPENFILL_ARCHITEKTURA_PROCEDUR_PAMIECI_I_NARZEDZI_20261031.md.
- Historyczny koszt i liczniki: docs/OPENFILL_TOKEN_WASTE_AUDIT_20261031.md i OPENFILL_TOKEN_AUDIT_DATA_20261031.json.
- Specyfikacja WhatsApp: docs/OPENFILL_WHATSAPP_SPEC_2026-10-29.md.
- Bieżący backlog i zasady SOURCE/STAGING/LIVE: docs/openfill_master_action_plan__v1.7__2026-10-17_1145.md i powiązane zasady aktualizacji.
- Dokumentacja narzędzi: https://github.com/BurntSushi/ripgrep/blob/master/GUIDE.md ; https://jqlang.org/manual/ ; https://sqlite.org/fts5.html ; https://sqlite.org/wal.html ; https://sqlite.org/json1.html .


### Aktualizacja priorytetu 2026-11-01: uniwersalny widok i krotkie akcje
- P0 A07: selektywna weryfikacja po akcji przez verify_view i act.verify, bez pelnego odczytu, z uncertain bez automatycznego ponowienia. SOURCE/STAGING QA PASS, LIVE jeszcze nie.
- P0 A08: rozpoznawanie struktury dowolnej strony, maly manifest, heurystyczny typ widoku, route_key/view_signature. SOURCE/STAGING: inspect_view i navigate(view_mode=structure) z realnym QA example.com PASS. Wymagane dalsze acceptance na list/table/inbox/form i realnych zalogowanych sesjach.
- P0 A09: lokalny dobor verified recipe i filtra po host/route/view_signature/account_scope, bez dodatkowej tury modelu. Nie traktowac samej struktury jako dowodu konta ani swiezosci danych.
- P0 A10: cache tylko z kontrola konta, zakresu, fingerprintu tresci i kompletnej paginacji; selective invalidation; zadnych automatycznych powtorek skutkowych uncertain.
- P0 A11: A/B rzeczywistych zadan z model_calls, input/cached/uncached tokens, model/elapsed ms, pelnymi DOM reads, structural probes, output bytes, cost i wynikiem. Pierwszy A08 baseline na example.com: fused navigation obnizylo calls 3->2 i cost 0.00112856->0.00099765 USD, lecz elapsed 7204->7198 ms i uncached tokens 8680->8734. To pojedynczy test, nie mediana.
- Airbnb D03: ANULOWANE decyzją właściciela 2026-11-02. Poprzedni niski priorytet jest nieaktualny. Nie realizować, nie testować, nie wydawać zmian Airbnb.

### Aktualizacja priorytetow 2026-11-01: universalny mechanizm pracy Luny
Ustalenie wlasciciela nadrzedne wobec pierwotnej kolejnosci adapterow:
1. Jednostka efektywnosci to cala sesja wieloetapowa (kilka kolejnych polecen i wiele dzialan), nie pojedyncze 2-3 tool calls. Bazowac na realnych logach LinkedIn i WhatsApp; porownywac identyczne zadania, mierzyc model calls, input/cached tokens, cost, czas, DOM reads i potwierdzone skutki.
2. Mechanizmy DOM, dobierania receptur i lokalnego wykonania musza byc uniwersalne, niezalezne od serwisu. Nie dopisywac na sztywno regulek dla LinkedIn/WhatsApp ani osobnej platformy procedur. Jeden lokalny view digest i batched actions maja zastapic liczne powtorki get_page/run_js.
3. A10 semantic digest, A11 act_locator i A12 selector-based act_many sa juz SOURCE+STAGING wedlug checkpoint. Uzyc ich jako punktu wyjscia do rozwoju. Testy na 3 realistycznych lokalnych SPA (messenger/network/crm) stanowią dowod podstawowej funkcji, nie oznaczaja akceptacji kazdej witryny.
4. Priorytet: szybkosc wdrozenia dla jednej osoby, minimalna liczba testow funkcji i naprawa rzeczywistych problemow; nie rozwijac autonomicznej infrastruktury QA/recovery. Airbnb poza backlogiem, anulowane.
5. Wydanie LIVE: bez względu na liczbę aktywnych zadań. Gdy nowa wersja jest gotowa, wykonać aktualizację natychmiast, ogłosić przerwę, spróbować kontrolowanego zamknięcia i po krótkim limicie czasu wymusić zakończenie procesu, podmienić binaria i uruchomić usługę. working=[] NIE jest warunkiem wydania. Nadrzędna zasada wydawania znajduje się w sekcji poniżej. Automatyzacja ChatGPT pozostaje wyłączona na życzenie właściciela.

### ZASADA STAŁA: zdroworozsądkowa ocena liczby wywołań modelu (01.11.2026)

**Cel:** ograniczać rzeczywiste marnotrawstwo wywołań modelu, tokenów i czasu, a nie osiągać najniższą możliwą liczbę wywołań za wszelką cenę.

1. **Oceniaj każde zadanie indywidualnie.** Jednostką oceny jest całe zadanie użytkownika zakończone użytecznym, zweryfikowanym rezultatem, a nie pojedynczy klik, ekran czy wywołanie narzędzia. Dla każdego zadania wyznacz niezależnie zdroworozsądkowy punkt odniesienia, uwzględniając jego złożoność, dostępne dane, niepewność i konieczną kontrolę skutku. Nie stosuj sztywnych limitów ani jednej skali wywołań dla wszystkich zadań.
2. **Porównuj rzeczywiste wykonanie z tym punktem odniesienia.** Ustal, które tury modelu były potrzebne do decyzji, a które zużyto na zbędne odczyty DOM, powtarzanie rozpoznania, sekwencje prostych kliknięć, odtwarzanie już znanych danych lub ponawianie bez nowej informacji. Powtarzalne czynności techniczne przenoś do lokalnego wykonania w OpenFill, o ile nie pogarsza to poprawności.
3. **Uwzględniaj malejące korzyści z optymalizacji.** Redukcja przykładowo z 7 do 3 wywołań może być łatwa i bardzo wartościowa. Redukcja z 3 do 2 może wymagać nieproporcjonalnie dużego nakładu i złożoności. Są to przykłady, nie progi ani cele obowiązujące wszystkie zadania.
4. **Wybieraj usprawnienia proporcjonalnie do zysku.** Priorytet mają proste zmiany usuwające największe straty, zwłaszcza w częstych zadaniach. Oceniaj łączny efekt w wywołaniach modelu, tokenach, czasie i koszcie względem kosztu implementacji, utrzymania i ryzyka pomyłek. Nie rozbudowuj aplikacji dla marginalnej oszczędności jednej tury.
5. **Nie usuwaj uzasadnionych wywołań.** Dodatkowa decyzja modelu ma sens, gdy rzeczywiście zmieniają się warunki, brakuje istotnych informacji albo potrzebna jest wiarygodna weryfikacja wyniku. Liczba wywołań sama w sobie nie jest miarą jakości: liczy się poprawnie wykonane zadanie i koszt jego realizacji.
6. **Raportuj praktycznie.** Dla analizowanego zadania podaj: rzeczywistą liczbę wywołań; indywidualnie oszacowaną liczbę rozsądną i jej uzasadnienie; główne źródła nadmiaru; możliwą oszczędność; orientacyjny trud jej uzyskania. Dopuszczalny i pożądany wniosek: „dalsza optymalizacja nieopłacalna”.

**Nadrzędna zasada:** usuwamy oczywiste i istotne marnotrawstwo, ale nie komplikujemy prostego, działającego rozwiązania, żeby za wszelką cenę wyeliminować ostatnie wywołanie modelu.

### ZASADA NADRZĘDNA: AKTUALIZACJA OPENFILL MA PIERWSZEŃSTWO (01.11.2026)

**Ostrzymy piłę:** OpenFill jest rozwijanym narzędziem. Aktywne zadania nie są powodem do odraczania nowego wydania. Ta zasada zastępuje wszystkie wcześniejsze wymagania czekania na bezczynność.

1. Nową gotową wersję wydaje się niezwłocznie, również gdy `working > 0`. Nie czeka się na zakończenie zadań, nie wprowadza progów kosztów przerwanych sesji ani dodatkowej zgody na restart.
2. Używa się istniejącego komunikatu maintenance: „Teraz nastąpi aktualizacja OpenFill. Wróć za kilka minut”. Standardowe zamknięcie może zapisać stan i przerwane zadania, ale nie wolno na nie czekać bez ograniczenia czasu.
3. Po krótkiej próbie kontrolowanego zamknięcia należy zatrzymać pozostający proces, podmienić binaria i uruchomić nową wersję.
4. Wykorzystuje się istniejący checkpoint, wznowienie i kopię poprzedniej wersji, jeśli są dostępne. Brak odzyskania stanu nie blokuje wdrożenia. Własny czat zlecający odpowiada za dalszą obsługę przerwanego zadania. Nie ponawia się automatycznie skutkowych operacji o niepewnym wyniku.
5. Potwierdzenie wdrożenia: zgodność plików nowej wersji, uruchomiony proces oraz odpowiedź MCP. Liczba aktywnych zadań nie jest kryterium.
6. Ta zasada jest nadrzędna wobec historii, starych skryptów oraz checkpointów mówiących o odroczeniu z powodu zajętości OpenFill. Gotowe wydanie zawsze ma pierwszeństwo.

**Aktualny punkt wejścia:** `tools/of_imp_release_live_ALWAYS_20261101.ps1`. Nie używać starych wersjonowanych skryptów z bramką `working=[]`.


### STATUS WDROŻEŃ, 02.11.2026 14:07 EAT: NADRZĘDNY NAD STARYMI TABELAMI PLANU

Poniższe statusy uwzględniają rzeczywisty SOURCE/STAGING/LIVE; wcześniejsze fragmenty planu są historyczną specyfikacją, nie aktualnym TODO.

- **LIVE: ukończone główne mechanizmy:** CLI i filtracja plików (A01-A03); capture + manifest + SHA256 (A04); routowanie widoków, odczyt po capture_id i semantyczne skróty (A06-A09); działanie wielu akcji lokalnie i zweryfikowane przejścia między profilami (A10-A21); parser powielonych argumentów (A22); rozpoznawanie inbox SPA (A23); krótszy prompt i mniej tur modelu (A20).
- **LIVE: WhatsApp:** zbiorczy odczyt według numerów i ostatnich 5-10 załadowanych wiadomości (A24); zbiorczy odczyt nazw widocznych rozmów (A25); wyszukiwanie nazw na pełnej wirtualizowanej liście, 1 lokalny skan na batch, fizyczne kliknięcie CDP i odczyt rozmowy (A26). Realny odczyt A26: status loaded na autentycznym WhatsApp, 2 model calls, USD 0.001, bez wysyłania.
- **LIVE: pamięć i procedury:** SQLite z indeksami, ledger z identyfikatorami operacji, katalog i wykonawca procedur, pamięć danych, technical CaptureRecipeStore i automatyczne wykorzystanie sprawdzonych receptur. Istniejące algorytmy nie wymagają przepisywania.
- **CZĘŚCIOWO:** A05: zweryfikowane receptury i ponowne użycie capture_id działają, ale cache treści między niezależnymi zadaniami nadal nie jest gotowy. Nie mylić pamięci technicznych receptur z aktualną treścią strony.
- **STAGING/CANDIDATE:** dedykowane whatsapp_send_batch nie jest wystawione w LIVE. Wcześniejsza próba ekspozycji została zablokowana przez narzędzie edycji, przyczyna konkretnej reguły nie została podana. Pojedyncze whatsapp_send pozostaje dostępne w LIVE.
- **CZĘŚCIOWO:** LinkedIn: lokalna sekwencja ośmiu zaproszeń w symulatorze zaliczona, 10 model calls; brak porównywalnego testu faktycznych wysyłek na LinkedIn. Airbnb: plan wielodniowych cen w SQLite, brak uniwersalnego zweryfikowanego adaptera zapisu do kalendarza Airbnb. Google/Meta: istnieje uniwersalny mechanizm semantycznego DOM, osobnych adapterów domenowych nie deklarować jako ukończonych.
- **Wydania:** obowiązuje wyłącznie tools/of_imp_release_live_ALWAYS_20261101.ps1, bez czekania na working=[]. Kopie poprzednich wersji zachowywane. Skrypt po faktycznym błędzie nadpisywania e_sqlite3.dll używa robocopy i pomija tę bibliotekę wyłącznie gdy SHA256 jest zgodny.
- **Ostatni potwierdzony LIVE CORE:** SHA256 A7A4F301DB60DF457CA07F969CF30DE79D7D920850E479495E4C7AFB0AD7CB1C, MCP OK, wydanie 02.11.2026 14:03 EAT.
- **Zasady:** nie blokować publikacji z powodu działających zadań; nie powtarzać zaliczonych testów; oceniać wywołania modelu zdroworozsądkowo osobno dla każdego zadania; nie rozbudowywać prewencyjnej infrastruktury odzyskiwania; automatyzacja ChatGPT pozostaje wyłączona.

**Najbliższe rzeczywiste braki funkcjonalne:** produkcyjna dedykowana obsługa wysyłki wsadowej WhatsApp (obecnie DEV candidate), A05 cache treści między niezależnymi zadaniami tylko przy realnej korzyści i poprawnym rozróżnieniu konta/widoku. Priorytety ustalać na podstawie faktycznych zadań, nie spekulacyjnych awarii.

### AUDYT HIGH, 02.11.2026 15:41 EAT: uzupełnienie statusu po A26
- Wcześniejszy checkpoint 15:29 poprawnie potwierdzał działanie DEV MCP (tools/list = 15), ale jego lista priorytetów nie uwzględniała nowszego kodu A27 i trwającej osobnej implementacji odczytu Airbnb. Nie traktować tamtej listy jako bezwzględnie aktualnej.
- Obecny LIVE i STAGING: identyczny SHA256 OpenFill.Core.dll = 4136CFDFD5BA8411BEAEF0813EF858A64CA06C89400DA5BEB656A01B5FAA890F. Jest to wydanie nowsze niż udokumentowane w sekcji A26 (A7A4...). Dokładny zakres wydania 15:04 nie był zapisany w tym planie.
- A27 CurrentTextCaptureCache i integracja PageCaptureService są w SOURCE. Nowa celowana walidacja `dotnet run --project tools/of_imp_smoke/FileFilterSmoke.csproj -- capture-cache-only` zakończona `A27_CURRENT_TEXT_CACHE_6_CHECKS_OK`, exit_code=0. Kontrola dotyczy exact URL/text/hash, okresu ważności i wyłączenia domen wrażliwych. Nie wykonywano dodatkowego testu LIVE cache hit; nie deklarować go jako udowodnionego. Historyczny opis A05 jako całkowicie niewdrożonego cache jest nieaktualny: istnieje ograniczony mechanizm na neutralnych stronach z kontrolą bieżącej treści.
- D03: `AirbnbCalendarReadService` i nowy flag testowy `airbnb-calendar-only` pojawiły się równolegle w SOURCE około 15:12-15:14. Nie modyfikowano ich ani nie deklarowano LIVE DONE. D02 nie posiada udokumentowanego rzeczywistego, autoryzowanego acceptance wysyłki. Dedykowany WhatsApp send_batch nadal DEV candidate, wcześniejsza blokada edycji nie była obchodzona.
- Kontynuować wyłącznie brakujące adaptery i rzeczywiste pomiary, jeśli dostępne są wiarygodne dane i nie trwa równoległa edycja. Nie powtarzać wykonanych A26 ani A27 fixture QA. Automatyzacja ChatGPT nie została uruchomiona.

### 2026-11-02 16:02 EAT: OF wake 000001, D03 odczyt kalendarza Airbnb LIVE
- Wybudzenie: OF-000001-A01-134712, godzina od silnika 2026-11-02 13:47:12 +01:00. CLI `--lite-project-wake status of` na SATANAMD: enabled=true, hasLead=true, minutes=20, sequence=1, rotationPending=false. Zapis eventu 12:53:36 UTC; dueUtc=2026-11-02T13:04:48.2992918+00:00.
- Audyt: A26 zakończone, A27 sprawdzone poprzednio, nie ponawiano testów. D03 istniejący SOURCE: AirbnbCalendarReadService i ToolRegistry `airbnb_read_calendar_prices`, wprowadzony wcześniej przez równoległy proces, nie był zawarty w LIVE (stare Core SHA256 4136CFDFD5BA8411BEAEF0813EF858A64CA06C89400DA5BEB656A01B5FAA890F).
- Jedyny celowany test D03: `dotnet run --project tools/of_imp_smoke/FileFilterSmoke.csproj -- airbnb-calendar-only` => D03_AIRBNB_CALENDAR_READ_5_CHECKS_OK, exit_code=0. Dotnet publish Release APP do staging/of_imp_20261031 => exit_code=0, brak błędów, istniejące ostrzeżenia. DEV zatrzymany przed publikacją i uruchomiony ponownie PID12676, LIVE nie dotykano podczas tej czynności.
- `tools/of_imp_release_live_ALWAYS_20261101.ps1 -CheckOnly` => RELEASE_READY_NO_IDLE_GATE. Kanoniczny release wykonany: BACKUP_OK C:\Users\pboru\OpenFill\app__rollback__before_release_20261102_160148; UPDATE_NOTICE_SENT; UNCHANGED_SQLITE_DLL_SKIPPED; LIVE_RELEASE_OK core_sha256=41A6BC0853A16C8C4CB7CBE70CA6435838AF83A6FFBC89000B42C80AF8D11C6A mcp=True, exit_code=0. Niezależne Get-FileHash LIVE potwierdza identyczny SHA.
- **Status D03 odczyt kalendarza: LIVE GOTOWE jako narzędzie odczytowe, po QA syntetycznym**. Brak potwierdzonego odczytu faktycznego zalogowanego kalendarza Airbnb w tym wydaniu. D03 edycja/zapis cen oraz reconcile pozostają PENDING. Funkcja nie zmienia cen, nie deklaruje kompletności niewidocznych dni.
- WhatsApp send_batch pozostaje DEV candidate; nie powtarzano wcześniej zablokowanych zmian. Nie było wysyłek, edycji cen, nowych automatyzacji ChatGPT ani dodatkowej kampanii testowej.

### 2026-11-02 16:20 EAT: D03 evidence URL/rate-plan compatibility, LIVE DONE
- Poprawiono AirbnbPriceSourceEvidenceGate: pełny, podpisany SHA256 dzienny dowód może pochodzić z https://pl.airbnb.com/multicalendar i innych poprawnych subdomen airbnb.com oraz z /hosting/; nie dopuszcza podszywających domen. Pusty rate_plan jest akceptowany zgodnie z kontraktem planera. Kontrole account/listing/day/currency, timestamp, scope, hash i kompletności pozostają wymagane.
- Celowane QA 8 checks PASS, 0 errors po poprawieniu konfliktu nazwy lokalnej zmiennej CS0136 w dodanym teście. Dotnet publish STAGING exit=0. LIVE release kanoniczny: BACKUP C:\Users\pboru\OpenFill\app__rollback__before_release_20261102_161940, MCP=true, SHA256 368480A14E20AE2BCB9F8A5B876E99F1019FB89B7C23CF94FBF1641C3CC1A36D, exit=0. Potwierdzony osobno hash LIVE. D03 dowód wejściowy kompatybilny DONE.
- Nie ukończono jeszcze zapisu wartości w kalendarzu Airbnb ani kompletnego adaptera action/reconcile; WhatsApp batch send nieudostępniony LIVE. Nie ponawiać tych ośmiu testów bez nowej przyczyny. Szczegóły w checkpoint OF-IMP.

### Aktualizacja 2026-11-04: API/MCP pierwsze, detekcja nadmiernych przebiegów
Zasada obowiązująca: przed zleceniem OpenFill model sprawdza dostępność już podłączonego API/MCP i zakres konkretnej operacji. Jeżeli natywny connector potrafi wykonać zadanie, należy go preferować. Przykład: czytanie i wysyłanie Gmail przez Gmail API/MCP zamiast UI przeglądarki. Browser pozostaje fallback dla brakujących akcji lub wymaganej kontroli wizualnej, bez zakazów opartych wyłącznie na nazwie domeny. Tę zasadę wdrożono w instrukcjach serwera MCP, openfill_start_task i prompt Luny.

W LIVE istnieją FailedCallGuard i NoteLoop. Dodatkowo od 2026-11-04 AgentRunner zapisuje read-streak przy 12 i 24 kolejnych pasywnych odczytach, a TaskRun zapisuje maxReadStreak i jedno task-review dla failed/partial, modelCalls>=60 lub maxReadStreak>=12. To znaczniki audytowe, nie automatyczne ograniczenia; sprawdzać postęp i przyczynę w logach. Raport: docs/OPENFILL_API_MCP_FIRST_LOOP_MONITOR_LIVE_20261104.md.

Główny następny P0 po tych zmianach: domknąć integrację istniejącej ścieżki LocalPlan z zwykłym AgentRunner, bez dublowania PageCapture/SQLite/act_many. LocalPlanModelDevRunner pozostaje #if DEBUG, brak produkcyjnego A/B end-to-end, nie oznaczać DONE. Kolejne kroki wyznaczać z rzeczywistych błędów i porównywalnych zadań, bez szerokiej kampanii testowej. D03 Airbnb ANULOWANE.

### 2026-11-04: P0 stop attribution + mail auth exception
1. Wdrażane jako DONE SOURCE/DEV/LIVE: `stop_reason` i `stop_actor` rozróżniają user_stop, requester_cancel, model_finish/model_partial, timeout, upgrade_interrupted i crash_interrupted. Bez zmiany dotychczasowej zasady nierobienia automatycznego retry niepewnych skutków. Acceptance rzeczywistych zakończeń jako OPEN.
2. API/MCP-first jest preferencją dla zwykłej poczty obsługiwanej przez model zlecający, a nie zakazem dla Luny. Luna w toku logowania może samodzielnie otworzyć mail w przeglądarce i odczytać kod/link/hasło potrzebne w procesie. Opisy MCP i SystemPrompt poprawione w LIVE.
3. Następny krok P0: ocenić rzeczywiste rekordy `stop_reason`, `task-review`, `read-streak` i dopiero na tych danych ustalić politykę kontynuacji oraz następne ograniczenia liczby wywołań. Kolejny P0 architektoniczny: integracja LocalPlan w produkcji, nie powielać kodu.
4. Odsyłacz: docs/OPENFILL_STOP_REASON_AUTHMAIL_LIVE_20261104.md i docs/OPENFILL_TODO_STOP_REASON_AUTH_MAIL_20261104.md.

### 2026-11-04: A22 production opt-in LocalPlan, metryki E03
- [x] Produkcyjny jawny tryb execution_mode=local_plan_v1_draft: zwykły AgentRunner pozostaje domyślny. Model tworzy jeden bounded plan z dopuszczalnym jednym wznowieniem, lokalny interpreter wykonuje nawigację i niezapisany draft formularza, weryfikuje wynik. Bez send/save/submit i bez automatycznego fallback.
- [x] Kompletne metryki: mode, model calls, modelMs, total elapsed, planDomReads, planActionAttempts, stopReason, model outcome, task-review nie myli user_stop z automatycznym błędem modelu.
- [x] Dwukrotna publikacja SOURCE/DEV/LIVE z rollbackiem, obecny Core SHA A3489FFFA8820A47462E0709C87BD5D285C544028E6A3EBEB1F3F15A715618FB, LIVE PID7496.
- [x] Lokalne przetwarzanie rzeczywistych usage: tools/of_efficiency_audit_20261104.ps1, docs/OPENFILL_EFFICIENCY_LIVE_20261104.md, ostatnie 100 rekordów, 38 kandydatów do audytu, 37 rekordów bez outcome rozpoznanych jako niepełne zamiast failed.
- [ ] E2E dla naturalnie zleconego zadania opt-in i porównanie z legacy na tych samych celach. Brak jeszcze potwierdzonej oszczędności produkcyjnej. Nie robić sztucznej szerokiej kampanii testowej.
- [ ] Automatyczny routing do LocalPlan na podstawie pewnych kryteriów i odnotowanej skuteczności. Nie przenosić automatycznie wiadomości, płatności ani formularzy wymagających zatwierdzenia.
- [ ] D01/D02 real batch send acceptance, gdy istnieją zatwierdzone dane odbiorców. D03 Airbnb anulowane.

### 2026-11-04: reguły GitHub i odpowiedzi użytkownika w zadaniach Luny
- [x] GitHub: caller ma używać własnego GitHub connector do operacji repo, commitów, branchy, issue/PR/files. OpenFill nie jest zastępczym konektorem. W MCP reguła opisowa oraz fail-fast use_native_connector, potwierdzony live bez tasku. Wyraźnie żądany browser UI jest wyjątkiem. Nie tworzyć repo ani autosync tylko z powodu tej reguły.
- [x] Karta showAsk Luny: Enter w textarea.note wysyła, Shift+Enter wstawia nową linię, klawisze trzymane i IME zabezpieczone. Node --check PASS, 5 syntetycznych przypadków PASS. Bez automatycznej decyzji w oddzielnej karcie Yes/No.
- [x] Build/DEV/LIVE: Core SHA 09E32B0594EB084357E894CAC687EF6FFE07E0EE5409BF8E10F68DC3FAE11AB8, PID LIVE21020, rollback app__rollback__before_release_20261104_024111. Pełny opis docs/OPENFILL_GITHUB_NATIVE_AND_ASK_ENTER_LIVE_20261104.md.
- [ ] Potwierdzić UX klawiszy na naturalnie otwartej karcie pytania. Inne P0 zgodnie z najnowszym checkpointem, bez sztucznej kampanii testowej.

### 2026-11-04: nadrzędna korekta GitHub + autosync oraz wake, wydanie LIVE
- [x] Zasada GitHub to WYŁĄCZNIE niezobowiązująca sugestia: model zlecający może rozważyć użycie własnego konektora GitHub i/lub dostępnego mechanizmu autosync. Wybór zawsze zależy od zadania i możliwości. Brak wymuszonego routingu, brak zakazu używania OpenFill.
- [x] Usunięto z MCP source blokadę `use_native_connector`; poprzednie sekcje opisujące obowiązkowy GitHub connector i fail-fast są HISTORYCZNE oraz zastąpione. Nie konfigurowano autosync ani nie zmieniano repozytorium GitHub: samo słowo autosync jest częścią sugestii.
- [x] SOURCE/DEV/LIVE: publish exit0; Core SHA256 6187841966432827A8C8E1F282580A1CDB2C085C17634232F2FDE0C4C6544AE2; DEV PID11468; LIVE PID19264, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, scheduled task Running, mcp=True. Rollback C:/Users/pboru/OpenFill/app__rollback__before_release_20261104_025612.
- [x] OpenFill project `of` wake na SATANAMD: `Wake.Enabled=false`; drugi odczyt po czasie potwierdził false, sekwencja 59 nie wzrosła. Backup projects.json.before_of_wake_off_20261104. Nie uruchamiać nowego automatycznego wybudzania bez osobnego polecenia.
- [ ] D01 i D02: zamknięcie REALNEGO skutkowego batch send dopiero na dokładnie określonym i autoryzowanym wsadzie; źródła i LIVE implementacje już istnieją. Nie oznaczać candidate jako verified bez dowodu UI.
- [ ] LocalPlan: produkcyjny E2E i A/B na porównywalnych zadaniach. Jedno testowe zgłoszenie na lokalnym CRM zwróciło JEV rejected_rewrite_needed (nadmiarowe zabezpieczenia w zadaniu), ponowne krótkie wywołanie przez zdalne narzędzie zostało zablokowane przez OpenAI security. Nie ma jeszcze udanego produkcyjnego E2E ani podstawy do auto-routing.
- [ ] Dalsza poprawa cache, metryk i przerwanych operacji: tylko po rzeczywistych wynikach, bez powtarzania ukończonych testów.

## 2026-11-04 17:17: D02 sender-menu, LocalPlan prawdziwe E2E/A-B, cache i audyt
- Źródło prawdy: docs/OPENFILL_OF_20261104_1717_DELIVERY.md.
- D02 LinkedIn: fallback do istniejącego LinkedInAccountMenuProbe tylko gdy nie ma widocznego profilu nadawcy; nigdy nie zastępuje jawnego mismatch. SOURCE/DEV/LIVE. Core SHA 581BAAB48A16B199EF0909529444B2F76B4B2E5560D9D30D3AC0B875DD5960AF.
- LocalPlan: rzeczywisty produkcyjny task OF-20261104-1708-459e, success, 1 call, 4 DOM reads, 1 action, 0.0001403 USD. Standardowy AgentRunner na tym samym lokalnym zadaniu: OF-20261104-1709-edd5, success, 5 calls, 0.001762 USD. E2E tego pojedynczego scenariusza DONE; szerszy auto-routing NIE wdrożony.
- Cache: prefilter zapisanych neutralnych manifestów przed odczytem DOM, realny elapsed_ms. Istniejący capture-cache-only 6/6 PASS; publish/DEV/LIVE PASS. Aktualny LIVE Core SHA C26EA22BA1F141E27EB1A5554A83C81D5A015D1E5839B8EB9F80CC98ADA8458C, PID24396, OpenFill-open Running, rollback app__rollback__before_release_20261104_171703. Brak potwierdzenia fizycznego cache hit z MCP, odrzucenie przez zabezpieczenia narzędzia.
- Audyt: naprawiony ReadLines IOException na pliku usage otwartym przez LIVE. Get-Content działa. REPORT_OK na 100 rekordach: 2788 model calls, 34 review, USD 1.1425, 34 success, 16 partial, 3 failed, 19 legacy cancels, 3 interrupted, 21 missing outcome. Backup skryptu .before_log_shared_read_20261104.
- D01/D02 batch sends candidate pozostają bez produkcyjnego effect acceptance, dopóki nie pojawią się konkretnie autoryzowane odbiorcy i treści. Auto-routing LocalPlan, Google/Meta P2 i E02 nie są zakończone. Airbnb anulowane. GitHub connector/autosync tylko sugestia, nie konfigurować. Wake.Enabled=false pozostaje decyzją właściciela.

## 2026-11-04 20:05 Warsaw, E02/D04/MCP LIVE (finalizacja części technicznej)
- SOURCE: E02 ProcedureLearningStore lifecycle candidate/verified/suspended po pełnych, odrębnych dowodach wsadowych, brak tekstu wiadomości; ProcedureCatalog, ProcedureExecutor, WA/LI integracja. D04 read-only YouTube Studio i Meta Business, 4 candidate procedury, domena i zakres ID, bez publikacji. MCP regresja przyczyn stopu.
- QA: E02 10/10, D04 16/16, WA 19/19, LI 21/21, LocalPlan autoroute 16/16, MCP 212/212: suma 293 zaliczone. Stary test MCP zakładał dokładnie 9 tools: poprawiono na >=9 i wymagane narzędzia, finalna regresja 212/212 PASS.
- dotnet publish Release exit0 staging/of_final_e02_d04_20261104; DEV_RELEASE_OK PID21936. LIVE_RELEASE_OK mcp=True, backup app__rollback__before_release_20261104_200502. Niezależny LIVE PID3756, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, Core SHA C7632AF5A4CE7A691C0CDD827A298DEEE71C8CFEEC6E6F3F4E41BF1DC46223B1, scheduled task Running, OpenFill MCP working=[].
- Rozstrzygnięcia: D01/D02 real send acceptance nadal brak konkretnie autoryzowanych wsadów; nie wysyłać fikcyjnie. YouTube/Meta publikacja candidate tylko preflight, bez dowodu realnych skutków. Naturalny cache hit/ask UI/user stop GUI nadal nieobserwowane. Airbnb anulowane, wake wyłączone, GitHub autosync niewdrożone zgodnie z decyzjami.
- Dokument szczegółowy docs/OPENFILL_OF_20261104_E02_D04_MCP_LIVE.md. Kontynuację wybierać wyłącznie z potwierdzonych braków i rzeczywistych danych.


## 2026-11-04 21:46 CET: A27 default verified recipe cache ACCEPTED
- Natural defect OF-20261104-2138-113a and controls 2141-c825/2141-2479 established: use_best=true selected verified_live_view but bypassed A27 due to expectedViewSignature guard; use_best=false reused correctly.
- SOURCE: PageCaptureService and CurrentTextCaptureCache, both backed up *.before_a27_default_cache_20261104. Added verified view URL/signature checks to reuse after fresh DOM structure/text probe.
- QA: cache smoke 6/6 PASS; Chromium Ask DOM 12/12 PASS (not native WebView2 card acceptance); publish/DEV/LIVE exit 0.
- LIVE PID 19828 Core SHA 713BBD7655410CF0518C9BA12DEB13B86C79F1D9AD09297A5F94470991D266FC, staging SHA identical, OpenFill-open Running, mcp=True, rollback app__rollback__before_release_20261104_214424.
- Natural default task OF-20261104-2144-116f reused same capture ID twice with reused_snapshot=true, exact_live_dom_text_sha256. Local live HTTP content invalidation: OF-20261104-2145-c8b9 Version A reused; OF-20261104-2146-2129 after Version B yielded new ID and new content. Fixture server stopped.
- P1-A27 CLOSED (technical and natural acceptance). Details: docs/OPENFILL_A27_DEFAULT_CACHE_LIVE_20261104.md. Do not re-test without concrete regression. Other backlog states unchanged.


## Aktualizacja 2026-11-04, zamknięty zakres
- P1-A27: DONE. Wdrożono i odebrano naturalny cache hit oraz invalidation. Dowody w docs/OPENFILL_A27_DEFAULT_CACHE_LIVE_20261104.md.
- P1-D04: YouTube rozpoznane rzeczywiste ekrany po dodaniu testowego Short, testowy szkic usunięty; Meta nadal nie obsłużył file picker. Adaptery skutkowe NIE zakończone. Szczegóły w docs/OPENFILL_HANDOFF_CONTINUATION_20261104_2200.md.
- P2-E03: audyt 100 zadań pokazał 3757 calls i konkretną pętlę WhatsApp 111 calls, 11 mismatch. Zmodyfikowano SOURCE act_locator i zaliczono 7/7 smoke JS; publish/DEV/LIVE w trakcie weryfikacji, nie deklarować wdrożenia przed niezależnym SHA.
- Pozostałe: D01/D02 realne sends i E02 awanse wymagają realnych zatwierdzonych wsadów, LocalPlan szerszy test zablokowany przez security MCP, GUI Stop i Ask naturalne odbiory niezakończone.


## 2026-11-04 22:23 CET: E03 act_locator LIVE, final acceptance
- Report: docs/OPENFILL_E03_ACT_LOCATOR_LIVE_20261104.md.
- SOURCE ToolRegistry__v0.22__2026-10-29_2314.cs: bounded observed_text (<=180), observed_count=1 on target_text_mismatch, never performs click. Backup .before_e03_act_locator_mismatch_20261104.
- Real evidence: WhatsApp OF-20261104-1602-5fa9 required 111 calls, 11 act_locator target_text_mismatch. JS extracted from SOURCE: 7/7 PASS. Natural WebView2 task OF-20261104-2223-bf40: target_text_mismatch, action_attempted=false, observed_text present, observed_count=1.
- E03 publish Release and DEV succeeded after transient DEV_COPY_FAILED_9 (Windows ERROR32), diagnosed both source/target DLL no longer locked and reused existing backup. DEV PID22928.
- LIVE_RELEASE_OK mcp=True, rollback C:/Users/pboru/OpenFill/app__rollback__before_release_20261104_222135. Independent LIVE verification tools/of_e03_independent_live_verify_20261104.ps1 PASS, PID23956, EXE=C:/Users/pboru/OpenFill/app/OpenFill.exe, SHA=11D2C9CA555486A8E1E2BBCB215BF8F8827BDDB0D89E2E8946D4440DE3F552D3, phase Running, MCP=True. Historical of_000041_live_verify.ps1 checks an obsolete constant SHA, so LIVE_SHA_UNEXPECTED from that script was not a regression.
- E03 targeted fix and technical acceptance DONE. Do not claim measured WhatsApp cost reduction until a comparable natural task occurs. No changes to D01/D02 sends, D04 external publication or E02 production promotions.


## 2026-11-05: status wykonawczy po wdrożeniach PBOX1
Szczegółowy wynik: docs/OPENFILL_EXECUTION_20261105_FINAL_HANDOFF.md, dane GUI: docs/OPENFILL_PANEL_NATIVE_QA_20261105.json, E03: docs/OPENFILL_EFFICIENCY_AUDIT_20261105.md.
CLOSED (kod i odbiór): D01 WhatsApp self batch z trwałym verified, E02 whatsapp.send_batch verified po 2 realnych wsadach, D04 Meta CDP file chooser i UI ścieżka do harmonogramu, LocalPlan dwupolowy bezpieczny A/B, native panel Stop/Ask/Enter/Shift+Enter komunikaty, D02 probe Me dropdown i testy 21/21, E03 telemetryka i wcześniejszy act_locator mismatch.
NOT VERIFIED EFFECTFUL: D02 wysyłki do rzeczywistego LinkedIn odbiorcy (brak jednoznacznego testowego kontaktu), D04 końcowe publikacje i harmonogramy na prawdziwych kontach (brak zlecenia konkretnych treści i terminów), szersze LocalPlan skutkowe (brak pomiaru różnych klas). GUI Stop sprawdzono jako komunikat native, nie jako zatrzymanie rzeczywistej sesji end-to-end.
Ostatni potwierdzony release: LIVE Core SHA 9FD37B1406BFE7999193AF9ADF3B96A27B1B005B604ADDD31019F3476505F2AC; rollback app__rollback__before_release_20261105_084045. Airbnb D03 anulowane, wake OpenFill wyłączone.

## 2026-11-06 E03: produkcyjne logi i walidacja run_js
- Odbiór produkcyjny: 24 zakończone tasks i 587 wywołań modelu (30 h), 0.218334 USD; raport docs/OPENFILL_E03_PRODUCTION_AUDIT_LIVE_20261106.md.
- Hotspot LinkedIn cancelled run 107 calls, 40 run_js, 29 compactions. Pięć błędów run_js: 2 timeout, 2 syntax i numeryczny code=29, poprzednio ogólny wyjątek. 
- Zmiana E03 SOURCE: run_js waliduje typ/niepusty code, daje precyzyjny błąd i wskazówki dla modelu. Testy 18/18, MCP 212/212 PASS; DEV/LIVE RELEASE_OK, Core SHA 3E25547519A2DDF2CC705153287666712A91C671241CD153B3F752074E150CA2, PID21564, rollback app__rollback__before_release_20261106_131217.
- Zakończona zmiana techniczna. Wpływ na koszty i powtórzenia niepotwierdzony nowym naturalnym A/B: wymaga kolejnych porównywalnych zadań; nie deklarować procentowego zysku. Nie rozszerzać zakresu na LinkedIn sends lub publikacje bez konkretnych zamierzonych skutków.

## 2026-11-06: E03 token priority, production proof and release
- Details: docs/OPENFILL_E03_TOKEN_OPTIMIZATION_LIVE_20261106.md.
- Full tool schema ~43.3k chars/call. Single-host profile cuts unrelated provider definitions to ~32.4k chars (25.1%), safe fallback for cross-host/unqualified tasks. Natural production task OF-20261106-1725-6a74 confirmed profile=single_host and 39/52 exposed tools; outcome success.
- History compactor filters wait/screenshot notes, deduplicates identical search_logs notes, retains action facts. Tests history 372/372 and MCP 212/212 PASS.
- Final LIVE PID 20824, Core SHA 79227A504FB58DD6E7737D47A38EEECF1148FAA82FD59F9499D1D5C5981A020B, rollback app__rollback__before_release_20261106_173231, staging/LIVE sha verified.
- Continue production A/B measurement on comparable tasks; no claimed token savings until measured. Do not optimize elapsed time, wake remains disabled.

## 2026-11-06 token-first deployment policy
- Production interruption: OF-20261106-1723-e793, upgrade_interrupted after 120 calls and 2,228,191 input tokens. Some work may persist, so do not label all tokens wasted.
- Nonurgent releases: prefer idle window to minimize duplicated reasoning after interruption. User explicitly prefers tokens over elapsed time. Canonical ALWAYS script was not changed. Details docs/OPENFILL_E03_TOKEN_OPTIMIZATION_LIVE_20261106.md.

## 2026-11-06: standard E03 kosztu JEDNEJ czynności
- Podstawowa jednostka pomiaru: zweryfikowany cel biznesowy i pojedynczy rzeczywiście wykonany efekt, nigdy sam taskId/runId.
- Wzór: tokeny na akcję = (całe inputTokens + outputTokens z wszystkich połączonych prób i kontynuacji celu) / liczba potwierdzonych skutków. Cache jest częścią input; reasoning częścią output. Niewykonane i niepewne sztuki nie są dzielnikiem.
- Rozdzielaj: strict ledger/live read, potwierdzenie z końcowego opisu UI agenta, nominalny plan. Dla celów mieszanych bez telemetrii fazowej nie przypisuj fikcyjnych kosztów poszczególnym rodzajom akcji.
- Historyczne wyniki, algorytm i metodologiczne ograniczenia: docs/OPENFILL_ACTION_UNIT_ACCOUNTING_20261106.md; szczegóły wszystkich grup w docs/of_action_accounting_20261106/all_business_goals.jsonl. Obowiązuje także przy przyszłych porównaniach modeli, promptów i wersji OpenFill.
- Otwarte P0 telemetryczne: przechowywać pełne założenie celu (typ, liczba sztuk, cel/obiekt), trwale łączyć ponowienia i kontynuacje z tym samym celem, zapisywać każdego potwierdzonego skutku osobno i jego źródło dowodu. Dopiero wtedy można w pełni automatycznie ustalać koszt /1 i /5 na produkcji bez ręcznej rekonstrukcji.
