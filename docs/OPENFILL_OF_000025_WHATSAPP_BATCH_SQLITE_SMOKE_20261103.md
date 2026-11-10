# OF-000025: end-to-end test ścieżki WhatsApp batch w SQLite, DEV aktywne

Wake: OF-000025-A01-031023. Start silnika: 2026-11-03 03:10:23 +01:00 Warszawa.
Host: PBOX1. SOURCE: C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\

## SOURCE
1. `src/OpenFill.Core/Agent/WhatsAppBatchService__v0.1__2026-10-31.cs`: wydzielono wąski `IWhatsAppBatchSendPort` z `OpenAsync` i `SendAsync`. Zwykła ścieżka produkcyjna nadal korzysta z dotychczasowego `WhatsAppBrowser` poprzez `WhatsAppBrowserBatchSendPort`; publiczna usługa otrzymała dodatkowy konstruktor pozwalający podać deterministyczny port testowy. Bieżące kontrakty read i send pozostają niezmienione.
2. `tools/of_imp_smoke/WhatsAppBatchEffectSmoke.cs`: jeden kontrolowany test całego `WhatsAppBatchService.SendBatchAsync` z realnym `ProcedureExecutor`, rzeczywistym `OperationalMemoryStore` SQLite i zapisami dowodu, ale bez WebView2 ani zewnętrznych wysyłek.
3. `tools/of_imp_smoke/Program.cs`: nowa flaga `of025-whatsapp-batch-only`.
4. Test obejmuje: dwa różne odbiorniki, zablokowany wątek bez próby send, drugi verified i dokładnie jedno wywołanie, plik dowodu z operation ID/nadawcą/odbiorcą/hashem, wznowienie po naprawie widoku z pominięciem verified, restart `OperationalMemoryStore`, uncertain bez replay.
5. Pierwsza kompilacja testu: CS0121, niejednoznaczne delegowanie konstruktora `this(null!, memory)` do przeciążonych typów; usunięto przyczynę przez jawne `this((WhatsAppBrowser)null!, memory)`. Druga próba exit 0. Nie wykonano dodatkowych serii testowych.

## Weryfikacja i wydanie
- `dotnet run --project tools/of_imp_smoke/FileFilterSmoke.csproj -c Debug --no-restore -- of025-whatsapp-batch-only`: `OF025_WHATSAPP_BATCH_15_CHECKS_OK`, exit 0.
- `dotnet publish src/OpenFill.App/OpenFill.App__v0.3__2026-10-12_2034.csproj -c Release --no-restore -v:q -o staging/of_000025`: exit 0, 0 errors, starsze ostrzeżenia.
- SOURCE `WhatsAppBatchService.cs` SHA256: `794FD158AEB7BFA5D6782D7B66B665FB3C4B3FAFF7A58619510F4D7273A207FA`.
- Release `OpenFill.Core.dll` SHA256: `32FDB34455F70EF2CE0925B5795D8B4041EC4B96EF019C7D1A54D8DF5B9FDFC8`.
- Release `OpenFill.exe` SHA256: `6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52`.

## Aktywacja DEV
- MCP przed zmianą: `{"working":[]}`. Backup starego DEV: `staging/of_000025_backup_pre_dev_activation`, poprzedni Core SHA256 `58573A1EACF2038F8CC3CBC81ECD794997C66A84066921A1CBCB6C9EE16954F0`.
- Zatrzymano wyłącznie zadanie `OpenFill-OFIMP-dev-20261031`; na liście procesów pozostał tylko LIVE PID 252.
- Kopiowano `staging/of_000025` do aktywnego `staging/of_imp_20261031`; potwierdzono dokładny SHA Core. Następnie `Start-ScheduledTask`.
- DEV nowy PID 22036 `--instance dev`, MCP `{"working":[]}`. LIVE PID 252 bez restartu; SHA LIVE z poprzedniego checkpointu `694E48BAAA9D59DDF9B47553968D007C0353FFDA6FC9E402C3B5FB1A0001B89B` (nie ponawiano odczytu w tym etapie).
- Rollback: Stop-ScheduledTask, przywrócenie backup do `staging/of_imp_20261031`, Start-ScheduledTask, sprawdzenie PID/MCP.

## Zakres potwierdzenia
- Potwierdzenie: wewnętrzna ścieżka batch i ledger SQLite z symulowanym adapterem, bez podwójnego send i z prawidłowymi dowodami.
- Nie potwierdzono: wykonania rzeczywistej wysyłki w zalogowanej sesji WhatsApp ani dostarczenia do urządzenia. Nie wolno traktować testów izolowanych jako produkcyjnego acceptance D01.
- Następny etap: pojedynczy rzeczywisty opt-in na istniejącym autoryzowanym odbiorcy i wiadomości, albo kontynuacja innego nieukończonego zadania planu bez zewnętrznych side effects.
