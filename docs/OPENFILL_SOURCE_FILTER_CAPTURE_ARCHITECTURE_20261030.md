# OpenFill 1.57+: architektura przetwarzania danych przed Luną
Stan: SOURCE IMPLEMENTATION, NO LIVE DEPLOYMENT. 30.10.2026.

## Cel
Odsiewanie niepotrzebnej zawartości przed wywołaniem modelu, uniknięcie pętli technicznych, zapis i ponowne użycie sprawdzonych filtrów dla powtarzających się witryn.

## Etapy
1. Widok WebView2/DOM: użycie istniejącego BrowserController i sesji zalogowanego użytkownika.
2. Plik: pełny zrzut dostępnego DOM/tekstu/elementów, z metadanymi, atomowy zapis, hashe i wielkość.
3. Filtr: deterministyczny tekst/regex, selektor CSS oraz narzędzia terminala do masowej analizy.
4. Luna: tylko kompaktowy manifest, dopasowania i odnośniki do pełnego materiału; model może rozszerzyć zakres.
5. Pamięć metod: per host/ścieżka/typ operacji zapis sposobu i skuteczności, licznik powtórzeń i czasu, selektor oraz filtr; nie zapisujemy prywatnej treści strony jako przepisu.
6. Dostęp przez MCP: docelowo do tej samej usługi bez uruchamiania Luny. Interfejs MCP musi zachować reguły tożsamości, domen i dostępu do kart.

## Zakres implementacyjny
- Jeden PageCaptureService dla trybów text/elements/html/all, jeden odczyt JS i bezpośredni zapis C#.
- run_js: opcjonalne save_to; jawny tryb expression/statements. Bez symulowania pobierania Blob.
- terminal_exec: jedno uruchomienie polecenia PowerShell/Python/rg/jq, stdout/stderr/exit, limit czasu, duży wynik poza modelem.
- shared_save: kopiowanie z własnego overflow oraz dotychczasowe from_download do rzeczywistych plików.
- LoopGuard: ten sam kod narzędzia + argumenty + błąd => blokada trzeciej identycznej próby, reset po zmianie skutku.
- Procedury filtrowania: odrębny katalog od luźnych SiteNotes, automatyczne utrwalenie tylko potwierdzonych capture, selektywne odnawianie.
- Domyślnie nie wysyłać do Luny całego dużego zrzutu, nawet gdy użytkownik żąda pełnego eksportu. Pełna treść pozostaje w pliku.

## Warunki akceptacji
1. Statyczne fixture HTML: czysty tekst, elementy, HTML i filtr, odczyt DOM bez zbędnego CSS/SVG.
2. Zapis trzech plików i SHA-256, zerowe użycie mechanizmu wielokrotnych pobrań WebView2.
3. Wynik modelowy kompaktowy, pełny wynik w pliku bez utraty bajtów.
4. Powtarzalny filtr i ponowne zastosowanie do tej samej witryny.
5. Pętla dwóch identycznych błędów nie wykonuje trzeciego wywołania.
6. Terminal: udane polecenie, stderr + exit code, timeout, duży wynik do pliku.
7. Regresja całego SOURCE bez nowych błędów, izolowany end-to-end aplikacji.
8. LIVE niezmienione do wyraźnego etapu deployment/approval.

## Ryzyka i rozwiązania
- Utrata danych przez filtr: zachować źródło, umożliwić selektywny ponowny odczyt.
- Stale DOM: manifest timestamp/URL, stan ładowania, informacje o zakresie widocznego/załadowanego DOM.
- Nadmiar pamięci procedur: zapis tylko parametrów technicznych z udanego wykonania, identyfikacja per host/path/mode.
- Interakcyjny DOM: get_page zachowuje istniejący tryb do formularzy i kliknięć.
- Proste zadania: unikać generowania wielokrokowego JS przez Lunę.
