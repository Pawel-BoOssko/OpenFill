# OpenFill OF-000029: promocja sprawdzonego wydania DEV do LIVE

Wake: OF-000029-A01-043006, 2026-11-03 04:30:06 +01:00 (Warszawa).
Host SOURCE i wydania: PBOX1.
Repo: C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\

## Decyzja
Aktualna nadrzędna zasada scalonego planu (2026-11-01) wymaga natychmiastowego wydania gotowej wersji, niezależnie od `working`. Poprzednie etapy OF-000021..OF-000028 zakończyły się zmianami SOURCE, testami i aktywną wersją DEV, lecz LIVE nadal miał stary Core. Ten etap wdraża cały aktualny kandydat DEV na LIVE, nie tworzy następnej niezweryfikowanej zmiany SOURCE.

## Stan i wykonanie
- Przed aktualizacją LIVE PID 252, Core SHA256 `694E48BAAA9D59DDF9B47553968D007C0353FFDA6FC9E402C3B5FB1A0001B89B`.
- DEV PID 9040, Core SHA256 `6F23153EA11500E9B1750127E3E779C6E049A9E656C03C22C491C8663DA623AA`, wydanie oparte o `staging/of_000028`.
- Sprawdzono `tools/of_imp_release_live_ALWAYS_20261101.ps1` i wykonano `-CheckOnly`: `RELEASE_READY_NO_IDLE_GATE` dla docelowego SHA.
- Uruchomiono ten sam zatwierdzony skrypt bez `-CheckOnly`: utworzono backup `C:\Users\pboru\OpenFill\app__rollback__before_release_20261103_063459`; MCP maintenance zwrócił `UPDATE_NOTICE_SENT`; wydanie uruchomiono bez bramki idle.
- Raport: `LIVE_RELEASE_OK core_sha256=6F23153EA11500E9B1750127E3E779C6E049A9E656C03C22C491C8663DA623AA mcp=True`. Exit 0, runtime 25.67 s.
- Po zmianie faktyczny LIVE PID 3540, EXE `C:\Users\pboru\OpenFill\app\OpenFill.exe`, Core SHA256 jak wyżej, EXE SHA256 `6B07E79A10158FD8903000EC8AE9930D14555179D90E51CBF7109935A5163E52`.
- DEV pozostał PID 9040, ścieżka `staging/of_imp_20261031/OpenFill.exe --instance dev`, Core SHA taki sam jak LIVE.
- Harmonogram `OpenFill-open`: Running; `OpenFill-OFIMP-dev-20261031`: Running. Skrypt potwierdził prawidłową odpowiedź MCP LIVE, bez oczekiwania na puste `working`.
- Zachowany rollback LIVE w kopii sprzed wydania, rollback DEV z OF-000028 w `staging/of_000028_backup_pre_dev_activation`.

## Co weszło do LIVE
Wydanie zawiera wszystkie zmiany SOURCE aktualnego kandydata DEV do OF-000028, w szczególności A04/A06: aktualny sharedFolder MCP, zrzuty podkatalogowe i zewnętrzne po `capture_id` z SHA256, oraz wcześniej dodane komponenty pamięci, procedur, batch WhatsApp/LinkedIn. Efekty wysyłki przeznaczone wyłącznie do DEV pozostają nadal pod flagą `_candidateStagingOnly` w ToolRegistry, więc sama promocja binariów nie stanowi wykonania rzeczywistych wysyłek.

## Niedokończone
- Brak acceptance realnego WebView2 na zalogowanej sesji WhatsApp/LinkedIn dla zatwierdzonych treści i odbiorców. Smoke SQLite/procedur nie dowodzi dostarczenia wiadomości.
- Brak potwierdzenia pełnego end-to-end z WebView2 dla capture do zewnętrznego folderu, choć plikowy SOURCE i MCP DEV przeszły testy.
- A09: `AutoViewReadPlanner` korzysta z `_recipes.Best(host,path)` bez live signature; `CaptureRecipeStore.RecordScoped` zapisuje 64-znakowy capture signature, a `SemanticViewInspector` zwraca 20-znakowy structural signature. Zidentyfikowano różny kontrakt sygnatur, bez zmiany SOURCE w OF-000029. Nie oznaczać A09 exact-view auto recipe jako gotowe, zanim zostanie ujednolicony kontrakt i wykonana weryfikacja.
- E03: realne pomiary kompletnych zadań i akceptacja efektywności pozostają częściowe; Airbnb D03 nadal anulowane.

## Handoff
Zapis rotacyjny na SATANAMD: `D:\OpenBrowserLiteHandoffs\of\HANDOFF_CURRENT.md`. Następny lead budzi się na 0 niezależnie od handoffu. Nie zatrzymano harmonogramu.
