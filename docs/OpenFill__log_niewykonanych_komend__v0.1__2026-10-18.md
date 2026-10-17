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