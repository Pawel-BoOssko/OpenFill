# Self Chat Steering: mapa sesji MCP do natywnego URL
Autor GPT-6, Europe/Warsaw, 2026-11-10 19:25:36.

## Mechanizm
1. MCP odczytuje metadane openai/session, wylicza hash 16 hex, unikalny w ramach sesji klienta.
2. OpenFill używa natywnego URL wprost, jeśli wcześniej związał go z tą sesją przez widoczny 20-32 znakowy fallback_id. Bez związania szuka dokładnie raz po tokenie.
3. Po ukończeniu sukcesem agent zwraca OPENFILL_SELF_CHAT_RESULT: {status, platform, native_chat_id, verified_url, fallback_id, fallback_visible, same_conversation, account_verified}.
4. Serwer waliduje format URL, zgodność sesji, platformy, tokenu, konta i wynik sukces. Negatywny lub niekompletny wynik nie tworzy powiązania.
5. Mapa jest zapisywana atomowo obok bazy operacyjnej, bez surowego tokenu session ani fallback.
6. Przy następnym wywołaniu MCP z tą samą sesją serwer pobiera natywne ID z mapy i przechodzi od razu pod URL. Brak wyszukiwania, brak ponownego fallback.
7. Jeśli mapa jest niedostępna, nie można zgadywać rozmowy, wymagany awaryjny token w jawnej wiadomości.
8. Odczyt self ID z samej metadanej MCP nie wywołuje modelu Luny.

## Wdrożenie i ograniczenia
- Źródła: OpenFill.Core/Mcp/SelfChatSteering v0.2, SelfChatBindings v0.1, McpServer v0.24.
- Testy smoke: 66/66 PASS. Sprawdzono odrzucanie błędnych tokenów, innych kont, obcych URL, innych sesji, braku wyniku, częściowego wyniku, negatywnej weryfikacji. Testują także ograniczenia długości promptów.
- Kompilacja całego aktualnego rozwiązania Release: 0 błędów, 6 ostrzeżeń (ślad w docs/2026-11-10_19-22-06_CET_GPT-6_...log).
- Live ChatGPT: wyszukanie tokenu OFSCWDWQHHA9LDYSLJ95QIRME87Q nie powiodło się: 0 wyników, a OpenFill zanotował błąd wczytania historii. Brak potwierdzenia mapowania konkretnego czatu.
- Uwaga: model wewnętrzny przy czynnościach UI może użyć więcej niż jednego wywołania. Instrukcje są minimalne, ale bez deterministycznego sterownika CDP limit wezwań nie jest gwarantowany.
- Produkcja nadal bez zmian.
- Gałąź lokalna Git: feature/openfill-self-chat-steering-20261110, poprzedni commit 3492313. Bieżące zmiany do nowego commitu.
