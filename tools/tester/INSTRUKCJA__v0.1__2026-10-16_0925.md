<!-- OpenFill - instructions for the tester package (copied as INSTRUKCJA.txt by tools\pack-tester). Metadata: wersja 0.1, data 2026-10-16 09:25 -->
OpenFill {{VERSION}} – wersja testowa
=====================================

OpenFill to program na Windows: okno z przeglądarką i małym modelem AI, który sam klika
po stronach (wypełnia formularze, szuka, zapisuje się na zajęcia…). Zadania możesz mu
dawać w jego oknie albo z Claude'a / ChatGPT – jako narzędzie MCP.

Ta wersja NIE wymaga niczego od Ciebie: klucz API jest już w środku, MCP jest włączone.
Testowy klucz ma mały limit kosztów i działa {{VALID_UNTIL}}. Gdy się skończy,
zadania zaczną kończyć się błędem – to normalne.


1. Uruchomienie (2 minuty)
--------------------------

Wymagania: Windows 10 lub 11 (64-bit), internet. Nic nie trzeba instalować.

1. Rozpakuj cały ZIP (prawy przycisk → „Wyodrębnij wszystkie…”), np. na Pulpit.
   Nie uruchamiaj niczego z wnętrza ZIP-a bez rozpakowania.
2. W rozpakowanym folderze kliknij dwa razy „URUCHOM OpenFill.cmd”.
   - Jeśli Windows pokaże „System Windows ochronił Twój komputer”:
     kliknij „Więcej informacji” → „Uruchom mimo to” (program nie jest podpisany).
3. Otworzy się okno OpenFill, a w czarnym okienku po chwili pojawi się
   ADRES MCP w stylu  https://coś-tam.trycloudflare.com/mcp/długi-sekret
   Adres jest już skopiowany do schowka.

Ważne:
- Okno OpenFill musi być otwarte, a komputer włączony, kiedy Claude/ChatGPT z niego korzysta.
- Adres zmienia się po każdym ponownym uruchomieniu OpenFill. Wtedy uruchom
  „URUCHOM OpenFill.cmd” jeszcze raz i podmień adres w ustawieniach konektora (punkt 2).
- Adres to jak hasło: kto go zna, steruje przeglądarką w Twoim OpenFill. Nie wklejaj go nigdzie publicznie.
- Okno nie otwiera się w ogóle? Zainstaluj „WebView2 Runtime”:
  https://go.microsoft.com/fwlink/p/?LinkId=2124703  i spróbuj ponownie.

Możesz od razu spróbować bez żadnego czatu: w oknie OpenFill wpisz zadanie pod
„What should I do?” i kliknij Run.


2. Dodanie OpenFill do Claude'a albo ChatGPT
--------------------------------------------

(Nazwy menu mogą się trochę różnić w zależności od wersji i języka aplikacji.)

A) Claude (claude.ai w przeglądarce albo aplikacja Claude na komputer)
   1. Ustawienia (Settings) → Konektory (Connectors).
   2. „Dodaj niestandardowy konektor” (Add custom connector).
   3. Nazwa: OpenFill
      Adres URL zdalnego serwera MCP: wklej ADRES MCP ze schowka.
      Ustawienia OAuth zostaw puste. Kliknij „Dodaj”.
   4. W nowej rozmowie kliknij ikonę narzędzi / „+” pod polem wiadomości
      i upewnij się, że OpenFill jest włączony.
   Po zmianie adresu: Ustawienia → Konektory → OpenFill → usuń i dodaj ponownie z nowym adresem.

   Claude Code (terminal), jeśli używasz:
      claude mcp add --transport http openfill "ADRES_MCP"

B) ChatGPT (chatgpt.com, wymaga planu Plus/Pro/Business)
   1. Ustawienia (Settings) → Aplikacje i konektory (Apps & Connectors)
      → Ustawienia zaawansowane (Advanced settings) → włącz „Tryb dewelopera” (Developer mode).
   2. Wróć do „Aplikacje i konektory” → „Utwórz” (Create).
   3. Nazwa: OpenFill
      Adres serwera MCP (MCP Server URL): wklej ADRES MCP.
      Uwierzytelnianie (Authentication): Brak (No authentication).
      Zaznacz „Ufam tej aplikacji” (I trust this application) → „Utwórz”.
   4. W rozmowie: „+” pod polem wiadomości → Więcej (More) → Tryb dewelopera
      → wybierz OpenFill.
   Po zmianie adresu: w ustawieniach aplikacji OpenFill podmień adres (albo usuń i utwórz ją ponownie).


3. Przykładowe polecenia do czatu
---------------------------------

- „Użyj OpenFill: wejdź na allegro.pl i znajdź 3 najtańsze czajniki elektryczne
   z oceną co najmniej 4,5. Nic nie kupuj. Podaj nazwy, ceny i linki.”
- „Przez OpenFill sprawdź na stronie PKP Intercity połączenia Warszawa → Kraków
   w najbliższy piątek po 16:00. Tylko odczyt, nic nie rezerwuj.”
- „Za pomocą OpenFill wypełnij formularz kontaktowy na <adres strony> danymi: …
   Zanim wyślesz, zapytaj mnie.”

Jak to działa: czat przekazuje zadanie do OpenFill, a mały model w OpenFill sam
klika w oknie na Twoim komputerze. Postęp widać na żywo w panelu po prawej.
Jeśli strona wymaga logowania, zaloguj się raz w przeglądarce w oknie OpenFill
(to osobny profil, niezależny od Twojego Chrome/Edge) – OpenFill to zapamięta.
Przed płatnością OpenFill zapyta o zgodę w swoim oknie.


4. Gdy coś nie działa
---------------------

- Czat mówi, że nie może połączyć się z OpenFill → sprawdź, czy okno OpenFill jest otwarte
  i czy adres w konektorze jest aktualny (uruchom „URUCHOM OpenFill.cmd”, porównaj).
- Zadanie kończy się błędem o kluczu / limicie (401, 429, „quota”) → skończył się testowy
  klucz albo jego limit. Daj znać osobie, która przysłała Ci program.
- Wszystkie dane OpenFill są w folderze  %USERPROFILE%\OpenFill  (profil przeglądarki, logi).
  Żeby wszystko usunąć: zamknij OpenFill, usuń ten folder i rozpakowany folder z programem.
- Przy zgłaszaniu problemu pomaga zrzut ekranu panelu oraz plik z folderu
  %USERPROFILE%\OpenFill\data\stable\logs  (logi nie zawierają haseł ani kluczy).
