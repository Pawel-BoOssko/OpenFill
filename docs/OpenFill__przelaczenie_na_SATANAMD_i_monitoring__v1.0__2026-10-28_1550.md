# Metadata
- Nazwa: OpenFill__przelaczenie_na_SATANAMD_i_monitoring
- Wersja: 1.0
- Data: 2026-10-28 15:50 (EAT, UTC+3)
- Autor: Claude Bishop (konto "Claude Bishop"), czat OpenFill - wdrozenie i monitoring
- Dotyczy: OpenFill (PBOX1), lc-multihost (SATANAMD), monitoring obu maszyn
- Zrodlo szczegolow przelaczenia: SATANAMD, C:\Users\pboru\AppData\Local\LupusCommander\multihost-next-stage\MULTIHOST_ZMIANY__v1.0__2026-10-28_1535.md (autor: czat "napraw lc multihost")

# 1. Przelaczenie lc-multihost na SATANAMD (skrot, szczegoly w pliku zrodlowym)
- Publiczne wejscie lc-multihost: https://lupus-plugins.taila6d4ec.ts.net/lc-multihost/ (Funnel na SATANAMD, 127.0.0.1:8893). Reszta adresu konektora bez zmian.
- Stare wejscie na PBOX1 (openfill-pbox1, /lc-multihost i :8443) zostawione jako zapasowe.
- SATANAMD: straznik sieci (zadanie LupusNetWatchdog, co 2 min), Windows Update tylko w 3. srode miesiaca o 04:00, karta Tuya do przycisku power (tylko status przetestowany).
- Zasada Pawla: restart SATANAMD dopuszczalny, ale staramy sie nie restartowac i dbamy o skrypty w autostarcie.

# 2. Co zostaje na PBOX1
- OpenFill (MCP 127.0.0.1:48731, Funnel https://openfill-pbox1.taila6d4ec.ts.net/), wersja 1.57 od 2026-10-27 15:17 EAT. Kopia poprzedniej wersji: C:\Users\pboru\OpenFill\app-1.56.
- Dane OpenFill: C:\Users\pboru\OpenFill\data\stable (nie ruszac).

# 3. Monitoring co 10 minut (OpenFillMonitor, od 2026-10-27)
- Skrypt i logi (na PBOX1 i SATANAMD): %USERPROFILE%\OpenFill\monitor\ (skrypt OpenFill_monitor__v0.2__2026-10-27_1535.ps1, status.json, logs\mon_RRRRMMDD.ndjson, logs\events.ndjson).
- Zadanie Harmonogramu: OpenFillMonitor (co 10 min, ukryte okno, dziala na baterii). Tylko mierzy i zapisuje, niczego nie naprawia ani nie wysyla.
- Mierzy: internet (brama, 1.1.1.1, DNS, HTTP, OpenAI, zegar), Wi-Fi, zasilanie i uspienie, Tailscale i Funnel, OpenFill (proces, zawieszenie, port, MCP, zadania, awarie panelu, 429 modelu), RAM, dysk, oczekujacy restart Windows.
- Alerty: brak (swiadomie, na razie nic nie dodajemy).

# 4. Ustalenia z pomiarow 27-28.10
- 2026-10-27 ok. 18:10-20:14 EAT: OpenFill zniknal bez wpisu w logu i bez zdarzenia awarii Windows (komputer dzialal, na zasilaczu); wrocil 20:14 EAT. Autostart tego nie podnosi, bo uruchamia sie tylko przy logowaniu i po awarii z kodem bledu. Przyczyna zamkniecia nieustalona.
- Wi-Fi na PBOX1 slabnie i rozlacza sie okresowo (19:11-19:31Z 27.10, 10:41-11:03Z 28.10).
- Tailscale offline 10 min (27.10 15:51Z).
- Nagle restarty OpenFill w logu: 25.10 14:16Z, 26.10 11:53Z, 16:28Z, 19:43Z, 23:41Z, 27.10 10:30Z.

# 5. Dalsze kroki (dopiero po zgodzie)
- Skrypt automatycznej naprawy / straznik OpenFill (przygotowac, nie wlaczac, dopoki Pawel nie zdecyduje).
- Gdzie docelowo dziala OpenFill (PBOX1 dev, SATANAMD produkcja) - decyzja pozniej.
