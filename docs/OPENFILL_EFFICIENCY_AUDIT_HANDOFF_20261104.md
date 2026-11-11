# OpenFill: audyt rzeczywistych przebiegów

Źródło: C:\Users\pboru\OpenFill\data\stable\logs\usage_202611.ndjson
Rekordy task: 100. Sukcesy: 40. Częściowe: 27. Failed: 3. Historyczne anulowania: 15. Interrupted: 7. Bez outcome: 0. Jawny user_stop: 0.
Wywołania modelu: 3757. Koszt według logów USD: 1.511. Kandydaci do przeglądu: 48.
Zarejestrowane rekordy zero-call: 0. Przed AgentRunner: 0. Model exception: 0. Runtime exception: 0.

cancelled_legacy oznacza jedynie parę zdarzeń app/stop i session/cancel w tym samym runId. Stara aplikacja błędnie opisywała każdy Stop jako żądanie użytkownika; faktyczny inicjator pozostaje nieustalony. Oryginalnych rekordów nie zmieniono.
Status partial/failed nie dowodzi zbędnych operacji. Historyczne stopReason może być nieznane. Progi to selekcja do analizy, nie automatyczny stop.
Median szerokich kategorii nie używamy do automatycznej klasyfikacji, gdyż zadania mają różny zakres.

| Task | Kategoria | Wynik | Stop reason | Calls | Read req | Same reads | Read streak | Koszt USD |
|---|---|---|---|---:|---:|---:|---:|---:|
| OF-20261104-0005-1cdf | Other | cancelled_legacy | legacy_cancel_actor_unknown | 147 | 65 | 0 | 0 | 0.0601 |
| OF-20261103-1504-dcd2 | LinkedIn | cancelled_legacy | legacy_cancel_actor_unknown | 127 | 51 | 0 | 0 | 0.0688 |
| OF-20261104-0221-593e | Mail | success | model_finish | 120 | 54 | 1 | 8 | 0.0527 |
| OF-20261104-1602-5fa9 | WhatsApp | partial | model_partial | 111 | 18 | 0 | 8 | 0.0384 |
| OF-20261104-0225-b4d1 | Meta | cancelled | requester_cancel | 107 | 50 | 1 | 8 | 0.0523 |
| OF-20261103-2145-4373 | LinkedIn | partial | unknown_historical | 96 | 41 | 2 | 0 | 0.0406 |
| OF-20261103-1449-c915 | LinkedIn | cancelled_legacy | legacy_cancel_actor_unknown | 95 | 57 | 1 | 0 | 0.0374 |
| OF-20261104-0103-0b89 | Other | interrupted | upgrade_interrupted | 93 | 47 | 2 | 4 | 0.0375 |
| OF-20261104-0025-a2f3 | Other | cancelled_legacy | legacy_cancel_actor_unknown | 92 | 44 | 0 | 13 | 0.0396 |
| OF-20261104-0036-a90e | LinkedIn | cancelled_legacy | legacy_cancel_actor_unknown | 89 | 39 | 0 | 6 | 0.0363 |
| OF-20261104-0007-0c59 | LinkedIn | cancelled_legacy | legacy_cancel_actor_unknown | 83 | 29 | 0 | 0 | 0.0308 |
| OF-20261104-1956-a066 | Other | cancelled | requester_cancel | 81 | 16 | 5 | 7 | 0.0302 |
| OF-20261104-2129-f517 | LinkedIn | success | model_finish | 73 | 28 | 1 | 4 | 0.0288 |
| OF-20261104-2130-440c | Other | cancelled | requester_cancel | 73 | 60 | 1 | 4 | 0.0366 |
| OF-20261104-0025-899e | LinkedIn | success | unknown_historical | 70 | 30 | 1 | 5 | 0.0239 |
| OF-20261104-1636-e2b6 | LinkedIn | success | model_finish | 67 | 28 | 3 | 8 | 0.0238 |
| OF-20261103-2352-401a | LinkedIn | cancelled_legacy | legacy_cancel_actor_unknown | 66 | 23 | 0 | 0 | 0.0313 |
| OF-20261103-1957-538d | Mail | partial | unknown_historical | 65 | 24 | 3 | 0 | 0.0201 |
| OF-20261104-0227-3819 | LinkedIn | cancelled | requester_cancel | 63 | 27 | 0 | 5 | 0.0217 |
| OF-20261103-2130-06d0 | LinkedIn | partial | unknown_historical | 62 | 21 | 0 | 0 | 0.0321 |

Aktualna wersja telemetryki zapisuje ponadto executionMode, planDomReads i planActionAttempts. Brak tych pól w starszych rekordach oznacza brak pomiaru, nie zero czynności.
