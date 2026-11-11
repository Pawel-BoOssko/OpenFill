# OpenFill: koszty celów i akcji, diagnostyka historyczna

Stan danych: snapshot 2026-11-06, wejściowe dane z całego archiwum 5 października do 6 listopada.
Wszystkie wartości obejmują tokeny wejścia (również cache) oraz wyjścia.
Jednostki wykonane: tylko udokumentowane w ledgerze, odczycie live lub zaraportowane przez agenta. Dowód z raportu agenta nie zastępuje niezależnego sprawdzenia.

## Rozkład tokenów na jedną opisaną akcję

| Typ akcji | Usługa | Zadań z efektami | Potwierdzone/zgłoszone jednostki | Mediana tokenów na efekt | P90 |
|---|---|---:|---:|---:|---:|
| invitation | linkedin | 32 | 188 | 333 466 | 753 856 |
| single_page_read | other | 22 | 22 | 11 864 | 33 367 |
| publication | instagram | 5 | 7 | 1 211 435 | 1 576 297 |
| message | whatsapp | 4 | 4 | 120 690 | 345 363 |
| publication | linkedin | 3 | 3 | 381 970 | 1 252 267 |
| publication | other | 3 | 6 | 1 468 987 | 1 558 922 |
| publication | facebook | 2 | 4 | 1 380 235 | 2 233 746 |
| message | linkedin | 2 | 2 | 571 180 | 697 510 |
| form_or_update | whatsapp | 2 | 2 | 20 074 | 20 323 |

## Koszt nierozstrzygniętych celów

- Cele z policzalną liczbą efektów: 77.
- Cele bez ustalonej liczby wykonanych efektów: 1649.
- Tokeny przypisane celom bez określonego efektu: 1 615 363 366. Nie jest to automatycznie liczba tokenów zmarnowanych.

Najdroższe nierozstrzygnięte grupy według zużycia tokenów:

| Klasa | Outcome | Wywołania modelu | Tokeny | Krótki opis celu |
|---|---|---:|---:|---|
| research | failed | 285 | 23 076 809 | Przejrzyj w Gmail https://mail.google.com konto Skakanka skakanka.centrum@gmail.com, używając istniejącej |
| other | partial | 241 | 18 516 412 | Konto LinkedIn Pawła Boruta-Ośko. WAŻNE: aktywne konto weryfikuj WYŁĄCZNIE w menu „Me” (nazwa przy zdjęci |
| research | partial | 180 | 14 712 538 | TYLKO ODCZYT. Nic nie wysyłaj, nie archiwizuj, nie usuwaj, nie oznaczaj jako przeczytane, jeśli da się te |
| research | success | 193 | 12 077 655 | Konto LinkedIn Pawła Boruta-Ośko (profil /in/pawel-boruta-osko/); sprawdź aktywne konto, nic nie rób z in |
| other | partial | 245 | 11 480 962 | JEDNO ZADANIE KOŃCOWE: POSPRZĄTAJ WYŁĄCZNIE hotelowe rozmowy WhatsApp dotyczące DEFINITYWNIE ODRZUCONYCH  |
| other | unknown | 330 | 11 309 146 | Facebook – utwórz Stronę (Page) firmy Skakanka – Centrum Ter… (+1969) |
| site_setup | interrupted | 556 | 10 971 563 | Uruchom na LH.pl osobną witrynę dla nowego forum forum-subiekta.pl. LH.pl to sprawdzony dostawca Skakanki |
| other | partial | 278 | 10 182 119 | Google Business Profile – uzupełnij istniejący profil Skakanki do maksimum (konto Google skakanka.centrum |
| other | partial | 166 | 10 167 903 | Konto LinkedIn Pawła Boruta-Ośko. Aktywne konto weryfikuj WYŁĄCZNIE w menu „Me” (nazwa musi brzmieć „Pawe |
| other | partial | 131 | 9 868 511 | Google Business Profile 'Lupus Consulting' (Krakowskie Przedmiescie 13, 00-071 Warszawa), account pawel.b |
| research | partial | 192 | 9 478 999 | ZADANIE TYLKO LUPUS CONSULTING, NIE SKAKANKA. Portal https://www.oferteo.pl/. Sprawdź czy istnieje konto  |
| other | success | 203 | 9 244 188 | Goal: create a free company listing for "Lupus Consulting" on GoodFirms (goodfirms.co, "List your company |

## Interpretacja

1. Porównuj koszty tylko w podobnym typie zadania i przy podobnej liczbie wymaganych akcji. Rejestracja konta, krótki odczyt strony i pięć wpisów to różne produkty.
2. Do kosztu wykonanej akcji wlicz przygotowanie, ponawianie, autoryzację i sprawdzenie skutku. Nie utożsamiaj liczby wywołań modelu z liczbą czynności.
3. Nierozstrzygnięte cele są kandydatami do diagnostyki dowodów; nie nazywaj ich bez dowodu błędami i zmarnowanymi tokenami.
4. Wysokie P90 przy stabilnej medianie wskazuje duże wahania; przed optymalizacją trzeba rozpoznać dostępność konta, limity portalu, różne formularze i dodatkowe pytania.
5. Pełne dane źródłowe i ślady techniczne: docs/of_action_accounting_20261106/all_business_goals.jsonl.
6. Nowe wpisy telemetrii business_goal_start, business_goal_effect i business_goal_finish będą pozwalały rozliczać jednostkę automatycznie w kolejnych zadaniach.
