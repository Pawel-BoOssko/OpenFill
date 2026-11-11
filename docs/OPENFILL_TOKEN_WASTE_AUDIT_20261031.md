# OpenFill: audyt strat operacji i tokenow, 2026-10-31

## Zakres i metodologia
- Host PBOX1, LIVE OpenFill. Dane: data/stable/logs/usage_202610.ndjson i app_202610*.ndjson, od 2026-10-05 do 2026-10-31. Analiza bez uruchamiania zadan na stronach, tylko odczyt logow i kodu SOURCE.
- 1512 identyfikatorow przebiegow w sumie, 1477 z wierszem kind=task. 73329 model calls, 1499464216 inputTokens, 1249851605 cachedTokens, 249612611 niecache'owanych, 6552358 outputTokens, 3615364 reasoningTokens, suma raportowanych costUsd 40.709 USD. Cache: 83.35% input.
- Czas modelowy sumowany po zadaniach: 236155163 ms, tj. okolo 65.6 h. To suma czasow wywolan, nie czas kalendarzowy ani czas pracy narzedzi.
- Brak bledow parsowania NDJSON. 35 przebiegow nie ma pelnej linii kind=task; zestawienie kosztow i wywolan nie musi byc identyczne z faktura.
- Kategoryzacja wg nazwy w poleceniu, narzedzi i adresow z argumentow. Grupy Google i Meta moga zawierac rozne domeny, nie traktowac tego jako czystego rankingu hostow.
- Powtarzana akcja nie jest automatycznie bledem: liczy sie powtorka bez nowej wiedzy i bez potwierdzonego przyrostu wykonania zadania.

## Ranking grup zadan
| Grupa | Przebiegi | Wywolania modelu | Srednio na przebieg | Koszt z logow |
|---|---:|---:|---:|---:|
| LinkedIn | 247 | 13687 | 55.4 | 12.8548 USD |
| Google, szeroka grupa | 327 | 23008 | 70.4 | 11.2818 USD |
| Facebook/Meta | 139 | 10801 | 77.7 | 4.9177 USD |
| WhatsApp | 167 | 6582 | 39.4 | 2.7457 USD |
| Airbnb | 25 | 1522 | 60.9 | 0.8141 USD |
- Cztery pierwsze grupy kosztuja razem 31.7999 USD: 78.1% wartosci 40.709 USD.
- Narzedzia w calym audycie: get_page 19887, act 16663, run_js 8693, wait 5782, read_text 5440, navigate 5047, screenshot 3230, search_logs 2299, get_network 1458, act_many 1420. Wiecej odczytow DOM i klikniec niz grupowanych akcji.

## Konkretne przebiegi dowodowe
- LinkedIn 20261022_141754_71f419: 241 krokow, 79 get_page, 103 act, 18 report_verified_action, 18503940 input tokens, 143813 tokenow w ostatnim kroku, requestChars=900247, koszt 1.0197 USD, wynik warn. Brak kompresji historii w tamtej wersji. Dzis kompresor dziala, wiec NIE przywracac starej logiki historii.
- LinkedIn 20261031_082614_0002d1: 147 krokow, 79 run_js, 24 search_logs, 21 wait, brak koncowego outcome. Kompresja juz dziala (17 checkpointow), jednak liczba decyzji modelowych nadal nadmierna przy audycie listy rozmow.
- Google Business Profile 20261008_215130_ac0879: 278 krokow, 96 get_page, 68 act, 37 run_js, 33 act_many; koszt 0.615 USD, wynik warn.
- Facebook 20261008_215124_2952e0: 330 krokow, 108 get_page, 133 act, 20 wait, 15 run_js, koszt 0.7553 USD; brak potwierdzonego koncowego outcome w zestawieniu.
- WhatsApp 20261029_081901_ae8073: 399 krokow, 99 get_page, 80 act, 55 search_logs, 33 screenshot, 49 read_text, koszt 0.1046 USD, wynik warn; 5 bledow skladni run_js i 1 brak elementu, czesc pracy to odzyskiwanie po problemach.
- WhatsApp 20261030_112322_236e23: 101 krokow, 69 whatsapp_open dla 13 unikalnych numerow, w tym 56 powtornych otwarc i maksymalnie 13 otwarc tego samego numeru. Odpowiedzi techniczne: 61 ok:true i 8 ok:false. Dodatkowo 31 search_logs, koszt 0.05781 USD, wynik warn. W przeciwienstwie do tego 20261030_114913_a17af1 otworzyl 11 unikalnych numerow w 11 wywolaniach, 0 powtorek.
- Airbnb 20261030_112227_1500e2 i 20261030_115301_b53421: lacznie 733 kroki. W drugim przebiegu 414 krokow, 116 search_logs, 90 get_network, 100 get_page, 84 act, koszt 0.2285 USD, brak zapisanego koncowego outcome. Problem to petla diagnostyczna dookoła jednej operacji z cenami, nie tylko same pola interfejsu.

## Co juz dziala, czego nie powielac
- PromptHistoryCompactor istnieje i wykonuje checkpointy. Do 22.10 w danych 0 checkpointow; po 23.10 widac je w wiekszosci dlugich zadan.
- LinkedIn koszt na wywolanie modelu: w probie sprzed 23.10 okolo 0.001180 USD, od 23.10 okolo 0.000315 USD. Spadek okolo 73%, ALE zadania i ceny modeli nie musza byc porownywalne. Srednia liczba krokow na zadanie 53.9 -> 59.6, problem nie jest rozwiazany.
- WhatsApp po 30.10: dedykowane whatsapp_send, whatsapp_open, whatsapp_read_visible i list_all_previews. Zadania WhatsApp z 30-31.10: 24 przebiegi, srednio 8.1 krokow, mediana 2.5. Mimo tego zdarza sie petla 101 krokow. Przeciac niepotrzebne powtorki, nie usuwac zyskow z nowych narzedzi.
- page_capture z filtrami, plikami, SHA256 i receptami jest juz w wersji 1.58, ale we wszystkich logach zarejestrowano tylko 2 uzycia tego narzedzia; brak podstaw, by uznac problem innych stron za automatycznie rozwiazany.
- Obecny limit kompresji historii jest efektywnie ograniczony do 110000 znakow przez PromptHistoryCompactor, niezaleznie od ustawienia 240000 w AppConfig. To nie blad. Nie proponowac arbitralnego zmniejszania progow bez analizy jakosci wyniku.

