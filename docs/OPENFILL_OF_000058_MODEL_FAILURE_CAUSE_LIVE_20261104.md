# OpenFill OF-000058: źródło błędu modelu i właściwa klasyfikacja, LIVE

Wake: OF-000058-A01-000916, 2026-11-04 00:09:16 +01:00 (Warszawa). Host: PBOX1.

## Potwierdzone dane i przyczyna
- Na początku wybudzenia LC MultiHost PBOX1 zgłosił `target_unavailable: fetch failed`, a Remote Desktop Commander nie odpowiedział. SATANAMD widział PBOX1 w Tailscale, ale początkowo zwykły ping gubił 2/2 i TCP443 nie odpowiadał. Później `tailscale ping openfill-pbox1` zwrócił pong ~189 ms, TCP445 odpowiadał, a LC MultiHost PBOX1 wrócił. Przyczyna krótkiej przerwy w konektorach pozostaje nieustalona, nie tworzono mostów.
- W realnym logu zadania `OF-20261104-0207-ae54` (`runId=20261103_230755_168261`) zapisano po 4 model calls status `failed`, przyczynę `model_non_success` i komunikat `Cannot reach OpenAI: Nieznany host. (api.openai.com:443)`. Problem dotyczył połączenia z API, nie stwierdzonej autonomicznej decyzji Luny.
- Dwa pozostałe najnowsze realne taski: `OF-20261104-0158-48d7` partial (26 model calls, niepotwierdzone wysyłki) oraz `OF-20261104-0203-cdb0` success (49 model calls). Nie wznawiano ich i nie zmieniano danych użytkownika.

## Źródło w SOURCE
- `AgentRunner.RunAsync` łapał błędy transportowe `HttpRequestException`, timeout i `ModelException`, ale zwracał `RunConclusion("failed", summary, null)`, bez osobnej przyczyny.
- `TaskRun.FillAsync` przypisywał każdemu innemu niż success/partial statusowi `model_non_success`, nawet gdy powodem był DNS.
- `McpTaskManager.Complete` miał domyślną kategorię `model_or_runtime_error` dla całej grupy failed. `stop_reason` nie odróżniało braku połączenia od rzeczywistego niepowodzenia modelu.

## Zmiana SOURCE (P0)
- `Agent/ToolRegistry__v0.22__2026-10-29_2314.cs`: dodano opcjonalne `FinishReason` do `RunConclusion`. Wsteczna kompatybilność czterech wcześniejszych pól i istniejących konstruktorów zachowana.
- `Agent/AgentRunner__v0.29__2026-10-29_2314.cs`: konkretne kody `model_network_error` (HttpRequestException), `model_response_timeout` (response timeout), `model_configuration_error` (ModelException status 0), `model_api_error` (inny ModelException). Zachowano liczbę ponowień i istniejące komunikaty użytkownika.
- `Hosting/TaskRun__v0.1__2026-10-07_1732.cs`: końcowy `stopReason` z `conclusion.FinishReason` ma pierwszeństwo przed dotychczasowym fallbackiem; `session/end` zawiera stopReason. Existing cancellation user_stop, requester_cancel, upgrade_interrupted i timeout pozostają bez zmian.
- `Mcp/McpTaskManager__v0.21__2026-10-28_2216.cs`: `rec.StopReason` pobiera jawny `c.FinishReason` przed mapowaniem samego statusu. Rekord trwały i `openfill_task_status` pokazują właściwą kategorię.
- Cztery kopie plików `.before_of000058` zapisano przed edycją. Nie modyfikowano provider adapters, WhatsApp/LinkedIn send, SQLite/ledger, LocalPlan routing ani wybudzania.

## Publikacja
- `dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000058 -v:q`: PASS exit 0. Istniejące ostrzeżenia CS8604, CS0168 i WindowsBase; brak nowego błędu.
- DEV: `tools/of_000058_activate_dev.ps1`, backup `staging/of_000058_backup_pre_dev_activation`, DEV_RELEASE_OK PID18828.
- CheckOnly: `RELEASE_READY_NO_IDLE_GATE`, Core SHA256 `26F0FED4F17952AC50D371A76955A31B2FCD555DDD471E836075A4FFBD155105`.
- LIVE: `tools/of_imp_release_live_ALWAYS_20261101.ps1`: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True, bez czekania na idle.
- Niezależny LIVE: PID13348, EXE `C:/Users/pboru/OpenFill/app/OpenFill.exe`, Core SHA256 `26F0FED4F17952AC50D371A76955A31B2FCD555DDD471E836075A4FFBD155105`, EXE SHA256 `6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52`, faza `Running`. OpenFill MCP `openfill_info` => `working=[]`.
- Rollback LIVE: `C:/Users/pboru/OpenFill/app__rollback__before_release_20261104_021730`.

## Granice dowodu i dalsze P0
- Potwierdzono błędną klasyfikację na historycznym realnym tasku, kompilację poprawki, SHA/PID/MCP i wydanie LIVE.
- Nie wymuszano awarii DNS po wdrożeniu. Właściwy `stop_reason=model_network_error` na nowym prawdziwym zadaniu pozostaje do naturalnej weryfikacji; nie twierdzić, że wykonano E2E.
- Następny etap: przy nowych zadaniach porównać `stop_reason`, realne modelCalls i outcome; dopiero wtedy wybierać dalszą politykę wznawiania. Nie wznawiać automatycznie świadomie zatrzymanych ani niedokończonych wysyłek.
- Produkcyjny `local_plan_v1_draft` nadal opt-in, E2E niepotwierdzony po odrzuceniu przez JEV; nie omijać blokad. D01/D02 batch send acceptance PARTIAL, D03 Airbnb ANULOWANE.
