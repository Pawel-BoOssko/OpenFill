# OpenFill Self Chat Steering, implementacja i plan odbioru
Autor: GPT-6, 2026-11-10, Europe/Warsaw.
Katalog: C:\Users\pboru\Documents\Projects\OpenFill-SelfChatSteering-Current-20261110
Commit bazowy źródeł aktualnych 2026-11-10: fbc8daa.

## Fakty o identyfikatorach
- ChatGPT przekazuje w JSON-RPC tools/call params._meta klucz openai/session.
- Format odczytanej w prawdziwym wywołaniu wartości: v1/<długi token>. Nie jest to tekstowo UUID 36-znakowe adresu chatgpt.com/c/{UUID}.
- Dotychczas nie potwierdzono odwracalnego mapowania openai/session na natywne conversation ID URL.
- Mcp-Session-Id jest oddzielną sesją transportową, nie identyfikatorem rozmowy.
- API zwraca of-chat-{SHA256 sesji 16 znaków}; to identyfikator logiczny OpenFill, nie natywna część adresu.

## Tryb działań
- MCP identity bez tokenu: 0 wywołań modelu wewnętrznego, wynik od razu.
- Jeśli istnieje poprawny natywny ID z metadanych (kandydat), OpenFill buduje URL i odwiedza go bez wyszukiwania, a zgodność rozmowy weryfikuje po tokenie.
- Brak natywnego URL: klient tworzy awaryjny fallback_id 20-32 znaki [A-Za-z0-9] i WYŚWIETLA go JAWNIE w wiadomości w tej rozmowie. Nie wystarczy przekazać identyfikator do MCP.
- Tool definition jawnie nakazuje publikację przed wywołaniem i przekazanie tego samego tokenu do MCP.
- OpenFill wykonuje co najwyżej jedno wyszukanie po dokładnym fallback_id, nigdy po długich tekstach. Po otwarciu dokładnie jednego wyniku odczytuje native URL i sprawdza obecność tokenu przed każdą zmianą.
- Narzędzia: openfill_self_chat_identity, openfill_self_chat_name, openfill_self_chat_rename, openfill_self_chat_archive, openfill_self_chat_move_project.
- Brak parametrów target_chat_id i target_url dla wywołań self.

## Ograniczenia
- Nie wdrożono jeszcze trwałego serwerowego mapowania tokenu sesji na natywny chat URL. Następny krok: zapisać zweryfikowane powiązanie po wyszukaniu raz, potem bez wyszukiwarki.
- Instrukcje dla agenta zmniejszają liczbę kroków, ale nie są fizycznym ogranicznikiem liczby wywołań Luny. Ostateczny wariant: deterministyczny CDP/WebView2 driver dla natywnych URL.
- Claude: brak potwierdzonych metadanych analogicznych do openai/session.
- Próba testowa OpenFill na chatgpt.com z jawnym tokenem OFSCWDWQHHA9LDYSLJ95QIRME87Q zwróciła „Brak wyników” oraz „Nie udało się wczytać historii”, więc brak weryfikacji native ID. To nie jest dowód, że fallback jest nieskuteczny, tylko usterka/usługa historii w tej sesji.
- Produkcja C:\Users\pboru\OpenFill\app niezmieniona, prace izolowane.

## Testy
- 52 asercje smoke: sukces, .NET 10, backend i kontrakt narzędzi.
- Build Release projektu Core na starym snapshot 0 błędów. Re-test kompletnego aktualnego solution jeszcze otwarty.
- Zachowano baseline aktualnych źródeł w lokalnym Git, bez push do GitHub.
