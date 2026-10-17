# OpenFill: log niewykonanych komend i sposób pracy na plikach

Wersja 0.1, 2026-10-18. Status: sprawdzone połączenie do PBOX1 przez Desktop Commander.

## Nowa zasada nazewnictwa

Nie używamy określenia „rejestr blokad”. Kanoniczny plik w repozytorium: `docs/OpenFill__log_niewykonanych_komend__v0.1__2026-10-18.md`.

Log obejmuje każde nieudane lub niewykonane polecenie: błąd techniczny, odmowę dostępu, nieudaną edycję, awarię narzędzia, przerwanie, wycofaną operację i historyczne niekompletne próby. Potwierdzone odmowy bezpieczeństwa są tylko jednym z typów zdarzeń.

Dla każdego nowego wpisu: data/godzina, komputer, pełna nazwa narzędzia, dokładna komenda bez sekretów, mały czy duży krok, dosłowny wynik, zakres niepowodzenia i to, co pozostało niewykonane. Nie wnioskuj samodzielnie, że niewykonana komenda była niebezpieczna.

## Obowiązujący podział narzędzi

- Pliki źródłowe OpenFill: Remote Desktop Commander, `read_file` / `read_multiple_files`.
- Wyszukiwanie: `start_search` i `get_more_search_results`.
- Zmiana istniejącego pliku: `edit_block`, jedna mała dokładna podmiana na wywołanie.
- Nowe pliki: `write_file`.
- Kompilacja i testy: `start_process` z `dotnet build` / `dotnet run --project ...`, potem `read_process_output`.
- LC MultiHost: tylko operacje systemowe, Harmonogram zadań, procesy, SATANAMD.

## Potwierdzone w tej sesji

- Desktop Commander połączył się z PBOX1 (online).
- Utworzono i odczytano 72-liniowy `docs/OpenFill__log_niewykonanych_komend__v0.1__2026-10-18.md`.
- Uruchomiono testy Release przez Desktop Commander: **771 PASS, 0 FAIL, exit 0**, proces PID 26900.
- Uruchomiono budowę OpenFill.App Release przez Desktop Commander: **0 errors, 1 warning MSB3277, exit 0**, proces PID 26588. Ostrzeżenie dotyczy konfliktu WindowsBase 4.0/5.0 w zależnościach WebView2 i pozostaje otwarte.
- Produkcyjnego OpenFill nie zatrzymywano, nie wykonywano stagingu ani wdrożenia. Nie wykonano pełnego testu recovery/restore i nie zmieniono mechanizmu releaseFinishedTabs.

## Stan dalszy

Nadal otwarte: realna weryfikacja odtworzenia zaszyfrowanego backupu na SATANAMD, OF-239 zastępczy MCP, rzeczywiste GUI/overnight A3, kontrolowany rollback i odbiór produkcyjny. Testy syntetyczne nie potwierdzają wdrożenia.
