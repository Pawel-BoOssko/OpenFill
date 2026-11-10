# OpenFill OF-000023: LinkedIn batch execution w DEV

Wake: OF-000023-A01-023011, start silnika 2026-11-03 02:30:11 +01:00 Warszawa.
Host: PBOX1. SOURCE: C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\

## Implementacja
- W ToolRegistry (SOURCE) dodano DEV-only `linkedin_send_batch` (na bazie istniejącego `LinkedInSendBatchService`, `ProcedureExecutor`, `LinkedInCurrentThreadSendAdapter`).
- Wejście: `authorized=true`, dokładny profil nadawcy, request_id, requester, chat i od 1 do 50 elementów `{profile_url,thread_url,message}`. Jedno wywołanie modelu dla całej sekwencji; każdy efekt ma osobny trwały operationId, exact preflight, stan i evidenceRef.
- Zatrzymanie/niepewny wynik: brak automatycznego ponowienia po attempted. Brak dowodu nie może zostać zapisany jako dostarczenie. Wywołanie nie jest rejestrowane w stable/LIVE.
- Nie tworzono duplikatu wykonawcy wsadowego i nie dotykano Airbnb (wyłączone z planu).

## Walidacja
- Rozszerzono `tools/of_imp_smoke/ToolRegistryEffectRegistrationSmoke.cs` o izolowany sprawdzian Build() dla stable/dev, parametry wymagane, odmowę authorized=false i odrzucenie nieprawidłowego scope bez dotykania WebView2.
- `dotnet run --project tools/of_imp_smoke/FileFilterSmoke.csproj -c Debug --no-restore -- of022-effect-registry-only`: `OF023_LINKEDIN_BATCH_REGISTRATION_14_CHECKS_OK`, exit 0. Nie wykonano zewnętrznego SEND.
- `dotnet publish src/OpenFill.App/OpenFill.App__v0.3__2026-10-12_2034.csproj -c Release --no-restore -v:q -o staging/of_000023`: exit 0, stare ostrzeżenia.
- SHA256 SOURCE ToolRegistry: `90D24A4029429E034E32DC6EE5F1415A0BEF076D94CDA3E6C84C5E8C91A530CF`.
- SHA256 opublikowanego Core.dll: `EB6E20DDC9CB2F4CA467E4344EFB6A3CDCFCE68D4548770830B86DE3B0FAB3F4`.
- SHA256 opublikowanego EXE: `6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52`.

## DEV aktywacja
- MCP przed zmianą: `{"working":[]}`.
- Backup starego DEV w `staging/of_000023_backup_pre_dev_activation`; poprzedni Core SHA256 `0F4A29AC6082FD1AEF4C4A43013AEAEA7F2853FC67F301D5AA58089CE93899C6`.
- Stop tylko `OpenFill-OFIMP-dev-20261031`; obserwowany przy stop tylko LIVE PID 252. Skopiowano `staging/of_000023` do aktywnego `staging/of_imp_20261031`; SHA Core identyczne z wydaniem.
- Start DEV: nowy PID 8220, `--instance dev`; MCP `openfill_info` odpowiedziało `{"working":[]}`.
- LIVE nadal PID 252, Core.dll SHA `694E48BAAA9D59DDF9B47553968D007C0353FFDA6FC9E402C3B5FB1A0001B89B` bez zmian.

## Co pozostało
- Pojedyncze realne wywołanie batch w autoryzowanym LinkedIn (konkretne profile, wątki i zatwierdzone teksty), obserwacja per-recipient outcome/evidenceRef i trwała blokada ponowienia. Niewykonane, brak konkretnego zatwierdzonego zlecenia i sesji.
- Po realnym acceptance ocenić udostępnienie w stable. Nie interpretować obecnego testu izolowanego jako dostarczonej wiadomości.
- Rollback DEV: Stop-ScheduledTask, przywrócenie backup do staging/of_imp_20261031, Start-ScheduledTask, odczyt MCP.
