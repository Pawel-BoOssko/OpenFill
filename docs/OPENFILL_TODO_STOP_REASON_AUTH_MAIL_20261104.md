# OpenFill TODO 2026-11-04

1. Rozdzielić przyczyny zakończenia: stop przez użytkownika, anulowanie przez zlecającego, samodzielny finał Luny, timeout, błąd i przerwanie aktualizacją. Zapisać powód w trwałym rekordzie, logach i odpowiedzi MCP. Od niego zależy polityka kontynuacji, bez automatycznego ponawiania niepewnych skutków.

2. API/MCP-first stosować do zwykłych operacji pocztowych wykonywanych przez model zlecający, jeśli ma odpowiednie narzędzie. Wyjątek: w ramach procesu logowania Luna wewnątrz OpenFill może samodzielnie odczytywać pocztę przeglądarkowo w celu uzyskania kodu, linku lub danych potrzebnych do uwierzytelnienia. Nie delegować takiego odczytu do modelu zlecającego.

3. Wdrożyć jedną celowaną zmianę, skompilować Release, wydać DEV i LIVE z rollbackiem i zapisać wynik. Nie zwiększać zakresu testów.
## Status implementacji, 2026-11-04
- [x] Podział przyczyn zatrzymania w AppHost i rekordzie MCP; `stop_reason` / `stop_actor` w odpowiedzi statusowej.
- [x] Zapis przyczyny w usage i logach; osobne przyczyny dla MCP cancel, GUI Stop, aktualizacji oraz utraconej sesji po restarcie.
- [x] Opis API/MCP-first z wyjątkiem dla odczytu wiadomości uwierzytelniających przez Lunę.
- [x] Build, DEV, LIVE, PID/SHA/MCP i rollback.
- [ ] Potwierdzić te pola w rzeczywistym zakończonym zadaniu każdego typu przy naturalnym wystąpieniu. Nie tworzyć sztucznych wielu przerwań.
- [ ] Rozszerzyć dalszy routing i analizę automatycznego resume z wykorzystaniem stop_reason; nie zmieniać obecnych zasad bez sprawdzenia wyniku.
