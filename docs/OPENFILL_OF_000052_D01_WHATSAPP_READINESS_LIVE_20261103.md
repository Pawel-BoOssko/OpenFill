# OF-000052: D01 WhatsApp readiness in tool, LIVE

Wake: OF-000052-A01-121115, 2026-11-03 12:11:15 +01:00 Warszawa. SOURCE, DEV, LIVE: PBOX1.

## Rzeczywista obserwacja
- Zadanie LIVE OF-20261103-1412-5f28 (runId 20261103_111236_b77b54), status partial. 9 model calls, koszt USD 0.0023, podglady 0, rozmowy 0. Dwa whatsapp_read_visible zwrocily ready=false, interface_status=not_loaded_or_login_required, preview_count=0. Po zakonczeniu przegladarka pokazala sidebar z rozmowami. Wynik poczatkowy pochodzil z niezainicjalizowanego DOM.
- Kontynuacja na tej samej karcie OF-20261103-1420-9234: status partial, 21 podgladow, 0 otwartych rozmow, koszt USD 0.0046, 16 krokow. Wykrywanie listy po zaladowaniu dziala. Otwieranie rozmow przez nazwe/telefon nie zostalo zaakceptowane. To osobny, nadal otwarty problem.

## SOURCE
- src/OpenFill.Core/Agent/ToolRegistry__v0.22__2026-10-29_2314.cs: whatsapp_read_visible wywoluje teraz istniejące ReadReadyAsync(ct, 18000) zamiast natychmiastowego ReadVisibleAsync(ct); opis narzedzia informuje o ready=false dla strony nadal ladowanej.
- src/OpenFill.Core/Browser/WhatsAppBrowser__v0.1__2026-10-29_2310.cs: wewnetrzny ReadReadyAsync odstep pomiedzy sondowaniami 400 -> 2500 ms; maksymalnie okolo 9 odczytow w 20 sekund. Gdy strona jest gotowa, wynik jest natychmiastowy.
- Backupy obu plikow: .before_of000052. Nie zmieniano wysylania, SQLite, kont, ani planu D03 Airbnb (anulowane).

## Build i release
- dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000052 -v:q: exit 0, bez nowych bledow, stare ostrzezenia.
- Pierwsza aktywacja DEV: DEV_COPY_FAILED_9, robocopy ERROR 32 dla DLL/EXE. Ustalono pozostajacy proces DEV PID 27016. Po zakonczonym Stop-Process -Id 27016 -Force ponowiono ten sam skrypt aktywacji: DEV_RELEASE_OK PID 8564.
- CheckOnly: RELEASE_READY_NO_IDLE_GATE, Core SHA256 A348220BD0F33D02D2E9428A22480E3DCB2980E167082915872A995004496860.
- LIVE: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True, aktywne zadania nie blokowaly wydania. Rollback C:/Users/pboru/OpenFill/app__rollback__before_release_20261103_143515.
- Niezalezna weryfikacja LIVE: PID 24072; EXE C:/Users/pboru/OpenFill/app/OpenFill.exe; phase Running; Core SHA256 A348220BD0F33D02D2E9428A22480E3DCB2980E167082915872A995004496860; EXE SHA256 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52; MCP openfill_info working=[].

## Stan i ograniczenia
- Poprawka READINESS jest wdrozona, lecz nie przeprowadzono po wydaniu nowego acceptance. Nie twierdzic, ze wszystkie rozmowy WhatsApp daja sie otwierac.
- Otwarty, potwierdzony symptom: 21 podgladow po inicjalizacji, 0 otwartych rozmow w drugiej probie. Nastepny etap: diagnoza na podstawie result reasons w logach, a dopiero potem jedna zmiana otwierania rozmow. Nie powtarzac masowo testow ani nie dopisywac nowych spekulacyjnych awarii.
- D01/D02 realne batch send nadal PARTIAL bez zatwierdzonych odbiorcow/tresci; D03 Airbnb ANULOWANE. Wake i harmonogram bez zmian.
