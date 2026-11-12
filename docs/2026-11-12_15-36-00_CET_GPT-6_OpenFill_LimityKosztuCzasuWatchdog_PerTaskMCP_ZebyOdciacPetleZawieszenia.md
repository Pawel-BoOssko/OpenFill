# OpenFill: limity per zadanie, watchdog kosztu, czasu i braku postępu

- Utworzono: 2026-11-12, 15:36 Europe/Warsaw (CET)
- Autor/model: GPT-6
- Projekt: OpenFill, PBOX1
- Cel: automatyczne zatrzymywanie pojedynczego zadania AI, aby ograniczyć skutki zapętlenia i długiego braku postępu
- Repozytorium: Pawel-BoOssko/OpenFill, jedna istniejąca gałąź feature/openfill-telegram-20261111

## Interfejs wywołania

MCP `openfill_start_task` obsługuje trzy opcjonalne parametry:

| Pole | Typ | Dopuszczalny zakres | Działanie |
| --- | --- | --- | --- |
| `max_cost_usd` | liczba | 0.001 do 50 USD | Zatrzymuje po przekroczeniu obliczonej wartości USD naliczonej dla tego zadania |
| `max_duration_seconds` | liczba całkowita | 10 do 36000 s | Granica czasu zegarowego od przyjęcia zadania przez aplikację, także podczas oczekiwania na człowieka |
| `max_idle_seconds` | liczba całkowita | 15 do 3600 s | Zatrzymuje, jeśli model nie wykona kolejnego kroku w określonym czasie; oczekiwanie na człowieka nie zwiększa bezczynności |

Domyślnie: `max_idle_seconds=180`, jeśli brak parametru. Jeśli `max_duration_seconds` nie podano, działają istniejące limity globalne. Jeśli `max_cost_usd` nie podano, działają istniejące limity kosztowe na domenę. Zawsze obowiązuje ostrzejsze ograniczenie.

Przykładowe parametry do przekazania zleceniu:

```json
{
  "task": "Wejdź na stronę i wypełnij wskazane pola, bez wysyłania formularza",
  "requester": "ChatGPT",
  "chat": "Przykładowe zlecenie",
  "max_cost_usd": 0.05,
  "max_duration_seconds": 300,
  "max_idle_seconds": 90
}
```

## Działanie odcięcia

- Licznik kosztu: aktualizowany po odpowiedzi modelu. Zatrzymanie następuje przed następnymi akcjami i zapytaniami modelu. Dotyczy także `local_plan_v1_draft`: kontrola po odpowiedzi planującej, przed wykonaniem planu.
- Limit zegarowy: osobny zegar uruchamiany wraz z przyjęciem zadania w AppHost, niezależny od pętli modelu. Anuluje tylko dany task.
- Brak postępu: osobna pętla watchdoga sprawdza liczbę kroków modelu i czas zegarowy co 500 ms, bez wywołań AI.
- Zadania oczekujące na odpowiedź użytkownika nie są zatrzymywane na podstawie bezczynności, ale nadal obowiązuje całkowity limit czasu.
- W odpowiedzi zlecenia: `stop_reason=max_cost_usd`, `max_duration_seconds` lub `stalled_no_progress` w zależności od przyczyny. W stanie końcowym koszt jest zachowany w rekordzie MCP.
- Parametry są zapisane w rekordzie zadania; w identycznych zleceniach z `idempotency_key` parametry limitów też muszą się zgadzać.

## Granice techniczne

Koszt jest estymacją na podstawie liczników tokenów odpowiedzi i cennika modelu. Pojedyncze rozpoczęte wywołanie modelu może przekroczyć wskazaną kwotę. Żadne kolejne wywołanie nie powinno być uruchomione po wykryciu przekroczenia. Jeśli cennik modelu jest nieznany, zadanie z maksymalnym kosztem zostaje zablokowane zamiast pracować bez kontroli.

Anulowanie jest egzekwowane przez tokeny .NET oraz istniejące limity pojedynczych operacji. Czynność obca, która całkowicie ignoruje anulowanie (np. zawieszony renderer lub nieprzerywalna natywna operacja), może wymagać osobnego mechanizmu eskalacji na poziomie procesu. Watchdog nie obiecuje fizycznego wyłączenia już rozpoczętego wywołania HTTP po stronie serwera modelu.

Nie usuwa się ani nie resetuje uprawnień, istniejących limitów domenowych, logów i historii. Włączanie nowego agenta lub drugiej przeglądarki nie jest potrzebne.

## Weryfikacja

- .NET 10 Release: kompilacja bez błędów.
- Testy jednostkowe watchdoga i walidacji: wszystkie zaliczone, w tym realne anulowanie przez zegar, bezczynność i koszt, pauza na odpowiedź człowieka, odrzucanie błędnych parametrów MCP.
- Pełna regresja: 1478 PASS i 9 FAIL. Osiem błędów w istniejących asercjach starszego promptu, którego wskazanych fraz nie było także w poprzednim commicie. Dziewiąty błąd dotyczy niezmienionego kodu WhatsAppBrowser, niezwiązanego z nowym watchdogiem.

