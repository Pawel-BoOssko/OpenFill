# OpenFill: historyczne tokeny na cel użytkowy i wykonaną akcję

Źródło: pełne logi produkcyjne usage i app NDJSON, październik i listopad 2026.
Stan pomiaru: 2026-11-06T17:09:37.9303352Z

## Reguła rozliczenia

Cały cel = suma wejścia i wyjścia modelu przez wszystkie powiązane próby i kontynuacje tego samego zamiaru, również nieudane.
Jedna potwierdzona akcja = całkowite tokeny tego celu / liczba faktycznie potwierdzonych efektów.
Jeżeli planowano 5 publikacji, ale potwierdzono 3, koszt wykonanej publikacji = całość / 3. Przy braku potwierdzenia nie ma wyniku jednostkowego.
Koszt nominalny = całość / liczba planowanych działań, wyłącznie jako plan, nigdy jako rzeczywista cena wykonania.
Tokeny rozumowania są podzbiorem wyjścia. Cached input jest podzbiorem wejścia.

## Bilans wszystkich dostępnych danych

- Wywołania modelu: 84 364.
- Tokeny wejściowe: 1 687 963 443, z cache: 1 420 525 689, bez cache: 267 437 754.
- Tokeny wyjściowe: 7 709 345, w tym rozumowania: 4 275 225.
- Tokeny razem: 1 695 672 788. Koszt historyczny: 44.77670 USD.
- Techniczne przebiegi: 1787, zakończone rekordy: 1765.
- Grupy celów: 1726 (prowizoryczne przy brakujących relacjach historycznych), jawne łańcuchy kontynuacji: 33.
- Wywołania bez końcowego rekordu zadania: 1156. Ich tokeny są uwzględnione.

## Bilans według rodzaju celu

| Typ celu | Grupy | Sukces według agenta | Tokeny wszystkie | Potwierdzone technicznie akcje | Akcje z raportu agenta | Tokeny na policzalną akcję* |
|---|---:|---:|---:|---:|---:|---:|
| Zgłoszenie aplikacji | 3 | 1 | 894 616 | 0 | 0 | n/d |
| Publikowanie konfiguracji | 15 | 4 | 19 585 046 | 0 | 0 | n/d |
| Zmiana danych / formularz | 53 | 29 | 52 859 780 | 2 | 0 | 20 074 |
| Wysyłanie zaproszeń | 87 | 43 | 114 969 398 | 5 | 188 | 276 977 |
| Wysyłanie wiadomości | 16 | 7 | 5 670 308 | 0 | 6 | 293 904 |
| Cel nierozpoznany z archiwalnego skrótu | 722 | 313 | 873 939 669 | 0 | 0 | n/d |
| Publikowanie wpisów / filmów / recenzji | 37 | 21 | 41 247 112 | 0 | 20 | 1 195 225 |
| Rejestracja konta lub zgłoszenie | 32 | 4 | 25 007 427 | 0 | 1 | 687 085 |
| Badanie / wielostronicowy odczyt | 732 | 425 | 542 130 015 | 0 | 0 | n/d |
| Odczyt pojedynczej strony | 22 | 22 | 457 696 | 22 | 0 | 20 804 |
| Uruchomienie witryny | 7 | 3 | 18 911 721 | 0 | 0 | n/d |

*Koszt jednostkowy w tabeli dotyczy tylko grup, dla których liczba efektów jest znana. Efekty z raportów agenta nie są niezależnie zweryfikowane. Nie wolno dzielić kosztu wszystkich prób przez niewielką liczbę udokumentowanych akcji z innych prób.*

## Przykłady całego zadania i jednej akcji

| Zadanie | Liczba efektów | Wejście | Cache | Wyjście | Wszystkie tokeny | Tokeny na akcję | Podstawa |
|---|---:|---:|---:|---:|---:|---:|---|
| Facebook: publikacja 3 wpisów | 3 | 6 686 378 | 3 765 377 | 14 859 | 6 701 237 | 2 233 746 | agent_reports_counted_publication |
| Instagram: publikacja 3 wpisów | 3 | 2 623 241 | 2 586 137 | 6 578 | 2 629 819 | 876 606 | agent_reports_counted_publication |
| LinkedIn: wsad 5 zaproszeń | 5 | 2 707 106 | 2 435 430 | 3 223 | 2 710 329 | 542 066 | agent_listed_ui_confirmation |
| Rejestracja Cylex | 1 | 682 726 | 656 312 | 4 359 | 687 085 | 687 085 | agent_reports_active_registration |
| Odczyt pojedynczej strony | 1 | 134 384 | 74 133 | 983 | 135 367 | 135 367 | success_and_live_read |
| Wysłanie pojedynczej wiadomości | 1 | 694 477 | 651 333 | 3 033 | 697 510 | 697 510 | agent_reports_sent_message |

