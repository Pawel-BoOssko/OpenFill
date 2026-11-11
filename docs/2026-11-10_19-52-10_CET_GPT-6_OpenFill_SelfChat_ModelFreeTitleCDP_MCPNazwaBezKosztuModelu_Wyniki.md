# OpenFill Self Chat: odczyt nazwy bez modelu
Data: 2026-11-10 19:52:10 CET, autor GPT-6.

## Wdrożone w SOURCE
- Po weryfikacji i utrwaleniu sesja MCP -> natywny URL, openfill_self_chat_identity odczytuje powiązanie bez Luny.
- openfill_self_chat_name dla zweryfikowanego powiązania odczytuje nazwę przez CDP bez Luny, bez wyszukiwarki i bez wysyłania pełnego snapshotu do modelu.
- AppHost używa DomainLocks i TabManager; jedna nawigacja, jeden odczyt URL + document.title.
- Nieznany tytuł, przekierowanie, złe ID, zajęta domena: jawny błąd bez zmiany innych czatów.
- Pierwsze dopasowanie pozostaje oparte o krótki fallback_id widoczny w rozmowie.

## Wyniki
- dotnet build całego solution Release: EXIT=0, 0 błędów, 6 ostrzeżeń z kodu bazowego.
- Smoke nowego MCP HTTP i cache: 81/81 PASS (FakeHost, nie rzeczywisty ChatGPT UI).
- Dotychczasowe testy MCP origin: 215/215 PASS.
- Regresje kart TabManager: 196/196 PASS.
- Regresje Capture i CDP: 74/74 PASS.
- łącznie: 566 asercji PASS z oddzielnych zestawów.
- Logi datowane są w tym katalogu docs.
- Nie wykonano rzeczywistego testu zmiany nazwy ani instalacji LIVE.
- Lokalizacja kodu: C:\Users\pboru\Documents\Projects\OpenFill-SelfChatSteering-Current-20261110, gałąź feature/openfill-self-chat-steering-20261110.

## Otwarta kontrola
- Pierwsze skojarzenie sesji z URL opiera się na raporcie agenta o widocznym fallback_id. Warto zwiększyć wiarygodność: dodatkowo potwierdzić token bezpośrednio w DOM przez CDP przed zapisem.
- Przed rolloutem: staging, rollback, kontrolowany shutdown, test fizycznego konta.
