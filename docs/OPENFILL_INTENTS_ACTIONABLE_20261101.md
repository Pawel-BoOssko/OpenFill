# OF-IMP E01: uzupełnienie klasyfikacji nieznanych intencji

Zadania: 1477, model calls: 73329.
Nieznane przed: 561 zadań, 33740 calls.
Nieznane po: 426 zadań, 17193 calls.
Rozpoznano dodatkowo: 135 zadań.

## Kandydaci do procedur (ranking według historycznych calls, nie ROI)

| Serwis | Intencja | Zadania | Calls | Calls/zadanie | Koszt logowany USD |
| --- | --- | ---: | ---: | ---: | ---: |
| youtube_studio | upload_schedule_short | 28 | 5810 | 207.5 | 1.6156 |
| linkedin | send_message | 84 | 4343 | 51.7 | 3.5616 |
| meta_business | reel_editor_workflow | 25 | 2854 | 114.2 | 0.8940 |
| whatsapp | send_message | 76 | 2749 | 36.2 | 0.9352 |
| linkedin | send_invitation | 35 | 2086 | 59.6 | 1.4907 |
| airbnb | edit_price_calendar | 5 | 890 | 178.0 | 0.4754 |
| youtube_studio | audit_channel_readonly | 4 | 473 | 118.2 | 0.1548 |
| linkedin | publish_comment | 11 | 429 | 39.0 | 0.3875 |
| meta_other | publish_comment | 4 | 405 | 101.2 | 0.4084 |
| meta_business | schedule_page_posts | 4 | 398 | 99.5 | 0.2039 |
| meta_other | reel_editor_workflow | 5 | 288 | 57.6 | 0.0873 |
| youtube_studio | configure_channel | 3 | 263 | 87.7 | 0.1519 |
| google_analytics_gtm | audit_analytics_gtm | 3 | 245 | 81.7 | 0.1167 |
| meta_other | schedule_page_posts | 3 | 238 | 79.3 | 0.1957 |
| meta_other | send_message | 5 | 143 | 28.6 | 0.0399 |
| airbnb | send_message | 6 | 130 | 21.7 | 0.0385 |
| gmail | send_message | 3 | 128 | 42.7 | 0.0481 |

## Do weryfikacji: klasyfikacja średniej pewności

| Serwis | Intencja | Zadania | Calls |
| --- | --- | ---: | ---: |
| linkedin | search_resolve | 52 | 3739 |
| google_analytics_gtm | configure_analytics_gtm | 19 | 1560 |
| whatsapp | read_thread_or_data | 49 | 1485 |
| linkedin | diagnose_test | 12 | 1209 |
| meta_business | audit_page_readonly | 11 | 1020 |
| meta_other | meta_developer_configuration | 11 | 1004 |
| google_business | search_resolve | 10 | 727 |
| meta_business | edit_record_settings | 11 | 706 |
| linkedin | read_thread_or_data | 17 | 549 |
| meta_business | meta_developer_configuration | 5 | 525 |
| google_analytics_gtm | read_thread_or_data | 8 | 516 |
| gmail | search_resolve | 6 | 496 |
| meta_business | search_resolve | 5 | 412 |
| youtube_other | search_resolve | 4 | 404 |
| meta_business | diagnose_test | 5 | 400 |
| meta_other | search_resolve | 11 | 374 |
| youtube_studio | diagnose_test | 6 | 356 |
| google_analytics_gtm | search_resolve | 5 | 351 |
| meta_business | read_thread_or_data | 8 | 348 |
| whatsapp | diagnose_test | 15 | 317 |
| linkedin | edit_record_settings | 4 | 239 |
| meta_other | audit_page_readonly | 10 | 229 |
| google_analytics_gtm | diagnose_test | 3 | 179 |
| meta_other | read_thread_or_data | 4 | 156 |
| airbnb | read_thread_or_data | 6 | 118 |
| gmail | diagnose_test | 3 | 36 |

## Pozostałe unknown według serwisu

| Serwis | Zadania | Calls |
| --- | ---: | ---: |
| other | 208 | 6263 |
| google_other | 73 | 3056 |
| linkedin | 46 | 1673 |
| whatsapp | 15 | 1631 |
| meta_business | 21 | 1257 |
| gmail | 25 | 1071 |
| youtube_studio | 13 | 799 |
| meta_other | 14 | 651 |
| youtube_other | 5 | 417 |
| airbnb | 4 | 236 |
| google_drive_docs | 1 | 75 |
| google_analytics_gtm | 1 | 64 |

## Ograniczenia
- Task text is truncated and sometimes contains replacement characters.
- Intent is a heuristic; logged outcome is not proof of an external effect.
- Calls measure historical model usage, not verified savings.
- Reel editor and scheduled follow-up labels do not assert publication.
- Candidate ranking is by historical model calls, not ROI or business priority.
- Wiele run może być próbami tej samej czynności biznesowej, nie liczbą niezależnych publikacji.

Pełne grupy, confidence, reguły i przykładowe run IDs są w JSON.
