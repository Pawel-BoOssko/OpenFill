# Diagnoza: Claude i rozpoznawanie konta LinkedIn
Data: 2026-10-28 (Warszawa), host zweryfikowany: PBOX1.
Zakres: wyłącznie ustalenie przyczyny, bez projektowania ani wdrażania zmian w obsłudze kont.

## Dowody z rekordów zadań OpenFill
- OF-20261028-1802-4ad4, Claude Bishop / Skakanka, 18:02 EAT: zlecenie prowadzenia strony firmowej Skakanki wyraźnie wskazywało osobiste konto Piotra jako docelowe. Wynik: failed/timeout. Nie jest to samoistny odczyt haseł.
- OF-20261028-1835-c61e, Claude Papa / Speaker Finder, 18:35 EAT: prompt nakazał Lunie sprawdzić aktywną tożsamość w menu „Me”, a w przypadku innego konta nie wysyłać zaproszeń i pytać zleceniodawcę. Odpowiedź Luny: konto Piotra, 0 zaproszeń, brak przełączenia. To bezpośrednie źródło wiedzy Claude'a o aktywnym koncie.
- OF-20261028-1839-d652, Claude Papa, 18:39 EAT: zadanie przełączenia konta zostało anulowane.
- OF-20261028-1843-af3b, Claude Papa, 18:43 EAT: prompt po wcześniejszej informacji narzucił Lunie szczegółową ścieżkę logowania: najpierw bezpośredni logout URL, potem login URL. Status rekordu: done, w podsumowaniu Luna zgłasza prawidłowe konto docelowe i wykonane zaproszenia.
- OF-20261028-1909-68bc, Claude Papa, 19:09 EAT: drugie zlecenie wymagało od Luny ponownej weryfikacji konta, wynik done.
- W dokumentowanej implementacji MCP (McpServer v0.24, instrukcje initialize, opis openfill_start_task) zlecający model jest proszony o podanie dokładnego konta, a OpenFill ma sprawdzać bieżącą sesję i dokonywać wyboru. Własny warunek Claude'a „nie przełączaj i spytaj” zawęził tę autonomię.
- Narzędzie MCP openfill_info nie pobiera nazwy zalogowanego profilu LinkedIn jako osobnego pola. Wiadomość o tym, kto jest zalogowany, pochodzi z danych przeglądarki odczytanych przez Lunę i zwróconych w wyniku zleconego zadania.
## Wniosek przyczynowy
Claude nie musiał logować się ani niezależnie badać sesji LinkedIn, aby znać tożsamość konta: informację przekazała Luna w odpowiedzi na wcześniejsze polecenie Claude'a. Nadmiarowe sterowanie sesją rozpoczęło się od promptu Claude'a, który wymusił osobną weryfikację, zatrzymanie i późniejszy instruktaż kolejnych kliknięć. Nie ustalono, skąd sam Claude wziął wszystkie szczegóły procesu logowania poza własnymi promptami i zwrotami z OpenFill; zapis MCP wystarcza jednak do ustalenia łańcucha informacji o aktywnym koncie. Nie stwierdzono dowodu, że Claude odczytywał zapisane hasła.
## Stan produkcji
28.10 ok. 19:38 EAT: OpenFill LIVE 1.57, 0 aktywnych zadań, brak maintenance. Wszystkie obserwacje wykonano odczytowo, bez interakcji z LinkedIn.
