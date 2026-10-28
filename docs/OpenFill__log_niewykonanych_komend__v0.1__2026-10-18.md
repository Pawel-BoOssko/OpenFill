# OpenFill: log niewykonanych komend

Wersja: 0.1. Utworzono: 2026-10-18. Źródło: dotychczasowe zestawienie ChatGPT v0.2 z 18.10.2026 oraz zachowane komunikaty i handoff.
Cel: dokumentować polecenia, których nie wykonano lub nie dokończono, niezależnie od przyczyny. To NIE jest katalog zagrożeń ani ocena bezpieczeństwa komend.

## Zasady wpisów

- Zapisywać: datę i godzinę (jeśli znane), komputer, pełną nazwę narzędzia, dokładne polecenie albo opis próby, rozmiar operacji, wynik i dosłowny błąd, zakres niewykonanej części i dalszy status.
- Przyczyna jest zapisana tylko wtedy, gdy wynika z komunikatu narzędzia; nie wolno samodzielnie przypisywać nieznanych powodów.
- Typy wyników: ODRZUCONE_PRZEZ_NARZĘDZIE, BŁĄD_TECHNICZNY, NIEUDANE, NIEWYKONANE_Z_INNEJ_PRZYCZYNY, HISTORIA_NIEPEŁNA.
- Nie zapisywać haseł, tokenów, kluczy prywatnych ani zawartości profilu użytkownika. Nie ponawiać odmów bezpieczeństwa pod pozorem obejścia.
- Wpis pozostaje w logu także wtedy, gdy innym razem niezależnie uda się osiągnąć podobny rezultat. Późniejszy sukces dokumentować osobno, nie usuwać poprzedniego niepowodzenia.

## Potwierdzone niewykonane wywołania

### OF-UNEXEC-001: rejestracja czatu i lista komputerów
- Kiedy: 2026-10-17, po checkpoint v0.3; dokładna godzina nieustalona.
- Narzędzie: functions.exec: mcp__lc_multihost__register_chat i mcp__lc_multihost__list_targets (jedna transakcja z dwoma wywołaniami).
- Komputer: PBOX1 (rejestracja); lista urządzeń (odczyt).
- Operacja: rejestracja rozmowy „OpenFill: domknięcie wydania i backlogu”, a następnie odczyt komputerów.
- Wielkość: mała operacja zbiorcza, bez modyfikacji plików.
- Wynik: ODRZUCONE_PRZEZ_NARZĘDZIE; nie uzyskano odpowiedzi potwierdzającej te czynności.
- Dosłowny błąd: „To wywołanie narzędzia zostało zablokowane przez zabezpieczenia OpenAI. Sprawdź dokładniej, co wysyłasz.”
- Zakres: całe wywołanie; nie wiadomo, który fragment wywołał odmowę. Powód szczegółowy: niepodany.

### OF-UNEXEC-002: Get-Date na PBOX1
- Kiedy: 2026-10-17, bezpośrednio po 001; dokładna godzina nieustalona.
- Narzędzie: functions.exec → mcp__lc_multihost__execute_command, target=pbox1.
- Dokładna komenda: Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
- Wielkość: pojedynczy odczyt, bez zmian w systemie.
- Wynik: ODRZUCONE_PRZEZ_NARZĘDZIE; nie wykonano komendy przez ten interfejs.
- Dosłowny błąd: „To wywołanie narzędzia zostało zablokowane przez zabezpieczenia OpenAI. Sprawdź dokładniej, co wysyłasz.”
- Zakres: jedno wywołanie. Szczegółowy powód: niepodany.

## Próby historyczne, dla których brak oryginalnego pełnego komunikatu

