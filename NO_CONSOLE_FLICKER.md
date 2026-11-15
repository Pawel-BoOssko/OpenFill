# Obowiazkowa zasada: ZERO migania okien CMD / PowerShell (PBOX1)

Zaden automatyczny proces nie moze wyswietlac ani na moment okna CMD, PowerShell, pwsh, Python, Node, conhost ani przejmowac fokusu.

Dotyczy autostartu, Harmonogramu Windows, watchdogow, deployow, backupow, monitoringu, synchronizacji, testow i procesow potomnych.

1. Uruchamiaj procesy calkowicie niewidocznie; samo -WindowStyle Hidden moze nie wystarczyc. Uzyj bezkonsolowego launchera (WScript.Shell.Run z windowStyle=0 lub CREATE_NO_WINDOW/STARTF_USESHOWWINDOW), takze dla procesow potomnych.
2. Nie wylaczaj uslug ani harmonogramow dla ukrycia konsoli. Zachowaj logi, diagnostyke, kody zakonczenia i restarty.
3. Testuj rzeczywisty autostart lub Harmonogram Windows oraz brak widocznych okien i przejmowania fokusu.
4. Miganie to blad blokujacy wdrozenie. Widoczna konsola wymaga wyraznej zgody uzytkownika.

Dyspozycja uzytkownika 2026-11-15: na PBOX1 nie ma prawa migac PowerShell/CMD podczas pracy automatycznej.

## Obowiazkowa obsluga FaultHarbor

1. Przed rozpoczeciem pracy nad bledem i podczas przegladu projektu sprawdzaj w FaultHarbor wlasne, otwarte i powiazane z projektem zgloszenia przez `find_issues`. Sprawdzaj rowniez duplikaty przed rejestracja nowego problemu.
2. Nowe bledy, regresje, awarie i braki funkcjonalne rejestruj w FaultHarbor przez `report_issue`, z kontekstem projektu, krokami reprodukcji, skutkiem, danymi diagnostycznymi i statusem prac. Nastepnie obsluguj swoje zgloszenia: diagnozuj, naprawiaj, testuj i aktualizuj status/wynik zgodnie z udostepnionymi funkcjami.
3. Jezeli FaultHarbor nie zapewnia potrzebnej funkcji, funkcja nie dziala prawidlowo, uniemozliwia zgloszenie lub obsluge problemu, albo API jest niewystarczajace: zglos to przez dostepny mechanizm FaultHarbor (`report_issue`) jako problem samego FaultHarbor, wyraznie przeznaczony dla czatu/wykonawcy rozwijajacego FaultHarbor. Opisz brak, oczekiwane zachowanie, przyklad uzycia i wplyw na prace projektu.
4. Nie pomijaj FaultHarbor i nie uznawaj sprawy za zakonczona bez sprawdzenia stanu. Gdy sam FaultHarbor nie pozwala nawet wyslac zgloszenia, udokumentuj nieudana probe oraz przekaz problem opiekunowi FaultHarbor dostepnym kanalem; po przywroceniu funkcji uzupelnij rejestr.
5. Powyższy obowiazek dotyczy takze bledow migania okien konsoli i regresji ich napraw.
