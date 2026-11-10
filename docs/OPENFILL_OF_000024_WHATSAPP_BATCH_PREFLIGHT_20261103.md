# OpenFill OF-000024: preflight WhatsApp batch przed attempted

Wybudzenie: OF-000024-A01-025007; początek silnika: 2026-11-03 02:50:07 +01:00 Warszawa.
Host: PBOX1. SOURCE: C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\

## Zmiana SOURCE
- Zmieniono `src/OpenFill.Core/Agent/WhatsAppBatchService__v0.1__2026-10-31.cs` w ścieżce `SendBatchAsync` (DEV-only ToolRegistry, nie LIVE).
- Poprzednio `ProcedureExecutor` nadawał stan `attempted` przed `WhatsAppBrowser.SendAsync`, a to dopiero próbowało otworzyć i zweryfikować gotowość rozmowy. Niedostępny wątek mógł zużyć próbę, pomimo braku kliknięcia Send.
- Teraz `ExecuteBatchAsync` otrzymuje `preflight`, które wywołuje `WhatsAppBrowser.OpenAsync(target)` i wymaga jednocześnie: `ok=true`, `conversation_open=true`, `composer_visible=true`. Niepowodzenie zostawia w journal stan `planned` i status `preflight_status=blocked`, bez działania skutkowego. Wynik rejestrowany jest per target.
- Właściwa akcja używa już istniejącego `actionWithOperation`, zapewniającego dokładne `operation_id`. Po atomowym `MarkAttempted` następuje `WhatsAppBrowser.SendAsync`.
- `verified` wymaga `status=sent_ui`, dokładnego `confirmation=new_outgoing_bubble_visible` oraz zarejestrowanej `view.messages`. Browser sprawdza nową, dokładną wychodzącą wiadomość po provider ID. To dowód UI, nie dostarczenia.
- Dowód zapisuje JSON z `operation_id`, `provider`, `account_id`, `target_id`, SHA256 dokładnej wiadomości, czasem i wynikiem UI. Zapis temp+File.Move, zanim `ProcedureExecutor` zapisze wynik `verified`. Niepewność i przerwanie po attempted pozostają bez auto-retry.
- Nie ruszano innych usług i nie tworzono nowej przeglądarki lub wykonawcy.

## QA i publikacja
- `dotnet publish src/OpenFill.App/OpenFill.App__v0.3__2026-10-12_2034.csproj -c Release --no-restore -v:q -o staging/of_000024`: exit 0, poprzednie ostrzeżenia, 0 błędów.
- `dotnet run --project tools/of_imp_smoke/FileFilterSmoke.csproj -c Debug --no-restore -- archflow-flow-only`: `ARCHFLOW_FLOW_15_CHECKS_OK`, exit 0.
- SHA256 SOURCE `WhatsAppBatchService.cs`: `92B6D625AD57E76A3DC2247EF3E3A49B211A2AA03161ECA92815D8C7A164BC0F`.
- SHA256 release/DEV `OpenFill.Core.dll`: `58573A1EACF2038F8CC3CBC81ECD794997C66A84066921A1CBCB6C9EE16954F0`.
- SHA256 release `OpenFill.exe`: `6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52`.

## Aktywacja
- MCP DEV przed aktywacją: `{"working":[]}`.
- Backup: `staging/of_000024_backup_pre_dev_activation`, poprzedni Core SHA `EB6E20DDC9CB2F4CA467E4344EFB6A3CDCFCE68D4548770830B86DE3B0FAB3F4`.
- Zatrzymano wyłącznie `OpenFill-OFIMP-dev-20261031`. Potwierdzono, że przy stop pozostał tylko LIVE PID 252.
- Wgrano `staging/of_000024` do `staging/of_imp_20261031`; SHA256 Core zgodny z release. Uruchomiono zadanie DEV.
- Rzeczywisty DEV PID 27232 z `--instance dev`. MCP `openfill_info` po aktualizacji: `{"working":[]}`. LIVE PID 252 pozostał bez zmian, Core LIVE SHA `694E48BAAA9D59DDF9B47553968D007C0353FFDA6FC9E402C3B5FB1A0001B89B`.

## Pozostałe
- Nie wykonano rzeczywistego SEND, brak konkretnego zatwierdzonego odbiorcy, tekstu i sesji WhatsApp w bieżącym zadaniu. Realny acceptance nowego preflight, attempt i dowodu w DOM pozostaje otwarty. Nie oznaczać D01 jako w pełni zweryfikowane w LIVE.
- Rollback DEV: Stop-ScheduledTask, kopia z backup do staging/of_imp_20261031, Start-ScheduledTask i odczyt MCP.
