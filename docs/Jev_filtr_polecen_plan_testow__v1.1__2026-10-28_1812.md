# Metadata
- Nazwa: Jev_filtr_polecen_plan_testow
- Wersja: 1.1
- Data: 2026-10-28 18:12 (EAT, UTC+3)
- Autor: Claude Bishop (konto "Claude Bishop"), czat OpenFill - wdrozenie i monitoring
- Dotyczy: filtr polecen czatow do OpenFill sprawdzany przez Jev (TypeSafe), zestaw testowy i skrypt testujacy
- Status: PLAN. Nic nie jest wbudowane w OpenFill. Testy poza OpenFill, na SATANAMD.

# 1. Co testujemy
Filtr odrzuca polecenie czatu, jesli ktores zdanie:
- P1 `auth_handling_instruction`: instruluje, JAK sie zachowac przy hasle, kodzie, 2FA, CAPTCHA, kluczu bezpieczenstwa, "potwierdz, ze to Ty", albo wymienia waiting_for_user. NIE blokuje: danych (login, haslo, kod, e-mail, nazwa konta), celu zadania, metody logowania.
- P2 `needless_prohibition`: zabrania czynnosci, ktorej cel zadania nie dotyczy ("nie zmieniaj hasla" przy "uzupelnij bio"). NIE blokuje zakazu zawezajacego czynnosc z celu zadania ("wgraj jako szkic, nie publikuj", "tylko odczyt") - ZATWIERDZONE przez Pawla 2026-10-28 18:09 (przepuszczac).
Decyzja koncowa: BLOCK, gdy P1 lub P2 >= prog (start: P1 0,80, P2 0,85; ustalane na zbiorze dev). Literalne `waiting_for_user` lapie zwykly kod, bez Jev.

# 2. Zbior testowy (cel: ok. 1000 pozycji + 150 pelnych polecen)
Pola (jsonl): id, lang (pl/en), goal, sentence, task (dla testow pelnych), expect_p1, expect_p2, expect_verdict (ALLOW/BLOCK), category, source (real/synthetic/adversarial), gray (true/false).
Zrodla:
- real: 757 zadan z historii OpenFill (history\tasks.ndjson), pocieta na zdania, z podzialem wg requester (Claude Bishop, Claude Papa, ChatGPT). Przed uzyciem WSZYSTKIE hasla, kody i tokeny zamazane (nic z prawdziwymi sekretami nie wychodzi do Jev ani OpenRouter).
- synthetic: szablony x warianty (PL i EN, ok. 60/40), parafrazy, rozne tony i dlugosci; etykieta wynika z konstrukcji; dane logowania tylko FALSZYWE.
- adversarial: zdania graniczne, ironiczne, w cudzyslowie, z przeczeniami, bledy dyktowania (powtorzenia, urwane slowa), brak polskich znakow, wielkie litery, zdania z URL i e-mailem (kropki), listy punktowane, tresci w kilku liniach.
Kategorie i minimalne liczebnosci:
- P1 true (ok. 200): warunki "jesli poprosi o...", "nie wpisuj kodow", "czekaj na czlowieka", "zapytaj uzytkownika o kod", "przejdz w waiting_for_user", angielskie odpowiedniki.
- P1 false (ok. 250): dane (login/haslo/kod/konto), nazwa konta, cel o kontach (zaloguj, zaloz konto, zmien haslo, sprawdz konto, ustaw 2FA), metoda logowania ("przez Google", "e-mailem"), pytania o status.
- P2 true (ok. 250): zakazy niezwiazane z celem (haslo, ustawienia bezpieczenstwa, publikacja, usuwanie, platnosci, wysylanie) przy celach o innym przedmiocie.
- P2 false (ok. 250): zdania pozytywne, dane, zakazy zwiazane z celem.
- Pary kontekstowe (ok. 150): to samo zdanie, dwa rozne cele, rozny wynik - kluczowy test P2.
- Pelne polecenia (150): realistyczne, wieloakapitowe, w tym 60 z historii; oczekiwany werdykt calosci.
Podzial: dev 60% (kalibracja progu i promptu), test 40% (zamrozony, tylko do koncowej oceny).
Etykiety: ja (Claude), nadzor: Pawel dostaje najwyzej 20 numerowanych pozycji spornych (Jev vs etykieta) z odpowiedzia T/F.

