# OpenFill: pre-rejestrowany benchmark wywołań modelu na cel i skuteczną akcję

Data opracowania: 2026-11-07 13:30 CET. Autor: GPT-6. Projekt: OpenFill na PBOX1.
Cel: przed rozpoczęciem testów utrwalić niezmienne prognozy liczby wywołań modelu, potem porównać z rzeczywistymi wywołaniami, tokenami i jakością zakończenia. Bez ingerencji w inne pracujące czaty. Wszystkie zadania są osobnymi zamiarami biznesowymi. Metryki produkcyjne usage i business_goal_*.

| ID | Typ celu | Zadanie i akceptacja | Oczekiwana liczba wywołań modelu (najbardziej prawdopodobna) | Dopuszczalny przedział | Jednostka wyniku |
|---|---|---|---:|---|---|
| B01 | Prosty odczyt nowej strony | https://example.org/, dokładny tytuł i tekst pierwszego akapitu, nic nie zmieniać | 2 | 2 do 3 | 1 odczyt |
| B02 | Strona oficjalna i filtrowanie | https://www.iana.org/domains/reserved, wypisz domeny example.com, example.net i example.org i objaśnij ich cel na podstawie strony | 3 | 2 do 5 | 1 potwierdzona odpowiedź |
| B03 | Dwa źródła na jednym serwisie | Wikipedia EN, strony o Ada Lovelace i Charles Babbage: lata urodzenia i związek z maszyną analityczną, dwie weryfikacje źródła | 5 | 3 do 8 | 1 poprawna synteza z 2 stron |
| B04 | Długi dokument techniczny | docs.python.org/3/library/pathlib.html: podaj dokładne różnice pomiędzy Path.glob(), Path.is_file(), Path.mkdir(), z odwołaniem do opisów, bez zmian | 5 | 3 do 9 | 1 poprawna odpowiedź na 3 podpunkty |
| B05 | Formularz demonstracyjny | https://www.selenium.dev/selenium/web/web-form.html, testowe dane w pole tekstowe, hasło, textarea, dropdown i przycisk submit. Potwierdzenie widoczne na stronie testowej, bez danych osobowych | 7 | 4 do 12 | 1 formularz skutecznie wysłany |
| B06 | Seria 5 wpisów testowych | Lokalny sandbox HTTP na PBOX1, utwórz 5 różnych wpisów o z góry podanych tytułach i treści, sprawdź komplet w aplikacji. Potwierdzenie niezależnym odczytem zapisanego stanu serwera | 11 | 7 do 18 | 5 wpisów, z całkowitego kosztu także koszt /5 |

Zasady oceny: (1) rzeczywiste wywołania modelu dla całego celu i jego kontynuacji, (2) input, cached, uncached, output i USD, (3) wynik success/partial/failed, (4) niezależna ocena poprawności i wykonanych sztuk, (5) odchylenie realnych calls od prognozy, (6) puste pętle, powtarzane odczyty i rozmiar schematu. Gdy cel nieukończony, żadne dzielenie przez planowaną liczbę akcji nie jest wynikiem wykonania.

Tych prognoz nie zmieniać po zobaczeniu wyników. Należy rozdzielić prosty przypadek od wieloetapowego. Bezpieczne zadania na otwartych stronach, testowe dane i własny sandbox, bez publikowania w prawdziwych serwisach społecznościowych i bez rejestracji kont.

## Uzupełnienie przed testem B07, 2026-11-07 godz. 13:43 CET

B07: pięć **kolejnych** wpisów na tej samej lokalnej stronie sandboxowej PBOX1. Pozostawić istniejące pięć, dodać nowe tytuły: Nowe zamowienia, Analiza marzy, Reklamacje, Czas kompletacji, Dostawy kurierskie; treści kolejno „Wpis testowy numer 6.” do „Wpis testowy numer 10.”. **Prognoza przed uruchomieniem: 12 wywołań modelu**, oczekiwany przedział od 8 do 17, cel skutkowy: 5 nowych, potwierdzonych przez niezależny odczyt serwera. Po wykonaniu powinno być 10 wpisów łącznie, bez dublowania pierwszej piątki. Hipoteza: ponowna praca na znanej stronie ograniczy liczbę odczytów, nawigacji i błędów DOM.
