# OpenFill: architektura, ślady rzeczywistych wywołań, 2026-11-02

- Zakres: 1550 operacji biznesowych, 1574 runId, 76566 model calls, bez Airbnb.
- Funkcje: 76534 wywołań narzędzi w odpowiedziach modelu; 0 odpowiedzi z więcej niż jednym function_call.
- act -> get_page: 10238 z 17148 wywołań act mających następną odpowiedź modelu (59.7%). To nie jest dowód zbędnego odczytu.
- Model calls na operację: mediana 28, p90 116, p95 160.
- requestChars p50 125442, p95 435168.

## Najczęstsze wywołania narzędzi

| Narzędzie | Liczba |
|---|---:|
| get_page | 20591 |
| act | 17182 |
| run_js | 9014 |
| wait | 6031 |
| read_text | 5752 |
| navigate | 5491 |
| screenshot | 3368 |
| search_logs | 2462 |
| act_many | 1558 |
| get_network | 1420 |
| finish | 1281 |
| read_site_note | 671 |
| ask_user | 382 |
| upload_file | 193 |
| confirm_irreversible | 152 |
| report_verified_action | 113 |

## Najczęstsze przejścia między kolejnymi wywołaniami modelu

| Poprzednie | Następne | Liczba |
|---|---|---:|
| act | get_page | 10238 |
| get_page | act | 9039 |
| wait | get_page | 4562 |
| run_js | run_js | 4059 |
| act | wait | 3345 |
| get_page | read_text | 2970 |
| act | act | 2162 |
| get_page | screenshot | 1966 |
| read_text | act | 1598 |
| get_page | run_js | 1592 |
| navigate | act | 1396 |
| search_logs | search_logs | 1207 |
| run_js | act | 1192 |
| get_page | navigate | 1184 |
| navigate | read_text | 1176 |
| get_page | act_many | 1052 |
| run_js | get_page | 982 |
| act_many | get_page | 941 |

## Granice interpretacji

- One function_call is not automatically one model call; transitions count only single-tool responses.
- A repeated get_page may reflect a changed page, not necessarily redundant work.
- Data spans historical versions of OpenFill and cannot measure current LIVE after code changes.
- No business outcomes or delivery confirmations inferred from usage records.
