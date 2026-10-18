# OpenFill OF-032/033/034: profile obciążenia i interpretacja pomiarów

Data: 2026-10-18. Stan: plan powtarzalnych badań + sprawdzony odczyt telemetrii; nie jest wynikiem wydania nowej wersji.

## Źródło danych

`ResourceTelemetry__v0.1__2026-10-14_2259.cs` zapisuje NDJSON `resources_YYYYMMDD.ndjson` co około 30 s, wyłącznie dla procesu OpenFill i potomnych rozpoznanych w drzewie procesów. Start PID, timestamp startu, timestamp próbki, liczba kart, procesów WebView2, liczba zadań i parametry pamięci są zapisywane w każdym rekordzie.

Nowy skrypt bez zmian stanu:
`tools/telemetry_window_quality__v0.1__2026-10-18_ChatGPT.ps1`
- Domyślnie czyta **ostatnie 120 próbek**, opcjonalnie `-File <path> -Samples N` i `-AsJson`.
- Oddziela wyniki `idle`, `active`, `waiting`; mierzy luki dłuższe niż 1,5 okresu, zmiany root PID/startedUtc, błędne linie i nieuporządkowane timestampy.
- Podaje jawnie czas pierwszej i ostatniej próbki, a nie deklaruje pełnej godziny, jeżeli dane są nieciągłe.
- Test syntetyczny: `tools/test_telemetry_window_quality__v0.1__2026-10-18_ChatGPT.ps1` (12 PASS/0 FAIL w dniu 18.10).

## Pomiary do wykonania po kontrolowanym wydaniu

| Profil | Stan aplikacji | Czas obserwacji | Kryteria porównania |
|---|---|---|---|
| Start na czysto | Brak zadań; wersja i PID zanotowane | 10-15 min | CPU, WS, Private Bytes, procesy WebView2, liczba kart |
| 1 karta | Jedna lekka strona; brak zadań | 30 min | Taki sam URL i ten sam status logowania |
| 3 karty | Trzy jawnie opisane strony | 30 min | Identyczne strony i liczba rendererów |
| 5 kart | Maksymalny limit, kontrola kolejki | 30 min | Brak szóstego renderera/karty poza limitem |
| 1 zadanie | Jeden kontrolowany, niepłatny workflow | Pełny czas wykonania | Aktywność, przerwania, potwierdzony skutek |
| 3 zadania | Trzy różne domeny, intencjonalne testy | Pełny czas | Brak kolizji domen, osobne karty i wyniki |
| Interakcja/popup | Przebieg dopuszczony przez użytkownika | Pełny czas | Własność popupu, oczekiwanie, wyjście |
| Długa bezczynność | Bez zadań, zestaw stron bez zmian | 30, 60 i 180 min | Stabilność CPU, pamięci, uchwytów, rendererów |
| Po wielu zakończonych zadaniach | Zwolnione lub zachowane strony zgodnie ze specyfikacją | 30-60 min | Brak niekontrolowanego przyrostu Private Bytes |
| Dłuższe utrzymanie sesji | Kilka godzin/dzień, rzeczywiste GUI | 1-3 h oraz do następnego dnia | Odtworzenie kontekstu i niezapisanych formularzy, brak fałszywego reuse |

Wersje 1.52 i kolejna muszą mieć **porównywalny profil obciążenia**. Wyników z 1 kartą nie zestawia się jako dowodu poprawy z wynikami dla 5 kart. Podobnie dane po restarcie wymagają osobnych odcinków.

## Słownik pól

- `cpuPctMachine`: procent łącznej mocy wszystkich logicznych procesorów; 100% oznacza wykorzystanie wszystkich logicznych CPU.
- `cpuPctCoreSum`: procent sumy rdzeni, pojedynczy maksymalnie obciążony logiczny procesor to około 100 punktów.
- `workingSetMb`: **suma Working Set procesów**, nie unikalna fizyczna pamięć; współdzielone strony DLL mogą być policzone wielokrotnie.
- `privateBytesMb`: suma prywatnie zadeklarowanej pamięci (commit), nie jest to fizyczny RAM ani pamięć jednego procesu.
- `gcManagedMb`, `gcHeapSizeMb`, `gcCommittedMb`: pamięć zarządzana .NET, bez pamięci Chromium.
- `activeTasks` i `waitingTasks`: odrębne stany. Jeśli aktywność i oczekiwanie współwystępują, klasyfikacja nowego skryptu nadaje priorytet active; interpretować także źródłowe liczniki.
- `rootPid`, `rootStartedUtc`: zmiana któregokolwiek wymaga rozdzielenia odcinków i odrzuca wniosek o ciągłym jednym uruchomieniu.

## Ostatni audyt próbki, nie wynik wdrożenia

W odczycie wykonanym 18.10.2026 narzędzie znalazło 120 próbek, około 59,84 min, 0 nieparsowalnych, 0 luk ponad 45 s, 1 zmianę PID i czasu startu. Zakwalifikowano 107 idle i 13 active, 0 waiting. Średnie idle: CPU maszyny 1,903%, suma WS 2187,81 MiB, Private Bytes 1881,78 MiB. Średnie active: CPU 10,334%, suma WS 2084,14 MiB i Private Bytes 1540,63 MiB. Nie wolno przedstawiać całego odcinka jako jednego ciągłego benchmarku procesu, bo nastąpiła zmiana PID.

## Ograniczenia odbioru

Te pomiary nie weryfikują OF-230, OF-235 ani stabilności po rzeczywistym wdrożeniu. Testy live wymagają zatwierdzonego wydania, działającego GUI oraz realnej obserwacji po wymaganym okresie. Nie zmieniano produkcyjnego procesu ani ustawień zbierania danych.
