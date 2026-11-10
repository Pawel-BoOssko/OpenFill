# Niezależna weryfikacja przed trwałym powiązaniem sesji z URL
Autor: GPT-6. Data: 2026-11-10 20:01:57 CET.

## Nowa reguła
Nie wolno trwale kojarzyć niejawnej sesji MCP z URL rozmowy wyłącznie na podstawie podsumowania modelu sterującego przeglądarką.

1. ChatGPT drukuje w swojej rozmowie fallback_id: 20-32 znaki [A-Za-z0-9].
2. MCP odczytuje automatycznie session hash z _meta.openai/session.
3. W pierwszym żądaniu OpenFill może wykonać dokładnie jedno wyszukanie tokenu, otwiera jedną rozmowę, zwraca natywny URL.
4. Po zakończeniu działania OpenFill (success) serwer niezależnie otwiera w WebView2 podany URL i wykonuje jeden zbiorczy odczyt CDP: finalny URL, document.title, boolean main.innerText.contains(fallback_id).
5. Jeśli URL jest dokładnie zgodny, token występuje w treści i model dostarczył spójny dowód JSON, zapis powiązania session->native_chat_id jest dozwolony. W przeciwnym razie nie zapisuj powiązania.
6. Kolejne odczyty native ID są bez modelu, a odczyt nazwy ma jedną bezpośrednią nawigację i jeden zbiorczy odczyt CDP, bez wyszukiwania.
7. Zmiana nazwy, archiwizacja i przeniesienie do projektu w obecnej fazie nadal są wykonywane przez model wewnętrzny, ale wskazuje mu się dokładny URL.

## Testy automatyczne, PBOX1, repo feature/openfill-self-chat-steering-20261110
- Smoke MCP JSON-RPC i cache: 87 zaliczone, 0 błędów; symulowany host.
- Dodatkowe negatywne scenariusze: model twierdzi, że znacznik jest widoczny, ale w DOM go nie ma; model podaje poprawny znacznik, ale CDP znajduje inny URL. W obu przypadkach nie powstaje zapis session->URL.
- Pełny .NET Release build: 0 błędów, 6 wcześniejszych ostrzeżeń.
- Testy MCP origin: 215 PASS; taby: 196 PASS; CDP capture: 74 PASS.
- Łącznie 572 kontrole testowe PASS w czterech zestawach.

## Ograniczenia
- Konieczny jeszcze test z rzeczywistym ChatGPT na PBOX1. Wcześniejsza próba wyszukania tokenu w jego wyszukiwarce zwróciła „Brak wyników” wraz z błędem ładowania historii.
- URL tej konkretnej rozmowy pozostaje nieustalony.
- Produkcja C:\Users\pboru\OpenFill\app\OpenFill.exe nie została zmieniona, gałąź feature tylko w katalogu developerskim.
- Wybudzanie: zlecenie przyjęte ZL-20261110-1815-OF, w rejestrze jest jednak pusty link do rozmowy. Próg 15 minut, lecz skuteczne dotarcie nadzorcy do tego czatu nie zostało zweryfikowane.
