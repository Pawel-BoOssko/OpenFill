# OpenFill E03: act_locator mismatch diagnostics, LIVE 2026-11-04 22:23 CET

## Źródło problemu (dane, nie hipoteza)
- Audyt: docs/OPENFILL_EFFICIENCY_AUDIT_HANDOFF_20261104.md, ostatnie 100 task: 3757 model calls, 1.511 USD, 48 review candidates.
- Przebieg WhatsApp OF-20261104-1602-5fa9, runId 20261104_150235_3a5297, 111 model calls, 11 błędów act_locator o reason_code=target_text_mismatch, wszystkie action_attempted=false, 18 pageReadRequests, 24 CDP reads, 0.038444 USD, outcome=partial.
- Poprzednio act_locator zwracał sam kod mismatch bez faktycznego tekstu trafionego elementu. Model próbował wielokrotnie różnych expected_text i selektorów bez tej informacji.

## SOURCE i ograniczenia
- Zmodyfikowano src/OpenFill.Core/Agent/ToolRegistry__v0.22__2026-10-29_2314.cs: przy unikalnym selektorze i złym expected_text odpowiedź zawiera teraz observed_text, max 180 znaków aktualnej widocznej treści elementu, observed_count=1, action_attempted=false. Uzupełniono opis narzędzia o interpretację mismatch.
- Backup: ten sam plik z sufiksem .before_e03_act_locator_mismatch_20261104.
- Skrypt modyfikacji: tools/of_e03_act_locator_patch_20261104.py.
- Narzędzie nadal nie klika przy mismatch i nie ponawia automatycznie operacji mogącej mieć skutek.
- To korekta diagnostyki, nie automatyczny limit liczby wywołań modelu.

## Testy
- Node uruchomił oryginalny embedded JS wyodrębniony z C# z kontrolowanym DOM: tools/of_e03_act_locator_embedded_20261104.cjs, E03_ACT_LOCATOR_EMBEDDED_JS_7_CHECKS_OK, 7/7 PASS.
- Pierwszy test naturalnego WebView2 OF-20261104-2222-7f61: selector=h1, wynik selector_not_found, action_attempted=false; to nie jest test ścieżki mismatch.
- Drugi test na załadowanym DOM https://example.com/ OF-20261104-2223-bf40: read_view, następnie act_locator(selector=body, action=click, expected_host=example.com, expected_text=INNY_NAGLOWEK_DO_TESTU). Wynik reason_code=target_text_mismatch, action_attempted=false, observed_count=1, observed_text zaczynające się od aktualnej treści strony. Outcome success. Tym samym potwierdzono ścieżkę na prawdziwym WebView2 po wdrożeniu.
- Nie ma jeszcze porównywalnego rzeczywistego przebiegu WhatsApp potwierdzającego redukcję liczby calls/kosztu. Nie deklarować oszczędności procentowych.

## Kompilacja i wdrożenie
- Publish Release: staging/of_e03_act_locator_20261104, E03_PUBLISH_OK, exit 0. Trwał 324,52 s na obciążonym PBOX1, ostrzeżenia istniejące CS0168/CS8604/MSB3277.
- Pierwsza aktywacja DEV tools/of_e03_act_locator_20261104_activate_dev.ps1: DEV_COPY_FAILED_9 przy robocopy, Win32 ERROR 32 dla OpenFill.Core.dll i OpenFill.dll. Backup DEV zdążył się wykonać.
- Diagnoza tools/of_e03_dev_filelock_diag_20261104.ps1: wyłączony DEV task w stanie Ready, tylko proces LIVE PID19828; cztery pliki source/target DLL można było po chwili otworzyć z FileShare.None. Blokada była przejściowa. Dokładnego procesu blokującego w momencie pierwszej awarii nie ustalono.
- Powtórzona wyłącznie aktywacja DEV: tools/of_e03_dev_retry_20261104.ps1, bez powtarzania backupu, DEV_RELEASE_OK pid22928, Core SHA 11D2C9CA555486A8E1E2BBCB215BF8F8827BDDB0D89E2E8946D4440DE3F552D3.
- Canonical LIVE tools/of_imp_release_live_ALWAYS_20261101.ps1: BACKUP_OK, UPDATE_NOTICE_SENT, LIVE_RELEASE_START_IGNORE_ACTIVE_TASKS, LIVE_RELEASE_OK mcp=True exit 0.
- Rollback: C:/Users/pboru/OpenFill/app__rollback__before_release_20261104_222135.
- Niezależna weryfikacja: tools/of_e03_independent_live_verify_20261104.ps1, exit 0, E03_INDEPENDENT_LIVE_OK, PID23956, EXE C:/Users/pboru/OpenFill/app/OpenFill.exe, OpenFill-open Running, staging SHA=LIVE SHA 11D2C9CA555486A8E1E2BBCB215BF8F8827BDDB0D89E2E8946D4440DE3F552D3, MCP=True, working=0.
- Historyczny skrypt tools/of_000041_live_verify.ps1 zgłosił LIVE_SHA_UNEXPECTED, ponieważ porównuje SHA ze stałą poprzedniego wydania. Nie jest to błąd aktualnego LIVE, dowodem jest niezależny porównawczy test staging/live.
- P2-E03: poprawka SOURCE/DEV/LIVE i techniczny odbiór DONE, ale realny wpływ na redukcję kosztów w WhatsApp pozostaje do pomiaru na naturalnych przyszłych zadaniach.

## Status innych punktów
- A27: zamknięte technicznie i naturalnie, dokument OPENFILL_A27_DEFAULT_CACHE_LIVE_20261104.md.
- D04: odczytowe audyty YouTube i Meta; testowy Short YouTube przesłany jako szkic i usunięty, Meta file chooser blokuje video upload. Szczegóły OPENFILL_HANDOFF_CONTINUATION_20261104_2200.md.
- Nie oznaczać jako DONE: realne D01/D02 sends, prawdziwe D04 publikacje, E02 production learning, broader LocalPlan, naturalne GUI Stop oraz natywne WebView2 Ask acceptance.
- D03 Airbnb pozostaje anulowane. Wake projektu of pozostaje wyłączone.
