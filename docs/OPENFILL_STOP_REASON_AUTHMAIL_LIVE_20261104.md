# OpenFill: rozróżnienie źródła zatrzymania i wyjątek dla uwierzytelniania w poczcie
Data: 2026-11-04, PBOX1.

## Rozpoznany błąd
AppHost.Stop(taskKey) oznaczał każdą anulowaną sesję jako żądanie użytkownika, również gdy stop pochodził z MCP (openfill_cancel_task) lub kontrolowanej aktualizacji. TaskRun.FillAsync na OperationCanceledException raportował failed i jeden wspólny komunikat "Task stopped / timed out". MCP rozróżniał częściowo status stopped_by_user/cancelled/interrupted, ale brakowało jednoznacznego, trwałego pola stop_reason. To wpływa na decyzję o dalszej pracy.

## SOURCE
- IRunHost: StopFromRequester, StopForMaintenance, z domyślnymi implementacjami kompatybilnymi ze starymi adapterami.
- AppHost: StopWithReason rozróżnia user_stop, requester_cancel, upgrade_interrupted; loguje app/stop z dokładnym powodem i taskKey. Pierwsza decyzja stop ma pierwszeństwo. Nie opisuje anulowania MCP jako kliknięcia użytkownika.
- TaskRun: SetStopReasonAccessor, odczyt przy anulowaniu; rozdzielenie statusów stopped, cancelled, interrupted oraz timeout/unknown cancellation; stopReason zapisywany w rzeczywistym usage kind=task i session/usage.
- McpTaskRecord: trwałe StopReason w JSON; Cancel -> requester_cancel, PrepareShutdown -> upgrade_interrupted, RecoverPersistedOrphan -> crash_interrupted; Complete nadaje model_finish/model_partial/model_blocked lub przyczynę niepowodzenia. Snapshot zwraca stop_reason i stop_actor.
- McpServer.Initialize, opis openfill_start_task i AgentRunner.SystemPrompt: API/MCP-first dotyczy zwykłych operacji pocztowych modelu zlecającego; Luna w istniejącym zadaniu może samodzielnie czytać w przeglądarce maile z kodami jednorazowymi, linkami logowania i danymi uwierzytelniania. To nie wymaga wywołania poczty przez zewnętrznego zlecającego.
- Backup przed edycją czterech plików: .before_stopcause_20261104. Zmiany nie dotyczyły adapterów wysyłania WhatsApp/LinkedIn, ich ledger ani bazy SQLite.

## Build i wdrożenie
- dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_stopreason_authmail_20261104 -v:q: exit 0, tylko znane ostrzeżenia.
- DEV: tools/of_stopreason_authmail_20261104_activate_dev.ps1, DEV_RELEASE_OK PID 2744, backup staging/of_stopreason_authmail_20261104_backup_pre_dev_activation.
- CheckOnly RELEASE_READY_NO_IDLE_GATE.
- LIVE tools/of_imp_release_live_ALWAYS_20261101.ps1: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True bez idle gate, exit 0.
- Niezależna kontrola PBOX1: LIVE PID 780, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, OpenFill-open Running, Core SHA256 537E859611C9BBAE037910547F17D1D46413A8320ED9051ADABE94637FC536CB. MCP openfill_info working=[].
- Rollback LIVE: C:/Users/pboru/OpenFill/app__rollback__before_release_20261104_004251.

## Zakres dowodu / następny krok
- Potwierdzono skompilowanie zmian, aktywne binaria LIVE i odpowiedź MCP. Nie twierdzić, że przeprowadzono sztuczny realny cancel-test.
- Na następnych rzeczywistych zakończonych zadaniach sprawdzić, czy stop_reason/stop_actor i usage stopReason odpowiadają pochodzeniu anulowania. Nie klasyfikować świadomego user_stop jako błędu modelu lub dowodu pętli.
- Nie wznawiać user_stop bez nowej decyzji użytkownika, nie ponawiać niepewnych skutków po upgrade/crash. Zachować rozróżnienie "stopped by requester" vs "stopped by person at window".
- Następne P0 po minimalnym acceptance: integracja LocalPlan do normalnego produkcyjnego routingu, bez duplikowania już istniejącego frameworku. D03 Airbnb anulowane.

Historyczne rekordy zadań sprzed tego wydania mogą nie mieć pola StopReason. Nie przypisywać im automatycznie winy użytkownika ani modelu; wymagają oddzielnego odczytu zdarzeń app/stop, session/cancel i danych task. Pozostawić stop_reason nieznany, gdy dowód nie istnieje.
