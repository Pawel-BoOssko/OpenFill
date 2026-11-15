# Obowiazkowa zasada: ZERO migania okien CMD / PowerShell (PBOX1)

Zaden automatyczny proces nie moze wyswietlac ani na moment okna CMD, PowerShell, pwsh, Python, Node, conhost ani przejmowac fokusu.

Dotyczy autostartu, Harmonogramu Windows, watchdogow, deployow, backupow, monitoringu, synchronizacji, testow i procesow potomnych.

1. Uruchamiaj procesy calkowicie niewidocznie; samo -WindowStyle Hidden moze nie wystarczyc. Uzyj bezkonsolowego launchera (WScript.Shell.Run z windowStyle=0 lub CREATE_NO_WINDOW/STARTF_USESHOWWINDOW), takze dla procesow potomnych.
2. Nie wylaczaj uslug ani harmonogramow dla ukrycia konsoli. Zachowaj logi, diagnostyke, kody zakonczenia i restarty.
3. Testuj rzeczywisty autostart lub Harmonogram Windows oraz brak widocznych okien i przejmowania fokusu.
4. Miganie to blad blokujacy wdrozenie. Widoczna konsola wymaga wyraznej zgody uzytkownika.

Dyspozycja uzytkownika 2026-11-15: na PBOX1 nie ma prawa migac PowerShell/CMD podczas pracy automatycznej.