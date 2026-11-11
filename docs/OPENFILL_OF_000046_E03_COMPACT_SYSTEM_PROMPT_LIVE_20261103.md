# OF-000046: E03, krótka instrukcja systemowa dla prostego odczytu URL, LIVE

Wybudzenie: OF-000046-A01-101052. Czas silnika: 2026-11-03 10:10:52 +01:00 (Warszawa).
Host SOURCE, DEV i LIVE: PBOX1. Lead OF-000040..049.

## Ustalony koszt
- W OF-000045 ograniczono schemat narzedzi prostego odczytu URL do 18/49, ale AgentRunner przekazywal bez zmian pelny SystemPrompt z instrukcjami dotyczacymi WhatsApp, LinkedIn, wysylek, tworzenia kont, logowania i zlozonych formularzy.
- Rzeczywista telemetria z OF-000045: usage_202611.ndjson, runId 20261103_085547_15e742, pierwsza tura instructionsChars 7040, requestChars 26814 i 4731 inputTokens. Cala realizacja: 2 calls, 11039 inputTokens, 4728 cached, 105 output, 6157 modelMs, 0.00073088 USD, 1 pageReadRequest, 7 cdpReadCalls, outcome success.

## Zmiana SOURCE
- src/OpenFill.Core/Agent/AgentRunner__v0.29__2026-10-29_2314.cs: dodano stalą SimpleReadPrompt (~1007 znakow). Krótka instrukcja obejmuje traktowanie witryny jako danych, bezposrednie navigate(url,view_mode=content), ponowne odczyty tylko gdy potrzebne, zakaz skutkow ubocznych w trybie read-only, weryfikacje faktow, raportowanie brakow i finish w jezyku zadania.
- Dla istniejacego, wasko zdefiniowanego profilu IsSimpleUrlRead(task) instrukcje modelu wybiera simpleUrlRead ? SimpleReadPrompt : SystemPrompt. Wszystkie pozostale zadania zachowuja dotychczasowy pelny SystemPrompt, nie zmieniono ToolRegistry ani MCP.
- Telemetria RecordUsage: instructionsChars wyznaczane na podstawie faktycznego request["instructions"], nie stalego SystemPrompt.Length.
- SOURCE backup: ten sam plik z sufiksem .before_of000046.

## Publikacja
- dotnet publish src/OpenFill.App -c Release --no-restore -o staging/of_000046 -v:q: exit 0, jedynie dotychczasowe ostrzezenia.
- Core SHA256: F26DA273228C8CEC10032D86DD42C4A516ED4655FCD3E0302115B9B793590B8D.
- DEV: tools/of_000046_activate_dev.ps1, exit 0, PID 26428; backup staging/of_000046_backup_pre_dev_activation.
- CheckOnly: RELEASE_READY_NO_IDLE_GATE. LIVE: tools/of_imp_release_live_ALWAYS_20261101.ps1 exit 0, BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_OK mcp=True. Nie bylo bramki idle.
- LIVE: PID 14200, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, zgodny Core SHA, EXE SHA256 6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52, faza OpenFill-open Running, MCP OK, working=0.
- Rollback: C:/Users/pboru/OpenFill/app__rollback__before_release_20261103_121326.

## Realny LIVE acceptance
- Task OF-20261103-1214-0a14, runId 20261103_091414_425e6c: success, example.com, zwrocony tytul i pierwszy akapit, pierwsze narzedzie navigate(content), potem finish. 2 model calls, 2 tool calls, 0 tool errors, 1 pageReadRequest, 0 repeatedPageReadRequests, 7 CDP.
- agent.tool_schema_profile: simple_url_read, exposedTools 18, totalTools 49, schemaChars 17086.
- usage_202611.ndjson: instructionsChars=1007 (poprzednio 7040), first requestChars=20691 (vs 26814), first inputTokens=3577 (vs 4731). Calosc: inputTokens 8691 (vs 11039), cachedTokens 3574 (vs 4728), outputTokens 93 (vs 105), modelMs 5196 (vs 6157), costUsd 0.00059394 (vs 0.00073088). Różnica: -2348 inputTokens (21,3%), -0.00013694 USD (18,7%) w jednej próbie. Liczba CDP 7->7. Cache/timing rozne, nie ekstrapolowac kosztu na zlozone zadania.
- Potwierdzono realny LIVE PID, EXE, SHA, Running, MCP i wynik zadania.

## Kontynuacja
- E03 nadal PARTIAL dla analiz innych realnych zadan; wystarczy zmienic zestaw docelowych testow zamiast wielokrotnie powtarzac example.com.
- D01 WhatsApp, D02 LinkedIn: realne send acceptance bez autoryzowanych odbiorcow i tresci pozostaja PENDING. D03 Airbnb ANULOWANE 2026-11-02.
- Harmonogram wake bez zmian. Rotacja po OF-000049.
