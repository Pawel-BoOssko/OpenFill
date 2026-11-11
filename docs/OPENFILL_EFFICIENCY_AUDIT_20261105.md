# OpenFill: audyt rzeczywistych przebiegów

Źródło: C:\Users\pboru\OpenFill\data\stable\logs\usage_202611.ndjson
Rekordy task: 100. Sukcesy: 49. Częściowe: 28. Failed: 2. Historyczne anulowania: 4. Interrupted: 9. Bez outcome: 0. Jawny user_stop: 0.
Wywołania modelu: 3191. Koszt według logów USD: 1.2427. Kandydaci do przeglądu: 43.
Zarejestrowane rekordy zero-call: 0. Przed AgentRunner: 0. Model exception: 0. Runtime exception: 0.

cancelled_legacy oznacza jedynie parę zdarzeń app/stop i session/cancel w tym samym runId. Stara aplikacja błędnie opisywała każdy Stop jako żądanie użytkownika; faktyczny inicjator pozostaje nieustalony. Oryginalnych rekordów nie zmieniono.
Status partial/failed nie dowodzi zbędnych operacji. Historyczne stopReason może być nieznane. Progi to selekcja do analizy, nie automatyczny stop.
Median szerokich kategorii nie używamy do automatycznej klasyfikacji, gdyż zadania mają różny zakres.

| Task | Kategoria | Wynik | Stop reason | Calls | Read req | Same reads | Read streak | Koszt USD |
|---|---|---|---|---:|---:|---:|---:|---:|
| OF-20261104-0221-593e | Mail | success | model_finish | 120 | 54 | 1 | 8 | 0.0527 |
| OF-20261104-1602-5fa9 | WhatsApp | partial | model_partial | 111 | 18 | 0 | 8 | 0.0384 |
| OF-20261104-0225-b4d1 | Meta | cancelled | requester_cancel | 107 | 50 | 1 | 8 | 0.0523 |
| OF-20261104-0103-0b89 | Other | interrupted | upgrade_interrupted | 93 | 47 | 2 | 4 | 0.0375 |
| OF-20261104-0025-a2f3 | Other | cancelled_legacy | legacy_cancel_actor_unknown | 92 | 44 | 0 | 13 | 0.0396 |
| OF-20261104-0036-a90e | LinkedIn | cancelled_legacy | legacy_cancel_actor_unknown | 89 | 39 | 0 | 6 | 0.0363 |
| OF-20261104-2152-ce98 | Meta | partial | model_partial | 87 | 39 | 2 | 6 | 0.0282 |
| OF-20261104-1956-a066 | Other | cancelled | requester_cancel | 81 | 16 | 5 | 7 | 0.0302 |
| OF-20261104-2152-c13d | Other | success | model_finish | 79 | 36 | 3 | 18 | 0.0259 |
| OF-20261104-2129-f517 | LinkedIn | success | model_finish | 73 | 28 | 1 | 4 | 0.0288 |
| OF-20261104-2130-440c | Other | cancelled | requester_cancel | 73 | 60 | 1 | 4 | 0.0366 |
| OF-20261104-1636-e2b6 | LinkedIn | success | model_finish | 67 | 28 | 3 | 8 | 0.0238 |
| OF-20261104-0227-3819 | LinkedIn | cancelled | requester_cancel | 63 | 27 | 0 | 5 | 0.0217 |
| OF-20261105-0814-fb72 | LinkedIn | partial | model_partial | 61 | 27 | 1 | 3 | 0.0189 |
| OF-20261104-1726-2701 | Other | success | model_finish | 60 | 9 | 1 | 8 | 0.0243 |
| OF-20261104-2109-0729 | LinkedIn | partial | model_partial | 59 | 28 | 2 | 9 | 0.0218 |
| OF-20261104-2120-853b | LinkedIn | partial | model_partial | 56 | 22 | 2 | 4 | 0.021 |
| OF-20261104-2225-a087 | LinkedIn | partial | model_partial | 54 | 22 | 0 | 10 | 0.0191 |
| OF-20261104-0052-3fcf | LinkedIn | success | model_finish | 46 | 12 | 0 | 13 | 0.0154 |
| OF-20261104-0220-7e9c | Meta | partial | model_partial | 44 | 18 | 1 | 7 | 0.0218 |

Aktualna wersja telemetryki zapisuje ponadto executionMode, planDomReads i planActionAttempts. Brak tych pól w starszych rekordach oznacza brak pomiaru, nie zero czynności.
