# OF-000020-A01-012959: LinkedIn effect, DEV routing

Start wybudzenia: 2026-11-03 01:29:59 +01:00 (Warszawa). Handoff: D:\OpenBrowserLiteHandoffs\of\HANDOFF_CURRENT.md na SATANAMD, poprzedni wake 000019. Poprzedni adres ChatGPT nieustalony.

## Zmiana SOURCE na PBOX1
- src/OpenFill.Core/Agent/ToolRegistry__v0.22__2026-10-29_2314.cs: dodano do Build() nowe narzędzie `linkedin_send_current_effect` WYŁĄCZNIE w DEV (_candidateStagingOnly), obok istniejącego read-only `linkedin_verify_thread`.
- Parametry: authorized, business_operation_id, effect_id, task_id, run_id, sender_profile_url, thread_url, target_profile_url, message.
- Handler wylicza SHA256 dokładnej wiadomości, tworzy LocalPlanEffectKey i wywołuje istniejący LinkedInLocalPlanEffectBridge.SendCurrentAsync na aktywnej karcie. Dowód trwały zapisuje w dev/memory/evidence.
- Most wykonuje journal attempted przed realnym adapterem, sprawdza dokładny wątek i after DOM, zapisuje verified tylko z dowodem, brak automatycznego resend. Tekst jest zachowywany w ledger.
- Nie zmieniono AgentRunner.RunAsync ani stabilnej rejestracji narzędzi.

## Sprawdzenie
- dotnet build OpenFill.App -c Release --no-restore -v:q: exit 0, 0 errors, 6 poprzednich warnings.
- dotnet publish OpenFill.App -c Release --no-restore -v:q do staging/of_000020_linkedin_effect: exit 0.
- ToolRegistry SOURCE SHA256: 7F3ADD946A512F3A233E8D3AD38F6219ECE25E3150D6372AA8016D12B2A23EBC.
- Published OpenFill.exe SHA256: 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52.
- Obserwowane procesy przed publikacją: STAGING dev PID 7624, LIVE PID 252. Nie restartowano żadnego.
- Nie wywoływano providerowego SEND na prawdziwej sesji: nie ma potwierdzonego zestawu nadawca, odbiorca, wątek i tekst do testu.
- Nowe wydanie jest tylko osobnym artefaktem STAGING; nie jest załadowane przez działającą instancję dev ani LIVE.

## Kontynuacja
1. Uruchomić wydanie w kontrolowanej sesji DEV po zakończeniu aktualnych równoległych prac; sprawdzić widoczność linkedin_send_current_effect.
2. W wykrytej zalogowanej sesji zweryfikować realny wątek i dopiero dla konkretnie zatwierdzonego tekstu wywołać jeden effect_id. Weryfikować trwały operationId i evidenceRef; nie wykonywać seryjnych prób.
3. Zdecydować o wdrożeniu stable dopiero po potwierdzeniu rzeczywistej ścieżki. Nie nadpisywać pracy nad nowym WhatsAppLocalPlanEffectBridge, wykrytym w tym samym okresie.
