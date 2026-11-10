# OF-000021: aktywacja kandydata DEV z LinkedIn i WhatsApp

Id wybudzenia: OF-000021-A01-015006. Silnik: 2026-11-03 01:50:06 +01:00 (Warszawa). PBOX1.

## Wykonane
1. Przeczytano bieżący checkpoint; ustalono równoległe źródłowe ukończenie mostu WhatsApp oraz starszy nieaktywny kandydat LinkedIn.
2. Przed restartem DEV wykonano wywołanie MCP openfill_info, wynik: {"working": []}. DEV OpenFill.exe PID 7624; LIVE PID 252.
3. Utworzono kopię poprzedniego DEV: staging/of_000021_backup_pre_dev_activation. SHA256 poprzedniego Core.dll: 694E48BAAA9D59DDF9B47553968D007C0353FFDA6FC9E402C3B5FB1A0001B89B.
4. Zatrzymano jedynie zadanie Windows OpenFill-OFIMP-dev-20261031. Potwierdzono brak procesu DEV przy nadal działającym LIVE PID 252.
5. Skopiowano wydanie staging/of_000020_linkedin_effect do staging/of_imp_20261031, zachowując oddzielny backup.
6. SHA256 nowego staging/of_imp_20261031/OpenFill.Core.dll: 8109F3193F781B423994ECD75006DF2E89108EA1287FC520BAE5F7715D9E4C40; zgodny z kandydatem.
7. Uruchomiono zadanie Windows OpenFill-OFIMP-dev-20261031. Nowy DEV PID 22588, komenda --instance dev, ścieżka do zaktualizowanego katalogu.
8. MCP DEV openfill_info po restarcie zwraca {"working": []}. HTTP port 48732 nasłuchuje; Get-NetTCPConnection pokazuje PID4 (HTTP.sys), nie PID aplikacji. Związanie MCP z DEV potwierdza odpowiedź na sekretnym adresie instancji dev.
9. W skompilowanym Core.dll stwierdzono dosłownie kodowane nazwy "linkedin_send_current_effect" oraz "whatsapp_send_effect". Kod ToolRegistry rejestruje oba narzędzia wyłącznie przy _candidateStagingOnly. Nie wywoływano narzędzi skutkowych ani nie wykonywano zewnętrznych wysyłek.
10. LIVE OpenFill.exe PID 252 pozostał nienaruszony; jego Core.dll nadal SHA256 694E48BAAA9D59DDF9B47553968D007C0353FFDA6FC9E402C3B5FB1A0001B89B.

## Ograniczenia i następny krok
- Potwierdzono wdrożenie binariów do procesu DEV i zdrową odpowiedź MCP. Nie potwierdzono jeszcze wywołania nowego internal ToolRegistry handler w autoryzowanej sesji, brak z góry określonych rzeczywistych tożsamości nadawcy, odbiorcy, wątku i treści do testu.
- Realny SEND uruchamiać wyłącznie dla konkretnego, zatwierdzonego zadania. Jeden efekt z trwałym operationId i evidenceRef; przy uncertain nie powtarzać automatycznie. Test w trybie read-only preflight możliwy bez wiadomości, jeśli jest zalogowana odpowiednia sesja.
- To nie jest wdrożenie do LIVE. Kandydat w DEV ma tę samą ProductVersion 1.59 co LIVE, ale inny Core.dll.
- Rollback DEV: Stop-ScheduledTask OpenFill-OFIMP-dev-20261031; przywrócić zawartość staging/of_000021_backup_pre_dev_activation do staging/of_imp_20261031; Start-ScheduledTask, sprawdzić MCP. Backup istnieje i nie został użyty.

## Dodatkowe potwierdzenie transportu
- Po aktywacji wywołanie MCP `tools/list` na adresie DEV zwróciło 15 narzędzi i brak błędu. Lista zewnętrznego MCP nie obejmuje wewnętrznych narzędzi `ToolRegistry.Build()`, więc sama nie jest ich runtime acceptance.
- `openfill_page_snapshot` jest read-only, ale wymaga istniejącego, fizycznie otwartego task_id oraz pary requester/chat z tego zadania. Brak tych danych dla autoryzowanej sesji LinkedIn/WhatsApp w przekazanym checkpoint; dlatego nie wykonywano pozornej walidacji przez anonimowy DOM.
- Jedyną nierozstrzygniętą częścią tego etapu pozostaje realny provider send z dowodem. Kompilacja, aktywacja binariów, restart DEV i odpowiedź MCP zostały potwierdzone.
