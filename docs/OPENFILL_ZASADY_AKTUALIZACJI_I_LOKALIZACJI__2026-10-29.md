# OpenFill: obowiązujące zasady aktualizacji i lokalizacji LIVE
Data decyzji: 2026-10-29
Status: decyzja właściciela, nadrzędna wobec starszych opisów OF-203–211, OF-224, OF-239 i historii wdrożeń.

## 1. Bez serwera zastępczego MCP
- OF-239 (zastępczy responder / watchdog MCP na okres aktualizacji) jest **ANULOWANE, USUNIĘTE Z ZADAŃ DO WYKONANIA**. Nie planować, nie budować, nie wdrażać i nie wymagać zastępcy MCP.
- Podczas wyłączenia OpenFill połączenia do MCP mogą być niedostępne. To zamierzona przerwa serwisowa, nie powód do utrzymywania zastępczego portu ani do zmuszania czatów do wielokrotnego odpytywania.
- Dokumenty sprzed 29.10.2026 zawierające OF-239 są wyłącznie historyczne.

## 2. Zasada natychmiastowej aktualizacji
1. Gdy poprawka SOURCE jest gotowa i przetestowana, wykonaj preflight kopii rollback, poprawności instalatora i możliwości restartu w interaktywnej sesji PBOX1.
2. **Nie czekaj na 0 aktywnych zadań.** Uruchom kontrolowany tryb aktualizacji; od razu zablokuj nowe zlecenia oraz przekaż osiągalnym czatom i operatorowi komunikat:
   **„Teraz nastąpi aktualizacja. Wróć do nas za kilka minut.”**
3. **Przerwij wszystkie działania**: zlecenia running, needs_input, waiting_for_user, aktualne żądania i pracę Luny. Zapisz każdemu task_id stan `interrupted`, identyfikator zlecającego, moment zatrzymania oraz informację o niepewnych skutkach ostatniego kroku. Zachowaj idempotency_key i informacje niezbędne do bezpiecznej kontynuacji. Nie powtarzaj automatycznie wysyłek, płatności ani innych działań o skutkach ubocznych.
4. Daj krótką możliwość odebrania komunikatu klientom, których połączenia w danej chwili są aktywne; następnie zamknij MCP, WebView2 i OpenFill przez istniejący **kontrolowany mechanizm zatrzymania**, zamiast czekać na bezczynność. Nie obiecuj powiadomienia klientom rozłączonym, którym technicznie nie można dostarczyć komunikatu.
5. Opublikuj wydanie na PBOX1. Uruchom OpenFill w sesji użytkownika, sprawdź proces, MCP, panel, JEV, rollback i co najmniej jedno bezpieczne zlecenie testowe. Przy niepowodzeniu użyj istniejącej kopii rollback.
6. Po powrocie MCP stare task_id mają raportować `interrupted` z informacją o aktualizacji, a kontynuacja musi zweryfikować, co wykonano. Czat zlecający może ponownie zlecić wyłącznie brakujące czynności.
- **Brak warunku 0 aktywnych zadań** w procedurze wydania, audycie i akceptacji.
- Celem jest **publikacja ASAP**, nie utrzymanie ciągłej łączności MCP podczas wymiany plików.
- Treść komunikatu jest ustalona, bez podawania pozornej dokładnej godziny wznowienia.

## 3. Lokalizacja i tolerancja na słabe Wi-Fi
- **OpenFill LIVE pozostaje na PBOX1 w Kenii**. Właściciel ma widzieć działania aplikacji i mieć bezpośredni lokalny dostęp, w tym możliwość sygnału dźwiękowego. To świadomy wymóg architektury, nie przejściowa wada.
- SATANAMD może nadal zapewniać usługi pomocnicze, takie jak JEV i LC MultiHost. Nie przenosić samego OpenFill LIVE z PBOX1 na SATANAMD bez nowej decyzji właściciela.
- PBOX1 jest mobilnym komputerem w Kenii i może mieć niestabilny sygnał Wi-Fi. Chwilowa utrata internetu, łączności z SATANAMD/Tailscale, MCP lub modelem nie jest sama w sobie dowodem awarii procesu OpenFill. Diagnozować oddzielnie stan procesu i GUI, łączność lokalnego MCP, Wi-Fi/internet, tunel Tailscale, JEV i API. Stosować sensowne timeouty i odzyskiwanie po powrocie sieci, bez niepotrzebnych restartów aplikacji i bez utraty kart/sesji.
- Nie uruchamiać częstych dodatkowych heartbeatów ani zbędnego ruchu sieciowego. Istniejący monitor może zbierać zdarzenia i odróżniać krótkie zakłócenia od długich awarii.

## 4. Status wdrożenia tych zasad
- 2026-10-29: decyzja zapisana w kanonicznym backlogu, planie operacyjnym i checkpointcie.
- Dotychczasowy mechanizm controlled shutdown oraz `interrupted` należy poddać audytowi przeciw tej zasadzie; **sam zapis nie dowodzi**, że wszystkie aktywne czaty otrzymają już komunikat ani że instalator zatrzyma wszystkie stany poprawnie. Zweryfikować na testach i wdrożyć ewentualne różnice.
- Nie uruchamiano aktualizacji LIVE w celu zapisania tej decyzji.
