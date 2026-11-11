# OpenFill: częstotliwość intencji z audytu zadań

Źródło: OPENFILL_TOKEN_AUDIT_DATA_20261031.json. Klasyfikacja heurystyczna oparta na poleceniach i dostępnych tool_counts. Wyniki są szacunkami, nie ręcznie potwierdzonymi etykietami każdej czynności.

| Serwis | Intencja | Zadania | Model calls | Calls/zadanie | Koszt logowany USD |
| --- | --- | ---: | ---: | ---: | ---: |
| google_other | other_uncertain | 167 | 14560 | 87.2 | 6.0279 |
| meta | other_uncertain | 99 | 8430 | 85.2 | 3.6460 |
| other | other_uncertain | 237 | 7842 | 33.1 | 3.2190 |
| google_other | search_or_resolve | 72 | 4244 | 58.9 | 2.9778 |
| linkedin | search_or_resolve | 56 | 3924 | 70.1 | 5.0748 |
| other | search_or_resolve | 75 | 3215 | 42.9 | 1.5424 |
| linkedin | send_message | 57 | 3193 | 56.0 | 2.7063 |
| whatsapp | read_messages_or_data | 87 | 3064 | 35.2 | 1.1125 |
| whatsapp | other_uncertain | 30 | 2322 | 77.4 | 1.0956 |
| linkedin | diagnostic | 24 | 2242 | 93.4 | 1.6199 |
| linkedin | other_uncertain | 55 | 2108 | 38.3 | 1.7160 |
| google_other | read_messages_or_data | 52 | 2064 | 39.7 | 1.1593 |
| other | read_messages_or_data | 121 | 1983 | 16.4 | 1.2486 |
| other | diagnostic | 58 | 1672 | 28.8 | 0.8942 |
| google_other | diagnostic | 26 | 1588 | 61.1 | 0.7411 |
| linkedin | read_messages_or_data | 27 | 1139 | 42.2 | 1.0337 |
| airbnb | update_listing_or_price | 5 | 890 | 178.0 | 0.4754 |
| meta | edit_record_or_settings | 12 | 857 | 71.4 | 0.3514 |
| google_other | publish_or_comment | 13 | 832 | 64.0 | 0.5428 |
| whatsapp | send_message | 34 | 810 | 23.8 | 0.3207 |
| other | edit_record_or_settings | 22 | 725 | 33.0 | 0.2484 |
| linkedin | publish_or_comment | 15 | 601 | 40.1 | 0.5291 |
| meta | search_or_resolve | 16 | 597 | 37.3 | 0.1812 |
| linkedin | send_invitation | 14 | 540 | 38.6 | 0.2504 |
| other | send_message | 13 | 506 | 38.9 | 0.1734 |
| meta | diagnostic | 6 | 485 | 80.8 | 0.3039 |
| meta | read_messages_or_data | 10 | 418 | 41.8 | 0.1786 |
| whatsapp | diagnostic | 16 | 374 | 23.4 | 0.2171 |
| meta | publish_or_comment | 6 | 352 | 58.7 | 0.4006 |
| other | publish_or_comment | 8 | 287 | 35.9 | 0.1064 |
| airbnb | other_uncertain | 4 | 236 | 59.0 | 0.1714 |
| linkedin | edit_record_or_settings | 3 | 226 | 75.3 | 0.0872 |
| airbnb | read_messages_or_data | 11 | 217 | 19.7 | 0.0875 |
| google_other | edit_record_or_settings | 5 | 123 | 24.6 | 0.0238 |
| google_other | send_message | 3 | 113 | 37.7 | 0.0463 |
| gmail | other_uncertain | 2 | 92 | 46.0 | 0.0373 |
| other | extract_or_export | 4 | 90 | 22.5 | 0.0342 |
| google_other | send_invitation | 2 | 88 | 44.0 | 0.0174 |
| airbnb | publish_or_comment | 1 | 71 | 71.0 | 0.0324 |
| airbnb | search_or_resolve | 2 | 70 | 35.0 | 0.0360 |
| whatsapp | search_or_resolve | 1 | 55 | 55.0 | 0.0147 |
| airbnb | send_message | 1 | 31 | 31.0 | 0.0091 |
| airbnb | edit_record_or_settings | 2 | 24 | 12.0 | 0.0081 |
| youtube | publish_or_comment | 1 | 20 | 20.0 | 0.0062 |
| airbnb | diagnostic | 1 | 7 | 7.0 | 0.0025 |
| linkedin | extract_or_export | 1 | 2 | 2.0 | 0.0004 |

Nie klasyfikować kategorią 'other_uncertain' jako pojedynczą procedurą. Każdego kandydata wdrożenia potwierdzić na reprezentatywnych run_id z wynikowego JSON.
