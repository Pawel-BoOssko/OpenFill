# OF-000051: D01 WhatsApp, stabilniejsza tozsamosc wiadomości bez data-id, LIVE

Wake: OF-000051-A01-115114; 2026-11-03 11:51:14 +01:00 (Warszawa).
Host SOURCE / DEV / LIVE: PBOX1.

## Rozpoznany blad
W WhatsAppBatchService.ReadBatchAsync dla wiadomości bez data-id wykorzystywano klucz "inferred:<pozycja DOM>|<kierunek>|<treść>". Przy doładowaniu wcześniejszych wiadomości indeks ulega przesunięciu i ta sama wiadomość może zyskać nowy identyfikator w SQLite. Jednocześnie WhatsAppBrowser.VisibleScript dla wiadomości bez data-id deduplikował identyczne teksty i etykiety, przez co dwa rzeczywiście osobne powtórzenia mogły zniknąć z wyniku odczytu.

## Zmiany SOURCE
- src/OpenFill.Core/Browser/WhatsAppBrowser__v0.1__2026-10-29_2310.cs: deduplikacja po data-id tylko kiedy data-id istnieje; bez identyfikatora nie zgaduje że dwa osobne elementy DOM to duplikat na podstawie samego tekstu. Do struktury wiadomości dodano pre_plain_text z widocznego atrybutu data-pre-plain-text.
- src/OpenFill.Core/Agent/WhatsAppBatchService__v0.1__2026-10-31.cs: przy braku data-id identyfikator oparty na dostępnym nagłówku, kierunku, treści i kolejności wystąpienia tej samej treści; nie używa już ogólnego indeksu DOM. W memory zapisuje odpowiednio visible_header_occurrence lub inferred_content_occurrence.
- Kopie obu plików SOURCE: .before_of000051.
- Bez zmian w wysyłaniu, ledger wysyłek, autoryzacji, trasach WhatsApp ani danych historycznych. Nie wykonywano migracji lub czyszczenia istniejących wiadomości.

## Build i release
- dotnet publish src\\OpenFill.App -c Release --no-restore -o staging\\of_000051 -v:q: exit_code=0, ostrzeżenia dotychczasowe.
- DEV aktywacja tools/of_000051_activate_dev.ps1: DEV_RELEASE_OK, PID 27016, backup staging/of_000051_backup_pre_dev_activation.
- CheckOnly: RELEASE_READY_NO_IDLE_GATE.
- LIVE: tools/of_imp_release_live_ALWAYS_20261101.ps1: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True, bez bramki idle.
- Core SHA256: 471C90A7A802F3464F93C11140E00CF514226A78B865AF4B0D90186B12A1B3E8.
- Niezależna weryfikacja LIVE: PID 21464; EXE C:\\Users\\pboru\\OpenFill\\app\\OpenFill.exe; Core SHA zgodny; EXE SHA256 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52; OpenFill-open phase Running.
- Oddzielny odczyt MCP openfill_info: odpowiedź working=[].
- Rollback: C:\\Users\\pboru\\OpenFill\\app__rollback__before_release_20261103_135730.

## Ograniczenia i dalsza praca
- Składnia C# skompilowana i binaria zaktualizowane; nie wykonano nowego rzeczywistego WhatsApp chat acceptance. Nie twierdzić, że bez-id fallback jest absolutnie jednoznaczny: jeżeli nie ma data-id i data-pre-plain-text, identyfikacja jest przybliżona. Zmiana nie usuwa już zgromadzonych duplikatów historii.
- D01/D02 faktyczne skutkowe batch send nadal PARTIAL, bez zatwierdzonych odbiorców i dokładnych treści. D03 Airbnb ANULOWANE.
- Kolejny etap: jedno prawdziwe read-only acceptance D01 z zapisaniem danych i porównaniem identyfikatorów przy dwóch odczytach tej samej rozmowy, kiedy sesja i uzasadniony cel będą dostępne; albo odrębny rzeczywisty wieloetapowy workflow. Nie powtarzać example.com.
- Wake i harmonogram projektu of bez zmian.
