# OpenFill: checkpoint v1.0 (2026-10-18, 17:15 CEST)

## Stan produkcji
- OpenFill 1.55 działa na PBOX1. Podczas odczytu 16:46 MCP zgłaszał trzy równoległe zadania. Nie wywoływano maintenance, nie kończono procesów i nie zmieniano `C:\Users\pboru\OpenFill\data\stable`.
- Zmiany tej sesji znajdują się **tylko w źródłach/skryptach i dokumentacji**. Żadnej nowej instalacji.

## OF-212/176, krytyczny błąd instalatora: zmiana w źródłach

- Stary `tools/install__v0.12__2026-10-12_2126.ps1` wykonywał `CloseMainWindow()`, po czym `Start-Sleep -Seconds 3` i `Stop-Process -Force`, jeśli OpenFill jeszcze działał.
- GUI `OnFormClosing` uruchamia `PrepareShutdown`, ma opóźnienie ok. 1,5 s na long-poll MCP i oczekiwanie do 4 s na `_host.DisposeAsync()`. Wymuszone zabicie po 3 s mogło przerwać kontrolowane wyłączenie. Jest to **potwierdzona luka projektowa**, niepełny dowód bezpośredniej przyczyny historycznego `stopped_by_user`.
- Przemianowano skrypt na `tools/install__v0.13__2026-10-18_1650.ps1` (starej nazwy nie pozostawiono), metadata 0.13. Nowy kod żąda zamknięcia, czeka do 25 s na zniknięcie PID, po czym w razie braku zamknięcia **przerywa instalację bez force kill i bez rozpoczęcia publikowania binarek**. `INSTALL.cmd` wybiera najnowsze `tools/install*.ps1` leksykograficznie.
- Nowy niezależny test `tools/test_install_controlled_shutdown__v0.1__2026-10-18_1655.ps1`: 10 PASS / 0 FAIL, w tym analiza składni PowerShell, brak Stop-Process -Force w fazie zamykania, kontrola timeout/abort/kolejności build. **Nie wykonano realnego restartu produkcji.**
- `dotnet build OpenFill.sln -c Release --no-restore`: 0 błędów, 1 znane ostrzeżenie MSB3277, exit 0 (PID 19516). Pełna regresja `dotnet run --project tests/OpenFill.Tests/OpenFill.Tests__v0.3__2026-10-14_2259.csproj -c Release --no-restore`: **813 PASS / 0 FAIL**, exit 0 (PID 17940).

## OF-223: pełne odtwarzanie rzeczywistego backupu na SATANAMD, WYKONANE

- SATANAMD lokalnie miał archiwum `OpenFill-state-portable__v0.2__2026-10-16_130304.tar.age` (1 090 191 400 B), manifest `OpenFill-manifest-portable__v0.2__2026-10-16_130304.json.age` (2 640 047 B), age.exe, tar.exe i wyłącznie lokalną prywatną tożsamość odzyskiwania.
- SHA-256 zaszyfrowanego archiwum: `2011F85B28B0B836045E404F28DE6A6D14F1523284215E37505E2754BD306F8D` (zgodny z poprzednim checkpointem/PBOX1/montowanym Drive).
- SHA-256 manifestu: `78D6170A5A48886CC64405D53232688D466676724060FF1333F372D01759DBD6`, również zgodny.
- Wykorzystano sprawdzony kod istniejącego na SATANAMD strumieniowego restore `restore_openfill_portable_age__v0.1__2026-10-16_1221.ps1`. Prywatnej tożsamości nie kopiowano ani nie wyświetlano; wszystkie operacje decrypt/extract wykonano na SATANAMD. 
- Katalog docelowy: `C:\Users\pboru\Documents\OpenFill-Restore-Checks\OpenFill_20261018_1655`, nowy, pusty, z wyłączonym dziedziczeniem ACL i tylko jedną regułą Allow dla właściciela. Nie dotykano prawdziwego profilu.
- Wynik po deszyfracji, rozpakowaniu i porównaniu każdego wpisu manifestu: **RESTORE_VERIFIED, 8 864 pliki, SHA256Mismatch=0, exit 0**. Skrypt zweryfikował również liczbę odtworzonych plików, brak plików spoza manifestu i brak wyjścia ścieżek poza katalog.
- To weryfikuje integralność kopii i możliwość odzyskania danych. Nie jest jeszcze testem uruchomienia GUI z odtworzonego profilu; OF-176/GUI pozostaje otwarty.

## A9/A10, testy niezależne

- Technology Map: `test_technology_manifest_consistency` 9 PASS/0 FAIL (manifest 41 tematów, 7 kategorii i aktualny hash atlasu); `test_technology_panel_source`: PASS wersja/PID/baner/MainForm. Brak realnego testu rozdzielczości GUI.
- Telemetria produkcyjna, tylko odczyt, 120 próbek: 59,7 min, **0 błędnych, 0 luk, 0 zmian PID/startedUtc**. Idle 67 próbek, CPU całej maszyny średnio 0,082%, suma WS 825,48 MiB, Private Bytes 559,03 MiB; active 53, CPU 6,902%, suma WS 1988,24 MiB, Private Bytes 1440,78 MiB. Miary pamięci obejmują sumy procesów i współdzielone strony, nie dowodzą wycieku.
- Następne priorytety: OF-239 (zastępczy responder MCP, bez ponawiania historycznie odrzuconego patcha), OF-230/231 rzeczywiste wielogodzinne GUI, OF-176 próba rollbacku i start z profilu z odtworzenia, A8 Radar. OF-240 widoczna konsola Harmonogramu nadal wymaga poprawki administracyjnej, ale synchronizacja dokumentów działa.

## Logi i zasady

- Niewykonane komendy: `docs/OpenFill__log_niewykonanych_komend__v0.1__2026-10-18.md`, dopisano OF-UNEXEC-031/032 (normalne błędy testu/ścieżki, nie odmowy bezpieczeństwa). 
- Zmiany zapisano przez Desktop Commander; LC MultiHost wykorzystano do SATANAMD systemowego testu restore.
- Produkcyjny OpenFill 1.55 jest niezmieniony; wszystkie powyższe nowe testy kodu instalatora to SOURCE/TEST, nie STAGED/DEPLOYED.