| ID | Zadanie / polecenie | Narzędzie i skala | Stan oraz brakujące informacje |
|---|---|---|---|
| OF-UNEXEC-003 | OF-212: modyfikacja PrepareShutdown oraz utrwalania statusu przy błędzie zapisu | Edycja źródeł, zakres nieznany | HISTORIA_NIEPEŁNA: odmowa według handoff v2.0; brak pełnego wywołania, nazwy narzędzia i dosłownego komunikatu |
| OF-UNEXEC-004 | OF-239: próba zmian zastępczego MCP respondera podczas aktualizacji | Edycja serwera, zakres nieznany | HISTORIA_NIEPEŁNA: odmowa według handoff; brak pierwotnych szczegółów |
| OF-UNEXEC-005 | Git commit-tree dla pięciu dokumentów | Polecenie Git, większy pakiet | HISTORIA_NIEPEŁNA: odmowa według handoff, bez dosłownego błędu; nie oznacza awarii Autosync |
| OF-UNEXEC-006 | OF-230: modyfikacja releaseFinishedTabs | Edycja źródeł, rozmiar nieznany | HISTORIA_NIEPEŁNA: według handoff odmowa konkretnej próby, bez treści błędu |
| OF-UNEXEC-007 | OF-117: korekta współdzielonych testowych plików openfill_test_overflow, openfill_test_logs i openfill_panel.png | Zmiana testów, rozmiar nieznany | HISTORIA_NIEPEŁNA: nieznane dokładne polecenie i komunikat |

## Inne nieudane operacje, nieutożsamiane z odmową bezpieczeństwa

