# OpenFill: historyczne zużycie vs oszacowany wzorzec, 2026-11-02

Etap 1: wykaz czynności z logów. Etap 2: zamrożony budżet normatywny. Etap 3: dopiero teraz koszty rzeczywiste.

**Wynik to potencjał optymalizacji, nie zaoszczędzone już pieniądze.**

## Pokrycie

- Sklasyfikowanych i porównanych przebiegów: 1099, rozpoznanych z rzeczywistym zużyciem: 1552.
- Bez rozpoznanej intencji, lecz z logiem zużycia: 453.
- Bez pomiaru użycia: 0.

## Łączny wynik z oszacowaniem

| Metryka | Rzeczywiste | Referencyjne | Różnica |
|---|---:|---:|---:|
| Wywołania modelu | 57,438.000 | 3,492.000 | 53,946.000 |
| Koszt USD | 33.133 | 3.492 | 29.641 |
| Tokeny input uncached | 211,648,137.000 | 7,028,300.000 | 204,619,837.000 |
| Odczyty widoku, przybliżenie | 26,498.000 | 2,198.000 | 24,300.000 |

## Grupy czynności

| Serwis | Intencja | Runs | Calls realne | Calls wzorzec | Nadwyżka | USD realne | USD wzorzec |
|---|---|---:|---:|---:|---:|---:|---:|
| youtube_studio | upload_schedule_short | 28 | 5810 | 168 | 5642 | 1.6156 | 0.1680 |
| linkedin | send_message | 84 | 4343 | 252 | 4091 | 3.5616 | 0.2520 |
| linkedin | search_resolve | 52 | 3739 | 156 | 3583 | 4.9672 | 0.1560 |
| google_other | search_resolve | 55 | 2956 | 165 | 2791 | 1.0529 | 0.1650 |
| meta_business | reel_editor_workflow | 25 | 2854 | 125 | 2729 | 0.8940 | 0.1250 |
| other | search_resolve | 71 | 2813 | 213 | 2600 | 1.2862 | 0.2130 |
| whatsapp | send_message | 76 | 2749 | 228 | 2521 | 0.9352 | 0.2280 |
| linkedin | send_invitation | 35 | 2086 | 105 | 1981 | 1.4907 | 0.1050 |
| other | read_thread_or_data | 120 | 1963 | 240 | 1723 | 1.0592 | 0.2400 |
| other | send_message | 42 | 1623 | 126 | 1497 | 0.8474 | 0.1260 |
| google_analytics_gtm | configure_analytics_gtm | 19 | 1560 | 133 | 1427 | 1.0148 | 0.1330 |
| whatsapp | read_thread_or_data | 49 | 1485 | 98 | 1387 | 0.5875 | 0.0980 |
| other | diagnose_test | 62 | 1603 | 248 | 1355 | 0.8687 | 0.2480 |
| linkedin | diagnose_test | 12 | 1209 | 48 | 1161 | 1.3135 | 0.0480 |
| youtube_studio | scheduled_short_followup | 4 | 1033 | 8 | 1025 | 0.3154 | 0.0080 |
| google_other | read_thread_or_data | 37 | 1066 | 74 | 992 | 0.4116 | 0.0740 |
| meta_business | audit_page_readonly | 11 | 1020 | 33 | 987 | 0.3713 | 0.0330 |
| google_other | diagnose_test | 16 | 1019 | 64 | 955 | 0.5477 | 0.0640 |
| meta_other | meta_developer_configuration | 11 | 1004 | 66 | 938 | 0.3144 | 0.0660 |
| google_business | search_resolve | 10 | 727 | 30 | 697 | 1.1642 | 0.0300 |
| meta_business | edit_record_settings | 11 | 706 | 44 | 662 | 0.2921 | 0.0440 |
| other | publish_comment | 13 | 651 | 39 | 612 | 0.2060 | 0.0390 |
| other | edit_record_settings | 19 | 606 | 76 | 530 | 0.2111 | 0.0760 |
| linkedin | read_thread_or_data | 17 | 549 | 34 | 515 | 0.3672 | 0.0340 |
| google_analytics_gtm | read_thread_or_data | 8 | 516 | 16 | 500 | 0.3892 | 0.0160 |
| meta_business | meta_developer_configuration | 5 | 525 | 30 | 495 | 0.1764 | 0.0300 |
| gmail | search_resolve | 6 | 496 | 18 | 478 | 0.2625 | 0.0180 |
| youtube_studio | audit_channel_readonly | 4 | 473 | 12 | 461 | 0.1548 | 0.0120 |
| google_other | publish_comment | 8 | 450 | 24 | 426 | 0.1990 | 0.0240 |
| meta_business | search_resolve | 5 | 412 | 15 | 397 | 0.1794 | 0.0150 |
| linkedin | publish_comment | 11 | 429 | 33 | 396 | 0.3875 | 0.0330 |
| meta_other | publish_comment | 4 | 405 | 12 | 393 | 0.4084 | 0.0120 |
| youtube_other | search_resolve | 4 | 404 | 12 | 392 | 0.6543 | 0.0120 |
| meta_business | schedule_page_posts | 4 | 398 | 16 | 382 | 0.2039 | 0.0160 |
| meta_business | diagnose_test | 5 | 400 | 20 | 380 | 0.2846 | 0.0200 |
| youtube_studio | schedule_existing_short | 1 | 360 | 3 | 357 | 0.1105 | 0.0030 |
| meta_other | search_resolve | 11 | 374 | 33 | 341 | 0.1125 | 0.0330 |
| google_analytics_gtm | search_resolve | 5 | 351 | 15 | 336 | 0.1477 | 0.0150 |
| meta_business | read_thread_or_data | 8 | 348 | 16 | 332 | 0.1295 | 0.0160 |
| youtube_studio | diagnose_test | 6 | 356 | 24 | 332 | 0.1024 | 0.0240 |
| meta_business | create_page | 1 | 330 | 6 | 324 | 0.7553 | 0.0060 |
| google_other | send_message | 4 | 319 | 12 | 307 | 0.1820 | 0.0120 |
| youtube_studio | read_thread_or_data | 2 | 282 | 4 | 278 | 0.1923 | 0.0040 |
| meta_other | reel_editor_workflow | 5 | 288 | 25 | 263 | 0.0873 | 0.0250 |
| whatsapp | diagnose_test | 15 | 317 | 60 | 257 | 0.2046 | 0.0600 |
| youtube_studio | configure_channel | 3 | 263 | 15 | 248 | 0.1519 | 0.0150 |
| google_analytics_gtm | audit_analytics_gtm | 3 | 245 | 9 | 236 | 0.1167 | 0.0090 |
| meta_other | schedule_page_posts | 3 | 238 | 12 | 226 | 0.1957 | 0.0120 |
| linkedin | edit_record_settings | 4 | 239 | 16 | 223 | 0.0893 | 0.0160 |
| google_analytics_gtm | publish_comment | 2 | 219 | 6 | 213 | 0.3045 | 0.0060 |
| meta_other | diagnose_test | 2 | 210 | 8 | 202 | 0.0545 | 0.0080 |
| meta_other | audit_page_readonly | 10 | 229 | 30 | 199 | 0.0791 | 0.0300 |
| google_drive_docs | read_drive_docs | 1 | 180 | 2 | 178 | 0.2149 | 0.0020 |
| gmail | edit_record_settings | 2 | 176 | 8 | 168 | 0.0573 | 0.0080 |
| google_analytics_gtm | diagnose_test | 3 | 179 | 12 | 167 | 0.0949 | 0.0120 |
| other | export_extract | 7 | 171 | 21 | 150 | 0.0565 | 0.0210 |
| meta_other | read_thread_or_data | 4 | 156 | 8 | 148 | 0.0586 | 0.0080 |
| meta_other | edit_record_settings | 1 | 151 | 4 | 147 | 0.0592 | 0.0040 |
| meta_other | send_message | 5 | 143 | 15 | 128 | 0.0399 | 0.0150 |
| gmail | send_message | 3 | 128 | 9 | 119 | 0.0480 | 0.0090 |
| airbnb | send_message | 6 | 130 | 18 | 112 | 0.0385 | 0.0180 |
| airbnb | read_thread_or_data | 6 | 118 | 12 | 106 | 0.0580 | 0.0120 |
| airbnb | publish_comment | 1 | 71 | 3 | 68 | 0.0324 | 0.0030 |
| meta_business | schedule_posts | 2 | 74 | 8 | 66 | 0.0168 | 0.0080 |
| airbnb | search_resolve | 2 | 70 | 6 | 64 | 0.0360 | 0.0060 |
| gmail | read_thread_or_data | 2 | 66 | 4 | 62 | 0.0252 | 0.0040 |
| google_other | export_extract | 2 | 65 | 6 | 59 | 0.0008 | 0.0060 |
| google_business | read_thread_or_data | 2 | 59 | 4 | 55 | 0.1087 | 0.0040 |
| youtube_studio | edit_record_settings | 2 | 60 | 8 | 52 | 0.0124 | 0.0080 |
| google_other | edit_record_settings | 3 | 63 | 12 | 51 | 0.0114 | 0.0120 |
| youtube_other | publish_comment | 1 | 48 | 3 | 45 | 0.0814 | 0.0030 |
| youtube_studio | send_invitation | 1 | 45 | 3 | 42 | 0.0082 | 0.0030 |
| meta_other | schedule_posts | 1 | 44 | 4 | 40 | 0.0159 | 0.0040 |
| meta_business | publish_comment | 2 | 44 | 6 | 38 | 0.0137 | 0.0060 |
| gmail | diagnose_test | 3 | 36 | 12 | 24 | 0.0094 | 0.0120 |
| google_drive_docs | read_thread_or_data | 1 | 21 | 2 | 19 | 0.0025 | 0.0020 |
| youtube_studio | publish_comment | 1 | 20 | 3 | 17 | 0.0062 | 0.0030 |
| airbnb | edit_record_settings | 2 | 24 | 8 | 16 | 0.0081 | 0.0080 |
| other | send_invitation | 1 | 18 | 3 | 15 | 0.0021 | 0.0030 |
| google_drive_docs | publish_comment | 1 | 10 | 3 | 7 | 0.0010 | 0.0030 |
| google_other | send_invitation | 1 | 9 | 3 | 6 | 0.0013 | 0.0030 |
| airbnb | diagnose_test | 1 | 7 | 4 | 3 | 0.0025 | 0.0040 |
| linkedin | export_extract | 1 | 2 | 3 | -1 | 0.0004 | 0.0030 |

## Ograniczenia

- Runtime ledger is not an external action acceptance proof.
- No confirmed mapping of continuation runs to a single business result; logical-operation savings cannot be claimed definitively until linked.
- Normative USD 0.001 per model call is an explicit assumption, not validated tariff.
- DOM read proxy includes some non-DOM run_js calls.
- Incomplete task-level November usage marked calls_only.
