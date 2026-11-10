# OpenFill: audyt rzeczywistych przebiegów

Źródło: C:\Users\pboru\OpenFill\data\stable\logs\usage_202611.ndjson
Rekordy task: 100. Sukcesy: 34. Częściowe: 16. Failed: 3. Historyczne anulowania: 19. Interrupted: 3. Bez outcome: 21. Jawny user_stop: 0.
Wywołania modelu: 2788. Koszt według logów USD: 1.1425. Kandydaci do przeglądu: 34.
Zarejestrowane rekordy zero-call: 0. Przed AgentRunner: 0. Model exception: 0. Runtime exception: 0.

cancelled_legacy oznacza jedynie parę zdarzeń app/stop i session/cancel w tym samym runId. Stara aplikacja błędnie opisywała każdy Stop jako żądanie użytkownika; faktyczny inicjator pozostaje nieustalony. Oryginalnych rekordów nie zmieniono.
Status partial/failed nie dowodzi zbędnych operacji. Historyczne stopReason może być nieznane. Progi to selekcja do analizy, nie automatyczny stop.
Median szerokich kategorii nie używamy do automatycznej klasyfikacji, gdyż zadania mają różny zakres.

| Task | Kategoria | Wynik | Stop reason | Calls | Read req | Same reads | Read streak | Koszt USD |
|---|---|---|---|---:|---:|---:|---:|---:|
| OF-20261104-0005-1cdf | Other | cancelled_legacy | legacy_cancel_actor_unknown | 147 | 65 | 0 | 0 | 0.0601 |
| OF-20261103-1504-dcd2 | LinkedIn | cancelled_legacy | legacy_cancel_actor_unknown | 127 | 51 | 0 | 0 | 0.0688 |
| OF-20261104-0221-593e | Mail | success | model_finish | 120 | 54 | 1 | 8 | 0.0527 |
| OF-20261104-0225-b4d1 | Meta | cancelled | requester_cancel | 107 | 50 | 1 | 8 | 0.0523 |
| OF-20261103-2145-4373 | LinkedIn | partial | unknown_historical | 96 | 41 | 2 | 0 | 0.0406 |
| OF-20261103-1449-c915 | LinkedIn | cancelled_legacy | legacy_cancel_actor_unknown | 95 | 57 | 1 | 0 | 0.0374 |
| OF-20261104-0103-0b89 | Other | interrupted | upgrade_interrupted | 93 | 47 | 2 | 4 | 0.0375 |
| OF-20261104-0025-a2f3 | Other | cancelled_legacy | legacy_cancel_actor_unknown | 92 | 44 | 0 | 13 | 0.0396 |
| OF-20261104-0036-a90e | LinkedIn | cancelled_legacy | legacy_cancel_actor_unknown | 89 | 39 | 0 | 6 | 0.0363 |
| OF-20261104-0007-0c59 | LinkedIn | cancelled_legacy | legacy_cancel_actor_unknown | 83 | 29 | 0 | 0 | 0.0308 |
| OF-20261103-1359-f9b0 | LinkedIn | cancelled_legacy | legacy_cancel_actor_unknown | 80 | 49 | 2 | 0 | 0.034 |
| OF-20261103-1344-bc0f | LinkedIn | cancelled_legacy | legacy_cancel_actor_unknown | 70 | 37 | 0 | 0 | 0.0358 |
| OF-20261104-0025-899e | LinkedIn | success | unknown_historical | 70 | 30 | 1 | 5 | 0.0239 |
| OF-20261103-2352-401a | LinkedIn | cancelled_legacy | legacy_cancel_actor_unknown | 66 | 23 | 0 | 0 | 0.0313 |
| OF-20261103-1957-538d | Mail | partial | unknown_historical | 65 | 24 | 3 | 0 | 0.0201 |
| OF-20261104-0227-3819 | LinkedIn | cancelled | requester_cancel | 63 | 27 | 0 | 5 | 0.0217 |
| OF-20261103-2130-06d0 | LinkedIn | partial | unknown_historical | 62 | 21 | 0 | 0 | 0.0321 |
| OF-20261104-0052-3fcf | LinkedIn | success | model_finish | 46 | 12 | 0 | 13 | 0.0154 |
| OF-20261104-0220-7e9c | Meta | partial | model_partial | 44 | 18 | 1 | 7 | 0.0218 |
| OF-20261103-2158-73e0 | LinkedIn | failed | unknown_historical | 32 | 15 | 0 | 0 | 0.0122 |

Aktualna wersja telemetryki zapisuje ponadto executionMode, planDomReads i planActionAttempts. Brak tych pól w starszych rekordach oznacza brak pomiaru, nie zero czynności.
