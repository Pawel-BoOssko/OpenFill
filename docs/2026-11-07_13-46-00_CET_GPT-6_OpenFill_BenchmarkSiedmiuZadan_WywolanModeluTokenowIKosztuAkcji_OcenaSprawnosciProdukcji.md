# OpenFill: siedem realnych testów efektywności modelu na cel i wykonaną akcję

Data: 7 listopada 2026, Europe/Warsaw (CET). Autor: GPT-6. Host: PBOX1.
Prognozy zapisane przed testami. Produkcyjny OpenFill LIVE, efekty lokalnego serwera weryfikowane niezależnie.

## Wynik zbiorczy

- Przewidywane wywołania: 45; rzeczywiste: 61; odchylenie +35.6%.
- Wszystkie tokeny: 562 235; input 554 815; cached input 480 487; output 7 420.
- Koszt modeli: 0.01594767 USD; błędy narzędzi: 7.
- Wyniki OpenFill: 6 success, 1 partial (B03 podał poprawne dane).
- Stan lokalnej aplikacji: 10 wpisów, zgodnych 5 pierwszych i 5 kolejnych.

- Rozpoczęte żądania do modelu: 62, z odpowiedzią i pomiarem tokenów: 61, z błędem: 1.
Nieudana próba wystąpiła w B03 na kroku 5: Cannot reach OpenAI: An error occurred while sending the request. Model ponowił krok i zwrócił poprawną treść, lecz bez wywołania finish, przez co raport OpenFill ma status partial.

## Prognozy i realny koszt

| Test | Prognoza calls | Rzeczywiste calls | Różnica | Tokeny celu | Efekty | Tokeny/efekt | Błędy narzędzi | Status |
|---|---:|---:|---:|---:|---:|---:|---:|---|
| B01: Odczyt jednej strony | 2 | 2 | +0 | 15 121 | 1 | 15 121 | 0 | success |
| B02: IANA domeny | 3 | 4 | +1 | 33 971 | 1 | 33 971 | 0 | success |
| B03: Dwie biografie Wikipedii | 5 | 6 | +1 | 68 594 | 1 | 68 594 | 0 | partial |
| B04: Dokument Python | 5 | 6 | +1 | 61 638 | 1 | 61 638 | 0 | success |
| B05: Formularz Selenium | 7 | 6 | -1 | 46 674 | 1 | 46 674 | 2 | success |
| B06: Pierwsze pięć wpisów | 11 | 18 | +7 | 159 475 | 5 | 31 895 | 2 | success |
| B07: Pięć kolejnych wpisów | 12 | 19 | +7 | 176 762 | 5 | 35 352 | 3 | success |

B01-B05: liczba wykonanych działań zweryfikowana ocennie na podstawie odpowiedzi i stanu przeglądarki. B03: jeden merytorycznie ukończony cel mimo statusu partial.
B06 i B07: pięć nowych wpisów potwierdzonych niezależnie przez dziesięć unikalnych par tytuł/treść w pliku danych serwera testowego.

## Konkretne przyczyny nadmiarowych wywołań

1. B05: act_many zawierał identyfikator id_nonexistent, powodując NOT_FOUND. act zakończył się błędem action_not_attempted:postcheck_selector_required_or_too_broad. Formularz ostatecznie wysłano prawidłowo.
2. B06: agent próbował zapisać kilka formularzy w jednej serii act_many. Każdy POST przeładowuje dokument, więc następne akcje na starym stanie CDP są zawodne. Dwa błędy Runtime.evaluate: Value does not fall within the expected range i dodatkowe odczyty podniosły liczbę calls do 18.
3. B07: dwa błędy Runtime.evaluate i jeden NOT_FOUND w act_many; kolejne odczyty DOM i ponowienia. Zadanie zakończone, ale przy 19 calls, zatem strona już poznana nie dała obniżenia zużycia tokenów.
4. B03: obie prawidłowe daty 1815 i 1791 oraz związek z maszyną analityczną, lecz outcome=partial. Jakość i status muszą być mierzone osobno.
5. B02: agent prawidłowo wskazał, że example.net nie jest wymienione dosłownie w tekście IANA, tylko wynika ze źródłowego RFC 2606; nie zmyślił zawartości strony.

## Porównanie pierwszej i powtórnej serii

- B06: 18 calls, 159 475 tokenów, 31 895 tokenów na wpis.
- B07: 19 calls, 176 762 tokenów, 35 352 tokenów na wpis.
- Różnica: 10.8% tokenów na wpis. Hipoteza poprawy przez znajomość strony nie potwierdzona.

## Rekomendowane poprawki

P0: Dla formularzy kończących się przeładowaniem strony wykonywać jeden submit na aktualnym DOM, następnie sprawdzić efekt i dopiero zaczynać kolejny wpis. Nie kontynuować act_many na nieaktualnych identyfikatorach elementów.
P0: Po błędzie Runtime.evaluate odczytać rzeczywisty skutek POST przed ponowieniem, aby nie duplikować działań.
P1: Stosować najtańsze wystarczające potwierdzenie pojedynczej akcji zamiast kilku pełnych odczytów DOM.
P1: Rozszerzyć detekcję goalType dla polecenia dodaj pięć wpisów: B07 błędnie otrzymał kategorię other i brak efektywnego dzielnika w standardowym raporcie, mimo kompletnego sukcesu.
P1: Sprawdzić rozbieżność outcome partial przy kompletnej odpowiedzi B03.

## Referencje i ograniczenia

Prognozy przed wykonaniem: docs/2026-11-07_13-30-16_CET_GPT-6_OpenFill_PlanBenchmarkuWywolanModeluNaCelIKluczoweAkcje_PorownaniePrzewidywanIZmierzony.md.
Źródło: produkcyjne usage_202611.ndjson i app_20261106.ndjson; szczegóły w plikach JSON i CSV o tym samym stemie.
Sandbox: tools/2026-11-07_13-30-16_CET_GPT-6_OpenFill_PiecWpisowSandboxSerwer_DoPomiaruTokenowNaWpis_BezPublikacjiZewnetrznej.py; dane staging/of_benchmark_5post_state_20261107.json.
W tym eksperymencie nie wdrażano zmian kodu agenta: wyniki dokumentują aktualne działanie, nie oszczędności uzyskane po optymalizacji.
