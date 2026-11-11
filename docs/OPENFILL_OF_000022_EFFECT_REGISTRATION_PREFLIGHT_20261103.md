# OF-000022: rzeczywista rejestracja efektów i poprawka preflight LinkedIn

Wybudzenie OF-000022-A01-021005, początek 2026-11-03 02:10:05 +01:00 (Warszawa). Host PBOX1. Zarejestrowano czat MultiHost o kluczu 0b0287ab-16ca-423e-919f-68a1e83007be.

## Zmiany SOURCE

1. `tools/of_imp_smoke/ToolRegistryEffectRegistrationSmoke.cs`: nowy test rzeczywistego `ToolRegistry.Build()` dla dwóch katalogów instancji: `dev` i `stable`, bez WebView2, modelu i wysyłki. Test wymaga obecności tylko w dev `linkedin_send_current_effect` oraz `whatsapp_send_effect`, poprawnych wymaganych parametrów, odrzucenia `authorized=false` przez oba handlery i odrzucenia nieprawidłowego scope LinkedIn przy `authorized=true`. 9 asercji.
2. `tools/of_imp_smoke/Program.cs`: flaga `of022-effect-registry-only` do uruchomienia powyższego testu.
3. `src/OpenFill.Core/Agent/LinkedInLocalPlanEffectBridge__v0.1__2026-11-03.cs`: wyodrębniono osobny, read-only `LinkedInThreadPreflight.ObserveAndCheckAsync` PRZED atomową zmianą `planned -> attempted`. Nieprawidłowa, brakująca albo niezalogowana aktualna karta daje `blocked_preflight` i zachowuje `planned`, umożliwiając poprawienie widoku bez wymuszania nowego effect_id. Kontrola at-most-once nadal atomowa w journal; adapter LinkedIn ponownie sprawdza thread/account po `attempted` i przed kliknięciem.
4. Nie zmieniano zwykłego `AgentRunner.RunAsync`, produkcyjnego tool routing ani zewnętrznego MCP. Realne send nieuruchomione.

## QA i publikacja

- `dotnet run --project tools/of_imp_smoke/FileFilterSmoke.csproj -c Debug --no-restore -- of022-effect-registry-only`: `OF022_INTERNAL_EFFECT_REGISTRATION_9_CHECKS_OK`, exit 0. Pierwszy przebieg przed poprawieniem napisu licznika również miał exit 0, błędny był wyłącznie komunikat 7 zamiast 9.
- `dotnet publish src/OpenFill.App/OpenFill.App__v0.3__2026-10-12_2034.csproj -c Release --no-restore -v:q -o staging/of_000022`: exit 0, bez błędów, dotychczasowe ostrzeżenia.
- SHA SOURCE LinkedInLocalPlanEffectBridge.cs: A61BB38295DF01FD6919C06995D2C30EB9AD39D7354D8D62E647A4DA3A6D01E1.
- SHA SOURCE ToolRegistryEffectRegistrationSmoke.cs: 3B6F1911DCD9C727C576989F05DC343CA331E338E166A5C5DF4807DEDC1B6990.
- SHA opublikowanego i aktywnego DEV `OpenFill.Core.dll`: 0F4A29AC6082FD1AEF4C4A43013AEAEA7F2853FC67F301D5AA58089CE93899C6.

## Aktywacja DEV

- Przed zmianą `openfill_info` DEV: `{"working":[]}`.
- Pełen backup poprzedniego DEV: `staging/of_000022_backup_pre_dev_activation`, Core SHA 8109F3193F781B423994ECD75006DF2E89108EA1287FC520BAE5F7715D9E4C40.
- Zatrzymano tylko zadanie `OpenFill-OFIMP-dev-20261031`, zaobserwowano wyłącznie LIVE PID 252.
- Skopiowano wydanie `staging/of_000022` do `staging/of_imp_20261031`, porównano SHA Core.dll, uruchomiono ponownie DEV.
- DEV nowy proces PID 22212 z `--instance dev`; `openfill_info` po restarcie zwraca `{"working":[]}`.
- LIVE nadal PID 252, Core.dll SHA 694E48BAAA9D59DDF9B47553968D007C0353FFDA6FC9E402C3B5FB1A0001B89B; nie aktualizowano.

## Ograniczenia i dalszy etap

- Potwierdzono działanie realnego `ToolRegistry.Build()` w izolowanym procesie testowym, nie rzeczywisty send z zalogowanego konta.
- Brak wskazanego adresata, autoryzowanej treści i zalogowanego zadania do przeprowadzenia skutkowego testu LinkedIn/WhatsApp.
- Następny etap: pojedynczy test właściwego handlera z jedną zatwierdzoną wiadomością w zalogowanym WebView2; sprawdzić operationId, evidenceRef i brak replay. Po nim decyzja o LIVE. Ustalenie odbiorcy i tekstu nie może zostać zastąpione fikcyjnymi danymi.
- Rollback DEV: zatrzymać zadanie, przywrócić backup `staging/of_000022_backup_pre_dev_activation` do `staging/of_imp_20261031`, uruchomić zadanie, sprawdzić MCP.