| ID | Próba i rezultat | Klasyfikacja |
|---|---|---|
| OF-UNEXEC-008 | local.handoff: accepted=false; „The user chose not to hand off.” | NIEWYKONANE_Z_INNEJ_PRZYCZYNY: brak zgody na przekazanie do Work |
| OF-UNEXEC-009 | Select-String wzorzec ToolRegistry(: błąd niezamkniętego nawiasu | BŁĄD_TECHNICZNY: błędne wyrażenie regularne |
| OF-UNEXEC-010 | Edycja testu: „verified test marker missing” | BŁĄD_TECHNICZNY: niedopasowany fragment źródła |
| OF-UNEXEC-011 | Sesja PowerShell nieoczekiwanie zakończona po teście 728 PASS/0 FAIL | BŁĄD_TECHNICZNY sesji; sam test zwrócił exit_code=0 |
| OF-UNEXEC-012 | Pobranie pliku przez web: DisabledError | BŁĄD_TECHNICZNY / niedostępny kanał; brak dowodu odmowy bezpieczeństwa |
| OF-UNEXEC-013 | Pobranie URL w narzędziu kontenerowym: „download failed because url not viewed in conversation before” | BŁĄD_TECHNICZNY mechanizmu pobierania; później odczytano plik z GitHub |

## Formularz kolejnego wpisu

### OF-UNEXEC-XXX: tytuł
- Kiedy i strefa czasowa:
- Komputer / projekt:
- Narzędzie:
- Dokładna komenda lub operacja (bez sekretów):
- Wielkość: mała / średnia / duża.
- Oczekiwany rezultat:
- Faktyczny rezultat i dosłowny komunikat:
- Co się wykonało, a co pozostało niewykonane:
- Zasięg błędu i potwierdzona przyczyna (jeśli podana):
- Link do logu źródłowego lub checkpointu:
- Dalszy status:

## Sposób pracy
Dla plików OpenFill: Desktop Commander read_file/read_multiple_files, start_search, edit_block (jedna zmiana na wywołanie), write_file, start_process i read_process_output. lc-multihost tylko do czynności systemowych (zadania harmonogramu, procesy, SATANAMD).

## Uzupełnienie 18.10.2026: nieudane komendy przygotowujące pakiet

### OF-UNEXEC-014: nieudany zapis pierwszej poprawki generatora pakietu
- Komputer: odizolowane środowisko robocze ChatGPT, nie PBOX1.
- Narzędzie: container.exec; mały krok lokalnej edycji tekstu.
- Operacja: zastąpienie w generatorze ZIP oczekiwanego fragmentu odpowiedzialnego za nazwę skryptu dopisywania do logu.
- Wynik: BŁĄD_TECHNICZNY, `needle-not-found`. Oczekiwanego fragmentu nie znaleziono. Zmiana nie została zastosowana w tej próbie.
- Powód: niedopasowanie tekstu w samym skrypcie generatora, nie odmowa bezpieczeństwa.
- Dalszy status: poprawiono generator i odtworzono paczkę.

### OF-UNEXEC-015: nieudana walidacja pierwszej wersji ZIP v0.3
- Komputer: odizolowane środowisko robocze ChatGPT, nie PBOX1.
- Narzędzie: container.exec; mały lokalny test zawartości ZIP.
- Operacja: asercja poprawności odwołania instalatora do źródłowego pliku logu.
- Wynik: BŁĄD_TECHNICZNY, `AssertionError`, kod zakończenia 1. Test nie przeszedł.
- Powód: niespójna nazwa pliku źródłowego w początkowej wersji generatora; szczegóły sprawdzono w treści instalatora.
- Dalszy status: poprawiono ścieżkę `$sourceName` oraz `$logName`, przebudowano ZIP; ponowna walidacja nazw i CRC zakończyła się powodzeniem. Nie uruchamiano instalatora na komputerze użytkownika.

### OF-UNEXEC-016: wyszukiwanie nazw plików w `docs` nie zwróciło istniejących dopasowań
- Kiedy: 2026-10-18, rano; dokładna godzina nieustalona.
- Komputer: PBOX1.
- Narzędzie: mcp__Remote_Desktop_Commander__start_search / get_more_search_results.
- Komenda: `start_search(path=...\\OpenFill\\docs, searchType=files, pattern="checkpoint|log_niewykonanych|handoff", earlyTermination=false, maxResults=100)`.
- Wielkość: małe, tylko odczyt.
- Wynik: NIEUDANE WYSZUKIWANIE. Raport narzędzia: `Status: COMPLETED; Total results: 0 matches; No matches found`.
- Rzeczywisty stan: `list_directory` wykazał pliki zawierające wymienione fragmenty nazw. Wyszukiwanie nie posłużyło jako dowód ich braku.
- Zakres: jedno wyszukiwanie plików. Przyczyna niedopasowania: niepotwierdzona, bez komunikatu o odmowie bezpieczeństwa.
- Dalszy status: kontynuowano niezależny przegląd istniejących plików przez `list_directory`.

### OF-UNEXEC-017: GitHub nie zawierał jeszcze nowego checkpointu v0.4
- Kiedy: 2026-10-18, rano, po utworzeniu checkpointu.
- Narzędzie: mcp__GitHub__fetch_file, odczyt zdalny, mały krok.
- Żądanie: `repository_full_name=Pawel-BoOssko/OpenFill`, `path=docs/OpenFill__implementation_checkpoint__v0.4__2026-10-18.md`, `ref=autosync/PBOX1`.
- Wynik: NIEUDANY ODCZYT, `NOT_FOUND: GitHub API error 404: {"message":"Not Found","status":"404"}`.
- Zakres: brak potwierdzenia publikacji tego jednego nowego pliku; zapis lokalny przez Desktop Commander był poprawny.
- Przyczyna: GitHub nie udostępniał pliku pod wskazanym ref w chwili zapytania; nie stwierdzono błędu zabezpieczeń ani awarii Autosync.
- Dalszy status: oczekiwanie na normalny cykl Autosync, bez ręcznego push.

### OF-UNEXEC-018: niezapisany skrypt raportu telemetrii z powodu błędu składni wywołania
- Kiedy: 2026-10-18, bieżąca sesja.
- Narzędzie: functions.exec, kod JavaScript przygotowujący mcp__Remote_Desktop_Commander__write_file.
- Operacja: utworzenie `tools/telemetry_window_quality__v0.1__2026-10-18_ChatGPT.ps1` na PBOX1.
- Skala: jeden nowy plik narzędziowy; bez modyfikacji istniejących plików.
- Wynik: BŁĄD_TECHNICZNY po stronie przygotowania wywołania: `SyntaxError: Unexpected token ':'`. Polecenie write_file nie zostało wywołane, więc plik nie powstał z tej próby.
- Zakres: wyłącznie ta próba nowego pliku; brak informacji o odmowie bezpieczeństwa.
- Dalszy status: NAPRAWIONE. Nowy plik telemetry_window_quality został utworzony na PBOX1, ma 12 PASS/0 FAIL w syntetycznym teście i został odczytany z gałęzi Autosync na GitHub.

### OF-UNEXEC-019: nieudany odczyt drzewa plików repozytorium z GitHub
- Kiedy: 2026-10-18, bieżąca sesja.
- Narzędzie: mcp__GitHub__fetch, pojedynczy odczyt metadanych, bez modyfikacji.
- Żądanie: `https://api.github.com/repos/Pawel-BoOssko/OpenFill/git/trees/autosync%2FPBOX1?recursive=1`.
- Wynik: BŁĄD_TECHNICZNY / INVALID_ARGUMENT, `HTTPError: 400: GitHub Fetch URL contains an invalid repository path`.
- Zakres: nie uzyskano listy śledzonych plików do audytu OF-187; inne pliki projektu i produkcja bez zmian.
- Przyczyna: format URL niezaakceptowany przez akcję GitHub; nie jest to potwierdzona odmowa bezpieczeństwa.
- Dalszy status: NAPRAWIONE W ODRĘBNYM AUDYCIE. Metadane tej samej gałęzi pobrano później dozwolonym, wyłącznie odczytowym git ls-tree przez Desktop Commander (225 plików); pierwotna próba GitHub fetch pozostaje w logu jako nieudana.

### OF-UNEXEC-020: nieudane przygotowanie protokołu wydania
- Kiedy: 2026-10-18, bieżąca sesja.
- Narzędzie: functions.exec, przygotowanie argumentu mcp__Remote_Desktop_Commander__write_file.
- Operacja: nowy plik docs/openfill_release_evidence__v0.2__2026-10-18_ChatGPT.md; jedna samodzielna operacja.
- Wynik: BŁĄD_TECHNICZNY w kodzie wywołującym: `TypeError: ... is not a function` (nieprawidłowo zinterpretowany znak w szablonie tekstu). Remote write_file nie zostało wywołane.
- Zakres: ten nowy dokument nie powstał w tej próbie. Brak podstaw do określenia błędu jako odmowy bezpieczeństwa.
- Dalszy status: NAPRAWIONE. Utworzono nowy plik docs/openfill_release_evidence__v0.2__2026-10-18_ChatGPT.md, zawierający protokół NO-GO z dowodami TEST/BUILD i brakującymi bramkami wydania.

### OF-UNEXEC-021: zdalny odczyt świeżo utworzonej Technology Map
- Kiedy: 2026-10-18, bieżąca sesja.
- Narzędzie: mcp__GitHub__fetch_file; pojedynczy odczyt zdalnej gałęzi, bez modyfikacji.
- Operacja: `repository_full_name=Pawel-BoOssko/OpenFill`, `path=docs/openfill_technology_map__v0.1__2026-10-18_ChatGPT.md`, `ref=autosync/PBOX1`.
- Wynik: NIEUDANY ODCZYT, `NOT_FOUND: GitHub API error 404 (Not Found)`.
- Zakres: nowa mapa istniała na PBOX1, lecz w chwili odczytu nie było potwierdzenia publikacji tego pliku zdalnie.
- Szczegółowa przyczyna: niepotwierdzona; nie jest to odmowa zabezpieczeń.
- Dalszy status: sprawdzić po kolejnym normalnym cyklu Autosync, bez ręcznego push.

### OF-UNEXEC-022: brak nowego checkpointu v0.6 na GitHub w chwili kontroli
- Kiedy: 2026-10-18, po zapisaniu checkpointu v0.6.
- Narzędzie: mcp__GitHub__fetch_file, pojedynczy odczyt, bez zmian w repozytorium.
- Operacja: `repository_full_name=Pawel-BoOssko/OpenFill`, `path=docs/OpenFill__implementation_checkpoint__v0.6__2026-10-18_ChatGPT.md`, `ref=autosync/PBOX1`.
- Wynik: NIEUDANY ODCZYT, `NOT_FOUND: GitHub API error 404 (Not Found)`.
- Zakres: plik v0.6 jest zapisany lokalnie na PBOX1, ale brak potwierdzenia zdalnej publikacji. Nie oznacza awarii aplikacji lub wydania.
- Szczegółowa przyczyna: niepotwierdzona; nie stwierdzono blokady bezpieczeństwa.
- Dalszy status: nie wykonywać ręcznego push, poczekać na prawidłowy cykl Autosync i zweryfikować konkretny plik.

### OF-UNEXEC-023: odmowa przemianowania pliku testowego OF-231
- Kiedy: 2026-10-18, około 08:43 CEST.
- Komputer: PBOX1.
- Narzędzie: `mcp__Remote_Desktop_Commander__move_file`.
- Dokładna operacja: `source=tests\OpenFill.Tests\TabTests__v0.6__2026-10-13_1035.cs` do `destination=tests\OpenFill.Tests\TabTests__v0.7__2026-10-18_0843.cs`.
- Wielkość: mały, samodzielny krok, samo przemianowanie jednego pliku bez zmiany zawartości.
- Wynik: ODRZUCONE_PRZEZ_NARZĘDZIE; komunikat dosłowny: „To wywołanie narzędzia zostało zablokowane przez zabezpieczenia OpenAI. Sprawdź dokładniej, co wysyłasz.”
- Zakres: przemianowanie NIE ZOSTAŁO WYKONANE; brak jakichkolwiek zmian testów niestabilnych w tej próbie. Nie wiadomo, dlaczego narzędzie odrzuciło tę operację.
- Dalszy status: nie ponawiać tego samego ruchu przez inny interfejs; pominąć punkt 5 w tym przebiegu i przejść do punktu 6.

### OF-UNEXEC-024: odmowa dopisania checkpointu A3 do logu autonomicznego
- Kiedy: 2026-10-18, około 08:5x CEST (dokładna godzina nieodczytana).
- Narzędzie: `mcp__Remote_Desktop_Commander__write_file`.
- Komputer: PBOX1.
- Dokładna operacja: `mode=append`, `path=docs\openfill_autonomous_log__v0.1__2026-10-18.md`, treść: wynik testów A3 (155 PASS/0 FAIL), statusy OF-225/226/228/229/234, dalsze A7/A9/A10.
- Wielkość: mały samodzielny zapis jednego punktu do istniejącego dokumentu.
- Wynik: ODRZUCONE_PRZEZ_NARZĘDZIE. Komunikat dosłowny: „To wywołanie narzędzia zostało zablokowane przez zabezpieczenia OpenAI. Sprawdź dokładniej, co wysyłasz.”
- Zakres: nie dopisano tej linijki do autonomous_log. Wcześniejsze testy 155/0 i zmiany statusów w backlogu zostały wykonane niezależnie i pozostają zachowane.
- Dalszy status: nie ponawiać równoważnego zapisu innym narzędziem; przejść do kolejnych zadań.

### OF-UNEXEC-025: odmowa aktualizacji ścieżki testu gotowości
- Kiedy: 2026-10-18, po punkcie A3.
- Komputer: PBOX1.
- Narzędzie: `mcp__Remote_Desktop_Commander__edit_block`.
- Operacja: pojedyncza zmiana w `tools\openfill_release_gate_readonly__v0.1__2026-10-17_1831.ps1`, zamiana stałej ścieżki do `McpTaskManager__v0.17__2026-10-14_2235.cs` na wyszukiwanie aktualnego `McpTaskManager__v*.cs`.
- Wielkość: mały samodzielny krok, jedna podmiana w skrypcie diagnostycznym, nie modyfikował danych użytkownika.
- Wynik: ODRZUCONE_PRZEZ_NARZĘDZIE; dosłowny komunikat: „To wywołanie narzędzia zostało zablokowane przez zabezpieczenia OpenAI. Sprawdź dokładniej, co wysyłasz.”
- Zakres: skrypt pozostał ze starą ścieżką i jego wynik SOURCE_PRESENT byłby nieaktualny. Nie uruchomiono go. Przyczyna szczegółowa odmowy niepodana.
- Dalszy status: bez ponowienia tej zmiany innym narzędziem; przejście do A9/A10.



### OF-UNEXEC-026: odmowa dostępu do zmiany zadania Harmonogramu

- Kiedy: 2026-10-18, około 14:40 CEST.
- Komputer: PBOX1.
- Narzędzie: `mcp__lc_multihost__execute_command`, pojedyncza operacja systemowa w Harmonogramie zadań Windows.
- Cel i polecenie: utworzenie `New-ScheduledTaskAction -Execute 'powershell.exe' -Argument '-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File "C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\tools\run_openfill_docs_sync__v0.1__2026-10-18_1440.ps1"'`, następnie `Set-ScheduledTask -TaskName 'OpenFill_Docs_GDrive_Sync' -Action $act -ErrorAction Stop`.
- Wielkość: jedna mała zmiana jednej akcji istniejącego zadania, bez dotykania `data\stable`.
- Wynik: NIEUDANE Z POWODU UPRAWNIEŃ. Proces zdalnej powłoki zwrócił `exit_code=1`, a system PowerShell: `Odmowa dostępu.` (Access denied). Zewnętrzny interfejs zgłosił też `INVALID_ARGUMENT` po niepowodzeniu wewnętrznej komendy.
- Zakres: Harmonogram nie zmienił polecenia ani wyzwalacza, co sprawdzono odczytem `Get-ScheduledTask`; nadal działa stary skrypt co 15 minut w widocznym PowerShell. Brak informacji o odmowie zabezpieczeń OpenAI; potwierdzona odmowa uprawnień Windows.
- Dalszy status: NIE PONAWIAĆ tej samej zmiany innym narzędziem ani przez alternatywne polecenie. Przygotować jeden krok uruchamiany ręcznie przez właściciela w podwyższonym PowerShell. Niezależne od tego launcher i jego logi mogą być weryfikowane odczytem/testem bez modyfikowania zadania.

### OF-UNEXEC-027: nowy raport diagnostyczny niewidoczny jeszcze na GitHub

- Kiedy: 2026-10-18, po zapisaniu raportu OF-240.
- Narzędzie: `mcp__GitHub__fetch_file` (wyłącznie odczyt, mały krok).
- Próba: `repository_full_name=Pawel-BoOssko/OpenFill`, `path=docs/OpenFill__powershell_popup_diagnostic__v0.1__2026-10-18.md`, `ref=autosync/PBOX1`.
- Wynik: NIEUDANY ODCZYT: `NOT_FOUND / GitHub API 404 Not Found` w chwili zapytania.
- Zakres: raport istnieje lokalnie na PBOX1, ale nie potwierdzono obecności na zdalnej gałęzi. Jednocześnie potwierdzono publikację planu `docs/OpenFill__next_action_plan__v1.0__2026-10-18.md`.
- Przyczyna: brak potwierdzonej synchronizacji tego konkretnego pliku w tej chwili; brak dowodu blokady bezpieczeństwa. Nie podejmowano ręcznego push.

### OF-UNEXEC-028: błędna składnia polecenia inwentaryzacji metadanych
- Kiedy: 2026-10-18 około 14:56 CEST. Narzędzie: mcp__lc_multihost__execute_command, PBOX1.
- Operacja: pojedynczy read-only PowerShell: porównanie czasu modyfikacji, rozmiaru i SHA-256 czterech plików w local docs i H:\OpenFill\docs.
- Rezultat: BŁĄD SKŁADNI PowerShell: `An empty pipe element is not allowed`. Pierwsze polecenie nie dało wyniku, nie zmieniło plików. Skorygowana samodzielna komenda później wykazała starsze kopie w Drive. Nie była to odmowa bezpieczeństwa.

### OF-UNEXEC-029: błędna składnia kodu przygotowującego test synchronizatora
- Kiedy: 2026-10-18 około 15:00 CEST. Narzędzie: functions.exec, przed mcp__Remote_Desktop_Commander__write_file.
- Operacja: przygotowanie nowego `tools/test_sync_openfill_docs_gdrive__v0.1__2026-10-18.ps1`, mały pojedynczy nowy plik.
- Rezultat: `SyntaxError: Unexpected identifier 'n'` w JavaScript przed wywołaniem Desktop Commandera. W tej próbie nie zapisano pliku. Po korekcie składni nowy test powstał, zakończył się 11 PASS/0 FAIL. Brak odmowy bezpieczeństwa.

### OF-UNEXEC-030: niedozwolona zmienna HOME w pomocniczym odczycie statusu
- Kiedy: 2026-10-18 około 15:02 CEST. Narzędzie: mcp__lc_multihost__execute_command, PBOX1.
- Operacja: pojedyncza diagnostyka read-only stanu harmonogramu i folderu archiwum na dysku H:, omyłkowo użyto zmiennej `$home`.
- Rezultat: `Cannot overwrite variable HOME because it is read-only or constant.` Nie zmieniło dokumentów. Zmienna poprawiona na `$dh` w odrębnym dozwolonym odczycie: 9 plików archiwalnych, zadanie Harmonogramu exit 0, lokalny i Drive backlog miały identyczny hash. Brak odmowy bezpieczeństwa.

### OF-UNEXEC-031: pierwsza wersja testu instalatora zgłosiła błąd
- Kiedy: 2026-10-18 około 16:55, PBOX1.
- Narzędzie: Desktop Commander `start_process`, pojedynczy test `tools/test_install_controlled_shutdown__v0.1__2026-10-18_1655.ps1`.
- Próba: weryfikacja statyczna, że instalator v0.13 czeka 25 s i nie stosuje `Stop-Process -Force`.
- Wynik: nieudany test harness (nie aplikacji), `FAIL True`, z powodu niewłaściwej konstrukcji tablicy warunków w PowerShell. Brak odmowy bezpieczeństwa.
- Działanie: skorygowano konstrukcję testu przez osobne wywołania `Check`; powtórzenie zakończyło się **10 PASS, 0 FAIL**. Produkcja niezmieniona.

### OF-UNEXEC-032: nieudany odczyt skryptu odzyskiwania na SATANAMD
- Kiedy: 2026-10-18 około 16:55, SATANAMD.
- Narzędzie: Desktop Commander `read_file`, pojedynczy odczyt istniejącego pliku.
- Operacja: odczyt przypuszczalnej ścieżki `Documents\\OpenFill-Backups\\EncryptedState\\restore_openfill_portable_age__v0.1__2026-10-16_1216.ps1`.
- Wynik: `ENOENT: no such file or directory`, ścieżka i wariant były inne niż przewidywane. Brak odmowy zabezpieczeń i brak zmian w danych.
- Działanie: znaleziono rzeczywisty istniejący skrypt `Documents\\Projects\\OpenFill-Recovery\\tools\\restore_openfill_portable_age__v0.1__2026-10-16_1221.ps1`; odczytano przed użyciem; rzeczywisty restore tego wariantu zakończył się `RESTORE_VERIFIED`, 8864 pliki, 0 niezgodności SHA-256.

### OF-UNEXEC-033: nowy checkpoint v1.0 nie był jeszcze widoczny w pierwszym odczycie GitHub
- Kiedy: 2026-10-18 około 17:04.
- Narzędzie: `mcp__GitHub__fetch_file`, pojedynczy odczyt, bez mutacji.
- Żądanie: `repository_full_name=Pawel-BoOssko/OpenFill`, `path=docs/OpenFill__implementation_checkpoint__v1.0__2026-10-18_1715.md`, `ref=autosync/PBOX1`.
- Wynik: `NOT_FOUND / GitHub API 404 Not Found` w chwili kontroli. Lokalny checkpoint na PBOX1 został zapisany poprawnie.
- Zakres: brak potwierdzenia zdalnego występowania tego jednego nowego pliku w momencie wywołania; nie jest to odmowa OpenAI ani dowód awarii Autosync.
- Dodatkowo: w tej samej kontroli nowe pliki `tools/install__v0.13__2026-10-18_1650.ps1` i `tools/test_install_controlled_shutdown__v0.1__2026-10-18_1655.ps1` były już potwierdzone na zdalnej gałęzi. Nie wykonywano ręcznego push.

### OF-UNEXEC-034: błąd składni przed pierwszą próbą zmiany TabTests
- Data: 2026-10-23, około 11:25, PBOX1.
- Narzędzie: `functions.exec` (JavaScript orchestrator), próba przygotowania przeniesienia `TabTests__v0.6` do v0.7 i aktualizacji testu.
- Wynik: `SyntaxError: Invalid or unexpected token` z powodu błędnego delimitera wielowierszowego tekstu w JavaScript. **Żadna z komend Remote Desktop Commander w tym wywołaniu nie została wysłana**; nie była to odmowa zabezpieczeń.
- Korekta: poprawny skrypt wywołał Desktop Commander `move_file` i osobne `edit_block`; test checkout został zapisany, rozwiązanie skompilowane, pełna regresja 1200 PASS / 0 FAIL. Nie doszło do obejścia odmowy bezpieczeństwa.

### OF-UNEXEC-035: błąd składni w przygotowaniu pierwszej wersji planu (bez operacji na plikach)
- Kiedy: 2026-10-23 około 13:00 Europe/Warsaw. Narzędzie: functions.exec (V8), przed wysłaniem Desktop Commander.write_file.
- Operacja: złożenie treści pełnego planu Markdown i ścieżki docelowej.
- Wynik: SyntaxError: Invalid or unexpected token (błędny literał ścieżki w JS). W tym wywołaniu nie powstał żaden plik. Nie była to odmowa bezpieczeństwa.
- Później: poprawiono składnię, Desktop Commander.write_file utworzył plan v2.0, odczyt metadanych i końca pliku potwierdził 393 wiersze, 75 zadań. Nie obchodzono zabezpieczeń.

## 2026-10-28, MCP status: odmowa zmiany opisów narzędzi
- Narzędzie: Remote Desktop Commander edit_block.
- Operacja: zmiana tekstu opisu openfill_task_status i instrukcji MCP w McpServer__v0.24 w celu ograniczenia wielokrotnego polling co 30 s.
- Wynik: BLOCKED, dokładny komunikat: „To wywołanie narzędzia zostało zablokowane przez zabezpieczenia OpenAI. Sprawdź dokładniej, co wysyłasz.”
- Przyczyna szczegółowa: nieudostępniona przez narzędzie; sprawdzono tylko komunikat blokady. Nie ponawiano operacji inną drogą.
- Dalsze niezależne kroki: usunięcie wybudzania statusu na zwykłym ActivityVer, testy jednostkowe, raport. Teksty opisu pozostały bez zmian.

## 2026-10-28, próba podniesienia numeru wydania do 1.58
- Narzędzie: Remote Desktop Commander edit_block.
- Operacja: Directory.Build.props, zmiana wartości OpenFillVersion 1.57 -> 1.58.
- Wynik: BLOCKED, dokładny komunikat: „To wywołanie narzędzia zostało zablokowane przez zabezpieczenia OpenAI. Sprawdź dokładniej, co wysyłasz.”
- Sprawdzona przyczyna: zabezpieczenia po stronie wywołania narzędzia; szczegółowa reguła nie została ujawniona. Nie podejmowano próby obejścia blokady.
- Wcześniejsza czysto dokumentacyjna modyfikacja metadanych została cofnięta. Testy SOURCE 1227 PASS/0 FAIL, produkcja niezmieniona.

## 2026-10-28: odmowa aktualizacji zasady publikowania wersji
- Narzędzie: Remote Desktop Commander edit_block, plik docs/OpenFill__plan_dalszych_prac__v2.0__2026-10-23_1259.md.
- Operacja: zastąpienie zasady wstrzymywania wydania przy aktywnych zadaniach regułą publikacji ASAP z kontrolowanym maintenance, utrwalonym interrupted i rollbackiem.
- Wynik: BLOKADA narzędzia, komunikat: „To wywołanie narzędzia zostało zablokowane przez zabezpieczenia OpenAI. Sprawdź dokładniej, co wysyłasz.”
- Szczegółowy powód odmowy: nieudostępniony. Nie ponowiono edycji alternatywną metodą. Treść planu pozostała niezmieniona. Reguła właściciela jest znana z rozmowy, ale wymaga nadal zapisu do instrukcji projektu.

## 2026-10-28: wydanie MCP z 1227 PASS/0 FAIL
- Kod instalatora 0, controlled close/publish wykonane, lecz postinstall task OpenFill-open zwróciło 0x8007042B i serwer MCP nie odpowiadał. Po rollbacku ta sama metoda uruchamiania zawiodła dla starej wersji. Start bezpośredni przywróconej 1.57 działa i port MCP wrócił.
- Przyczyna pierwotna zakończeń z Harmonogramu: NIEUSTALONA. Sprawdzone: konfiguracje OpenFill-open/OpenFill, AC BatteryStatus 2, stan procesu, TCP, odpowiedzi MCP, Application/WER oraz dziennik TaskScheduler/Operational wyłączony.
- Wydanie nowego SOURCE pozostaje niewykonane; staging gotowy w C:\Users\pboru\OpenFill\staging\candidate-mcp-20261028. Nie ponawiać ślepo.
