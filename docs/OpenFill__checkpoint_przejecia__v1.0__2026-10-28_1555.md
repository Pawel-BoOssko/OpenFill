# Metadata
- Nazwa: OpenFill__checkpoint_przejecia
- Wersja: 1.0
- Data: 2026-10-28, ok. 15:55 EAT (UTC+3)
- Wykonawca: ChatGPT, przejęcie na podstawie polecenia użytkownika
- Host źródła i LIVE: PBOX1
- Status: AUDYT W TOKU, bez restartów i bez zmian LIVE

## Potwierdzone
1. RDC: PBOX1 i SATANAMD są online. Hosty zweryfikowane przez $env:COMPUTERNAME.
2. SOURCE OpenFill: C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill.
3. LIVE OpenFill: C:\Users\pboru\OpenFill\app\OpenFill.exe, PBOX1, PID 9016; openfill_info: 1.57, build 2026-10-27 15:16, status=ok, maintenance=false, active_tasks=0; 5/5 kart finished.
4. Directory.Build.props: OpenFillVersion=1.57. Wcześniejszy plan v2.0 z 2026-10-23 zawiera już nieaktualne wskazanie LIVE 1.56 / staging 1.57.
5. SATANAMD: folder C:\Users\pboru\OpenFill zawiera monitoring, nie instalację OpenFill LIVE.
6. Na obu hostach istnieje monitor OpenFillMonitor co 10 min; raporty status.json ok=true 2026-10-28 ok. 15:42-15:45 EAT; monitor nie naprawia automatycznie.
7. Dokument migracji: PBOX1 docs\OpenFill__przelaczenie_na_SATANAMD_i_monitoring__v1.0__2026-10-28_1550.md; źródło SATANAMD: C:\Users\pboru\AppData\Local\LupusCommander\multihost-next-stage\MULTIHOST_ZMIANY__v1.0__2026-10-28_1535.md.
8. Publiczny ingress LC MultiHost przeniesiony na SATANAMD (lupus-plugins.taila6d4ec.ts.net, gateway 8893), stary PBOX1 pozostawiony jako zapasowy. Starszy, odrębny connector Lupus Commander nadal uruchamia polecenia na PBOX1. Nie utożsamiać ich.
9. Historia monitoringu: PBOX1 ma okresowe zaniki Wi-Fi; OpenFill 2026-10-27 zniknął bez potwierdzonej przyczyny, po czym wrócił. Nie twierdzić, że problem naprawiono.
10. SATANAMD: watchdog sieci i zmieniona polityka Windows Update według dokumentu migracji; karta Tuya testowana tylko komendą status. Restart SATANAMD możliwy awaryjnie, unikać i zachować autostart.

## Nowe wymaganie: filtr wejściowych promptów LC MultiHost
Użytkownik proponuje lekki model decyzyjny JEV (nazwa do zachowania w specyfikacji), analizujący ograniczenia dotyczące bezpieczeństwa, haseł, kodów, tożsamości i czatów. Odpowiedź do czatu-zleceniodawcy przy rzeczywistej sprzeczności: wskazanie konkretnych fragmentów oraz prośba o poprawiony prompt. Filtr nie może automatycznie usuwać sensownych ograniczeń użytkownika, ujawniać sekretów ani nadpisywać nadrzędnych zasad bezpieczeństwa. Wymagana klasyfikacja: rzeczywisty konflikt / zwykły zakres zadania / potencjalny prompt-injection / niejasne. Bez wdrażania nieprzetestowanej bramki w produkcyjny gateway.

## Następne działania
- Potwierdzić SHA-256, produkt i testy OpenFill LIVE / SOURCE / STAGED; bez zmiany data\stable.
- Zweryfikować harmonogram autostartu OpenFill i monitoringu oraz bieżące logi i przerwy.
- Opracować weryfikowalne wymagania JEV, testy pozytywne i negatywne, lokalizację wdrożenia w LC MultiHost.
- Zaktualizować plan rozwoju i statusy LIVE; checkpoint po każdym zakończonym etapie.
- Nie uruchamiać ręcznego git commit/push, ChatGPT Tasks ani restartu usług przy niepewnej sesji.

## Uzupełnienie 28.10, analiza MCP openfill_info (bez zmian implementacji)
- Potwierdzono: problem dotyczy narzędzia MCP `openfill_info`, nie publicznego API ani zadań przeglądarkowych OpenFill.
- Pomiar faktycznej odpowiedzi: 17 543 znaki, szacunkowo 4 386 tokenów (nie mierzone tokenizerem); 8 ostatnich zadań, 60 pozycji kosztów domen, 5 kart, 1 aktywne zadanie. Sekcje dominujące: `openfill` 8528 znaków, `recent_tasks` 4576, `active_tasks` 1683, `active_task` 1681 (duplikacja).
- Kod: `src/OpenFill.Core/Mcp/McpServer__v0.23__2026-10-17_1130.cs` definiuje `openfill_info` bez parametrów (okolice linii 507) i wywołuje `InfoResult()` (okolice linii 587); `src/OpenFill.Core/Hosting/AppHost__v0.22__2026-10-14_2259.cs` generuje diagnostykę m.in. per-site koszty i karty. Pozostaje zbadać klientów i testy kontraktowe przed zmianą.
- Propozycja, nie wdrożenie: krótka odpowiedź MCP domyślnie i pełna diagnostyka na żądanie, z zachowaniem ważnych błędów i aktywnych zadań. Wpisano jako punkty 15-19 w planie operacyjnym v1.0.
- STATUS: DODANE DO BACKLOGU. SOURCE/LIVE/TEST bez zmian. Brak restartów i modyfikacji MCP.