Uwaga: przykłady publikacji i zaproszeń są ustalane z końcowego raportu agenta o tym, co widział w UI. Pełne tokeny obejmują przygotowanie, weryfikację i nieudane próby.
Raport agenta nie jest równoważny niezależnej kontroli każdego wpisu lub konta. Szczegóły poziomu dowodów pozostają w pliku wszystkich celów.

## Działania po pięć sztuk

- Cele publikacji z pięcioma zamówionymi wpisami widoczne w skróconych historycznych opisach: 0.
- W tym cele z pięcioma potwierdzonymi albo zaraportowanymi efektami: 0.
Nie przeliczam brakujących sztuk na wykonane wpisy. W danych szczegółowych pozostaje koszt nominalny 1/5 oraz odrębny koszt na faktycznie rozpoznaną akcję.

## Odczyt nieznanej dotąd witryny

- Odczyty jednej strony zakończone sukcesem i z dowodem live: 22.
- Spośród nich odczyty hostów występujących po raz pierwszy w dostępnej historii zadań: 2.
- Mediana tokenów na odczyt pierwszego zaobserwowanego hosta: 10 693.

To definicja operacyjna, nie wiedza o tym, czy model lub użytkownik znał witrynę wcześniej. Historyczne logi nie odtwarzają rzeczywistej wcześniejszej znajomości danej strony.

## Granice dokładności rekonstrukcji

1. Starsze przebiegi nie mają identyfikatorów powiązania kontynuacji. Rozliczenie celów bez jawnego łańcucha jest ostrożnym zbiorem osobnych prób, a nie gwarantowaną liczbą unikalnych projektów.
2. Zadania z długim poleceniem są w logach użycia skrócone. Dlatego część celów i liczby zamówionych sztuk pozostają nierozpoznane zamiast być zgadywane.
3. Sukces zgłoszony w końcowym podsumowaniu nie zawsze oznacza potwierdzoną akcję skutkową. Liczby z ledgeru, obserwacji UI i samego planu mają osobne pola.
4. W wieloetapowym celu koszt całego zadania jest znany dokładnie. Koszt pojedynczej różnorodnej czynności wewnątrz tego celu nie jest możliwy do precyzyjnego podziału bez telemetrii przypisującej wywołania do faz.
5. Cache wpływa na koszt USD, ale tokeny cached są nadal input tokens. Nie oblicza się oszczędności jako różnicy tokenów pomiędzy nieporównywalnymi celami.

## Obowiązujący model pomiaru dla przyszłych zadań

Każde zlecenie zapisuje nadrzędny cel biznesowy (np. zarejestruj konto X albo opublikuj 5 wpisów Y). Kolejne próby i uruchomienia dziedziczą ten sam cel.
Każdy efekt zapisuje typ akcji, identyfikator przedmiotu efektu, stan prepared/attempted/confirmed/uncertain oraz dowód weryfikacji.
Liczniki obejmują: requested, attempted, confirmed, partial, failed i skipped. Należy odróżnić dokonane skutki zewnętrzne od samego kliknięcia.
Zużycie modeli jest przypisywane do celu, a tam gdzie to możliwe także do aktualnej fazy: przygotowanie, wykonanie, kontrola. Wspólny narzut celu pozostaje wspólny.
Dla pięciu identycznych potwierdzonych publikacji koszt jednej = koszt wszystkich prób i całego zadania / 5; dla trzech potwierdzonych = /3. Dla mieszanych akcji nie wolno udawać, że dokładny koszt każdej wynika z podzielenia przez liczbę różnych efektów.
Najważniejsze wskaźniki: całkowite tokeny na zrealizowany cel, wejście bez cache, wyjście, koszt USD na cel i na potwierdzoną akcję, koszt prób niezakończonych sukcesem.
Porównania modeli i optymalizacji prowadzić na tych samych typach celów, z podobną liczbą akcji i stopniem trudności.

## Pliki i odtwarzalność

- Komplet 1726 prowizorycznych celów: docs/of_action_accounting_20261106/all_business_goals.jsonl.
- Bilans kategorii: docs/of_action_accounting_20261106/summary.json.
- Odtworzenie: python tools/of_action_accounting_20261106.py, następnie python tools/of_action_report_20261106.py.
- Ekstrakcja potwierdzonych i zgłoszonych skutków: tools/of_action_evidence_20261106.py.
- Historyczne identyfikatory run/task istnieją w JSONL tylko jako odnośniki diagnostyczne. Podstawą agregacji są cele i efekty.
