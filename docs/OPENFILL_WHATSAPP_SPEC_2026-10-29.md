# WhatsApp Web w OpenFill — 2026-10-29

Wymóg właściciela: **brak Luny Mini**. Jedna Luna, operacje DOM w OpenFill.

- W każdym zadaniu WhatsApp: automatyczny, zwięzły odczyt wszystkich wyrenderowanych podglądów wierszy listy bez otwierania rozmów.
- Przekazywać wszystkie widoczne znaczące dane (tytuł, podgląd, data, status, etykieta, ikonki użytkowe), bez CSS, SVG, pozycji, skryptów i obrazu.
- Otwieranie konkretnej rozmowy wyłącznie po dostarczonym numerze międzynarodowym lub obsługiwanym ID; bez przeszukiwania listy.
- Po otwarciu odczytać zbiorczo **tylko załadowane** wiadomości i dostępne informacje, bez przewijania po historię.
- Wysyłka: otwarcie, wpisanie, wyślij, jeden odczyt wyniku. Kliknięcie nie dowodzi wysyłki.
- Na wyraźne żądanie: odczyt podglądów całej wirtualizowanej listy jako jedna operacja techniczna, bez wchodzenia do rozmów.
- Celem jest minimalizacja tur Luny, tokenów, czasu. Żadnych obrazów w normalnym przepływie.
- Testy real WhatsApp Web: automatyczny DOM listy, dane, open number/ID, loaded messages, wysyłka do autoryzowanego testowego kontaktu, pełna lista, metryki koszt/czas/tury.