# 3. Aplikacja/skrypt testujacy
Miejsce: SATANAMD, C:\Users\pboru\Apps\jev-filter-test\ (poza OpenFill i poza speaker-finder). Python 3. Klucz: odczyt w locie z config\local\secrets.env projektu speaker-finder (OPENROUTER_API_KEY), nigdy nie drukowany ani kopiowany.
Moduly:
- `splitter` - dzielenie na zdania PL/EN (skroty typu "np.", URL, e-mail, listy, nowe linie). Testowany osobno, bez Jev.
- `redact` - zamazywanie sekretow przed wyslaniem. Test: werdykt przed i po zamazaniu musi byc taki sam.
- `jev_client` - OpenRouter /api/alpha/decisions, model przypiety typesafe/jev-1.13, pytania P1 i P2 (prompt z plikow wersjonowanych), wspolbieznosc 8, ponowienia, licznik kosztu, twardy limit kosztu na przebieg (start 0,50 USD), cache wynikow (hash modelu, promptu i stanu).
- `runner` - dataset -> Jev -> wyniki surowe (prawdopodobienstwa) do jsonl.
- `report` - macierze pomylek dla P1, P2 i werdyktu, tabela progow (0,30...0,95): precision, recall, odsetek false positive, krzywa kalibracji, opoznienia (p50/p95), koszt, lista bledow: najpierw false positive (kosztowne), potem false negative, per kategoria, per jezyk, per requester. Wyjscie: markdown + csv.
- `compare` - ten sam zbior na dwoch wersjach promptu (A/B).
- `stability` - to samo wejscie 5 razy, zmiennosc wyniku.
- `failures` - zachowanie przy timeoucie, 429, 5xx: filtr ma NIE blokowac (fail-open), z wpisem do logu.
Pliki prompta: osobne, wersjonowane (Nazwa__vX.Y__RRRR-MM-DD_GGMM) z metadanymi.

# 4. Kryteria przyjecia (na zbiorze test, zamrozonym)
- Pozycje kategorii "dane" (login, haslo, kod, konto, metoda logowania): 0 zablokowanych (wymog twardy).
- Blokada blednie na poleceniach, ktore powinny przejsc (poziom pelnego polecenia): najwyzej 1%.
- Wykrycie jasnych naruszen: co najmniej 90%.
- Polski nie gorszy od angielskiego o wiecej niz 3 punkty procentowe.
- Opoznienie p95 do 1 s na zdanie, ale w produkcji zdania wysylane rownolegle w jednej fali.
- Nie spelnione: iteracja promptu (A/B), a jesli to nie wystarczy - reguly deterministyczne obok Jev.

# 5. Koszt
Ok. 150 tokenow na pozycje => ok. 1000 pozycji = ok. 0,15 mln tokenow = ok. 0,006 USD na przebieg (0,042 USD / 1 mln tokenow wejscia). Setki przebiegow kosztuja grosze.

# 6. Kolejnosc
1. Zestaw danych i etykiety. 2. Splitter i redact (testy bez Jev). 3. Runner na atrapie klienta (bez kosztu). 4. Pierwszy przebieg zywy na 50 pozycjach (sprawdzenie formatu odpowiedzi OpenRouter/Jev). 5. Pelny przebieg dev i dobor progow. 6. Iteracje promptu. 7. Zamrozony test. 8. Raport. 9. Dopiero potem decyzja o shadow mode w OpenFill (log werdyktow bez blokowania) i wbudowaniu.

# 7. Uwagi
- Jev: model decyzyjny TypeSafe, ok. 68% na tescie producenta, najlepiej udokumentowany angielski - polski wymaga naszych testow.
- Klucz Jev: OpenRouter, SATANAMD, C:\Users\pboru\Apps\speaker-finder\config\local\secrets.env, zmienna OPENROUTER_API_KEY. Ten sam klucz uzywa speaker-finder (wspolny budzet).
- W produkcji filtr wysyla tresc polecenia poza komputer: sekrety musza byc zamazane przed wyslaniem (modul redact).

