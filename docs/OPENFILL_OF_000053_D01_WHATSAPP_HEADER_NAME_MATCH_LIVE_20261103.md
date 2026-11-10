# OF-000053: WhatsApp, poprawne potwierdzenie nazwy otwartej rozmowy

Wybudzenie: OF-000053-A01-124030, 2026-11-03 12:40:30 +01:00 (Warszawa). Host SOURCE, DEV, LIVE: PBOX1.

## Przyczyna, potwierdzona logiem
- Poprzednie rzeczywiste acceptance OF-20261103-1420-9234, runId 20261103_112041_f6c653: whatsapp_read_names dwukrotnie zwrócił conversation_not_ready. W tym samym przebiegu whatsapp_read_visible pokazywał conversation_open=true i composer_visible=true, ale conversation_header zawierał inicjały awatara przed nazwą kontaktu. Metoda WaitForNamedConversationAsync porównywała z pełną nazwą całe header.innerText, dlatego poprawnie otwarta rozmowa była odrzucana.
- Niezależne błędy w historycznym przebiegu: whatsapp_open dla nazwy kontaktu zwrócił unsupported_identifier_for_direct_open, bo wymaga numeru; numer przekierowany przez bezpośredni URL zwrócił conversation_not_ready_after_navigation. Nie mylić tych ścieżek z otwarciem po nazwie.

## Zmiana SOURCE
- Plik src/OpenFill.Core/Browser/WhatsAppBrowser__v0.1__2026-10-29_2310.cs, kopia .before_of000053.
- VisibleScript zbiera conversation_header_labels z widocznych etykiet nagłówka (span[title], span[dir=auto], jawny data-testid tytułu rozmowy), zachowując stare conversation_header.
- WaitForNamedConversationAsync akceptuje pełne, dokładne dopasowanie nazwy z tych etykiet przy jednocześnie widocznym composerze. Nie stosuje fuzzy substring ani końcówkowego dopasowania całego tekstu nagłówka. Nie zmieniono wysyłania ani pamięci SQLite.
- Skrypt tools/of_000053_activate_dev.ps1: po Stop-ScheduledTask kończy tylko stare procesy DEV o dokładnej ścieżce EXE, aby robocopy nie napotykał ERROR 32.

## Kompilacja i wydanie
- dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000053 -v:q: exit 0, bez nowych błędów; istniejące ostrzeżenia.
- DEV: DEV_RELEASE_OK, PID 14156; backup staging/of_000053_backup_pre_dev_activation.
- CheckOnly: RELEASE_READY_NO_IDLE_GATE.
- LIVE: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True, exit 0, bez czekania na idle.
- Core SHA256: 92E1D8234B084923F57EAFB321A643BBF5EBCA05060272798B7F3B73F83AB10B.
- Niezależnie: LIVE PID 18468; EXE C:/Users/pboru/OpenFill/app/OpenFill.exe; Core SHA zgodny; EXE SHA256 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52; zadanie OpenFill-open phase Running; odczyt MCP openfill_info working=[].
- Rollback: C:/Users/pboru/OpenFill/app__rollback__before_release_20261103_144748.

## Rzeczywiste acceptance i zakres potwierdzenia
- Pierwsze uruchomienie po wydaniu: OF-20261103-1449-a525, status partial, whatsapp_read_names -> sidebar_not_loaded, observed_count=0, gdy WhatsApp inicjalizował sidebar; koszt 0.001 USD.
- Jedno dokończenie na tej samej karcie: OF-20261103-1450-0388, item status loaded, observed_count=1, koszt 0.0011 USD, całościowy outcome partial. To potwierdza funkcjonalne otwarcie rozmowy według dokładnej nazwy i odczyt załadowanej wiadomości. Nie jest potwierdzeniem kompletności historii ani wysyłki.
- Otwarta drobna obserwacja: pierwsza nowa karta może nie zainicjalizować sidebar w dotychczasowym limicie whatsapp_read_names (8 s); po inicjalizacji read działa. Nie powtarzać pełnych testów bez potrzeby.
- D01 i D02 rzeczywiste batch send nadal PARTIAL bez zatwierdzonych odbiorców/treści; D03 Airbnb ANULOWANE, nie wznawiać.
- Wake i harmonogram projektu of pozostawiono bez zmian. Następny planowy handoff po 000059.