## Docelowa architektura, priorytety
1. P0: Narzedzie DOM dla czynnosci wieloetapowych: jedno pobranie kontekstu strony do pliku, deterministyczne przeszukiwanie i wyselekcjonowanie fragmentow, model dostaje niewielki wycinek; nie get_page po kazdym nieistotnym kliknieciu.
2. P0: Stan i przyrost. Przed/po bloku czynnosci porownuj identyfikator widoku, aktywna domenę, URL, kluczowe obszary DOM i wynik celu. Jesli ponowny odczyt niczego nie zmienil, nie wysylaj tego samego duzego kontekstu do modelu; wybierz alternatywna metoda z informacja, co sie nie zmienilo. Nie stosuj slepego limitu liczby prob, gdy postep jest rzeczywisty.
3. P0: WhatsApp: dla listy numerow jedno skoordynowane narzedzie typu batch_open_read, ktore zwroci kompaktowy wynik dla kazdego numeru i bedzie umialo pominac powtorki w ramach tego samego odczytu. Dla rozmow identyfikowanych po nazwie: dopasowanie do wyrenderowanych preview, potem raz po pelnej liscie, nie zgadywanie numerow ani otwieranie wielu czatow w poszukiwaniu jednego. Wyjscie bez SVG, CSS, obrazow i pelnych historii, gdy wystarcza ostatnie 5-10 wiadomosci.
4. P0: Airbnb: odczyt kalendarza i tabeli cen naraz, operacja serii dat na strukturalnych danych, potem zbiorcza weryfikacja zmian. Nie 116 osobnych search_logs i 90 get_network podczas jednego ciagu. Przy API i asynchronicznym UI jedna krotka diagnostyka na konkretny blad, nie diagnostyka co krok.
5. P1: LinkedIn: oddziel tryb listy zaproszen, listy wiadomosci i profil. Recepty selektorow per widok, odczyt listy jako jeden strukturalny wynik, akcje powtarzalne jako act_many z raportem efektow per osoba. Dla prawdziwych zmian sprawdzac konto i efekty, bez ponownych pelnych get_page przy kazdym prostym ruchu.
6. P1: Google/Meta: adaptery do najbardziej powtarzanych widokow, zwlaszcza Google Business Profile, YouTube Studio, Gmail, Facebook Pages i Instagram. Jednorazowy zrzut semantyczny, lokalne filtry, stabilne wzorce akcji i zapamietanie zweryfikowanych selektorow. Fallback do DOM/debug tylko przy realnej zmianie widoku albo bledzie.
7. P1: search_logs/get_network/screenshot: diagnostyka na konkretne zapotrzebowanie, nie petla modelowa. Diagnostyczne dane zapisuj do pliku, do modelu jedno streszczenie z bledem, URL i istotnym fragmentem.
8. P1: Wsadowa kontrola plikow zrzutu: jeden krotki terminal_exec lub weryfikator w C# oblicza manifest, rozmiary i SHA256 wszystkich plikow. Nie wywolywac osobnego modelu dla kazdego pliku.
9. P2: Uporzadkowanie telemetryki: rejestruj per zadanie host rzeczywisty (nie tylko nazwa w poleceniu), widok, logiczny krok, unikalny wynik DOM, liczbe niezmiennych ponowien, narzedzie i powod jego uzycia, czy zadanie zakonczylo sie dowiedzionym sukcesem. Przebiegi bez terminalnego wyniku oznaczaj 'nieustalone', nie 'nieudane'.

## Kryteria odbioru bez sztywnych ograniczen
- Na tym samym zadaniu testowym porownac stare i nowe: liczba wywolan modelu, uncached tokens, liczba get_page/run_js/search_logs, czas wykonania, rzeczywisty efekt i liczba prob bez zmiany stanu.
- Osiagniecie efektu jest wazniejsze niz niska liczba krokow; wiele krokow jest dopuszczalne, gdy kazdy wnosi nowa informacje lub stan.
- Testy szczegolnie: 10 numerow WhatsApp i 10 rozmow po nazwie; LinkedIn odczyt listy oraz faktyczne wyslanie; Airbnb zakres kalendarza; GBP/Facebook edycja profilu. Bez odtwarzania nieautoryzowanych operacji na LIVE.
- Kluczowe: zero nieuzasadnionych kolejnych odczytow tego samego widoku, zaden brak sukcesu nie może byc zamaskowany malym kosztem.

## Pliki i zakres realizacji
- Dokladne agregaty i przebiegi: docs/OPENFILL_TOKEN_AUDIT_DATA_20261031.json. Mozliwy duzy plik z kontekstem zadan, tylko lokalnie w projekcie.
- Powtarzalny analizator odczytowy: tools/audit_token_waste__v0.1__2026-10-31.py.
- Audyt wykonany odczytowo. Nie zmieniono LIVE 1.58 ani produkcyjnych danych kont. Priorytety to projekt kolejnych zmian SOURCE; nie udawac ich wdrozenia.
