# OF-244: Audyt wywołań MCP i okresu odpytywania

Dane: app_20261027/28/29.ndjson, wyłącznie rekordy source=mcp i eventType=request.
Bez treści zadań, task_id, domen i danych użytkowników.
Pojedynczy log może łączyć ruch kilku klientów. Bez client_id nie da się wiarygodnie przypisać odstępów do konkretnego czatu.

| Dzień UTC | Start | Status | Info | Inne | Razem |
|---|---:|---:|---:|---:|---:|
| 2026-10-27 | 28 | 437 | 93 | 17 | 575 |
| 2026-10-28 | 48 | 513 | 174 | 12 | 747 |
| 2026-10-29 | 16 | 20 | 50 | 2 | 88 |

## Odstępy między kolejnymi statusami w całym ruchu

- 2026-10-27: 437 wywołań status; odstępy 436; okolice 30s: 14; okolice 60s: 3; mediana 5.6s
- 2026-10-28: 513 wywołań status; odstępy 512; okolice 30s: 42; okolice 60s: 0; mediana 21.8s
- 2026-10-29: 20 wywołań status; odstępy 19; okolice 30s: 8; okolice 60s: 0; mediana 32.9s

## Godziny o największej liczbie zapytań statusowych

| Godzina UTC | Status calls |
|---|---:|
| 2026-10-27 11:00 UTC | 279 |
| 2026-10-28 10:00 UTC | 158 |
| 2026-10-28 09:00 UTC | 130 |
| 2026-10-27 12:00 UTC | 91 |
| 2026-10-28 15:00 UTC | 65 |
| 2026-10-28 11:00 UTC | 61 |
| 2026-10-28 18:00 UTC | 57 |
| 2026-10-27 10:00 UTC | 56 |
| 2026-10-28 12:00 UTC | 22 |
| 2026-10-29 07:00 UTC | 19 |
| 2026-10-27 17:00 UTC | 9 |
| 2026-10-28 17:00 UTC | 9 |
| 2026-10-28 19:00 UTC | 6 |
| 2026-10-28 16:00 UTC | 3 |
| 2026-10-27 23:00 UTC | 2 |

Łącznie rekordów MCP request: 1410. Błędy parsowania: 0.
Odstęp 30 sekund w logach nie dowodzi harmonogramu serwera: long-poll jest wywołaniem klienta, a licznik telemetrii jest lokalny.
Wniosek przyczynowy wymaga identyfikacji klientów i porównania liczników przed/po zmianie kodu, nie wolno automatycznie utożsamiać ruchu z pętlą.
