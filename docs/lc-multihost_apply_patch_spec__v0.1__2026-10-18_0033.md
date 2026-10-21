# LC MultiHost: narzędzie `apply_patch` — specyfikacja
Metadata: wersja 0.1, data 2026-10-18 00:33. Autor: Claude (na prośbę Pawła). Odbiorca: czat rozwijający LC MultiHost.

## 1. Po co

Dziś każda zmiana pliku przez MultiHost idzie przez `execute_command`: treść pliku trzeba wkleić w skrypt PowerShella. Skutki widziane 17.10.2026:
- zabezpieczenia OpenAI blokowały nawet drobne zmiany (np. `true` → `false`), bo wywołanie wygląda jak dowolny skrypt na komputerze użytkownika;
- ogromne polecenia z here-stringami, problemy z cudzysłowami i kodowaniem;
- łatka z końcami linii LF nie weszła na pliki z CRLF (`git apply` odrzucił całość).

`apply_patch` to **jedno** nowe narzędzie, które robi wyłącznie zmianę treści plików. Wszystko inne (czytanie, szukanie, budowanie, testy, procesy) zostaje w `execute_command`. Nie dodajemy osobnych narzędzi typu `read_file`, `list_directory`, `move_file` — PowerShell robi to dobrze.

Format łatki jest ten sam, którego używa Codex (`*** Begin Patch` … `*** End Patch`), więc modele OpenAI znają go bez uczenia się.

## 2. Wywołanie

| Parametr | Typ | Wymagany | Znaczenie |
|---|---|---|---|
| `target` | string | tak | komputer, jak w `execute_command` (`pbox1`, `satamad`) |
| `patch` | string | tak | treść łatki w formacie z punktu 3 |
| `base_dir` | string | nie | folder, względem którego liczą się ścieżki względne; bez niego ścieżki muszą być bezwzględne |
| `dry_run` | bool | nie (domyślnie `false`) | tylko sprawdza, czy łatka pasuje, i zwraca wynik bez zapisu |
| `expect_sha256` | obiekt `{ścieżka: sha256}` | nie | zmień plik tylko, jeśli jego obecna zawartość ma ten skrót (ochrona przed nadpisaniem cudzej zmiany) |
| `new_file_eol` | `"crlf"` / `"lf"` | nie (domyślnie `"crlf"`) | końce linii dla plików tworzonych |
| `new_file_bom` | bool | nie (domyślnie `false`) | BOM UTF-8 dla plików tworzonych (np. `.ps1` z polskimi znakami: `true`) |
| `chat_key`, `requester`, `chat` | string | nie | tożsamość, tak jak w pozostałych narzędziach MultiHost |

Działa synchronicznie: zwraca wynik od razu (bez `task_id`), bo zapis plików trwa ułamek sekundy. Nie uruchamia PowerShella — jest zaimplementowane w samym serwerze MultiHost, działa jako ten sam użytkownik Windows co `execute_command` i wymaga tego samego poziomu dostępu co zapis przez `execute_command`.

## 3. Format łatki (przykład poglądowy)

```
*** Begin Patch
*** Update File: src/OpenFill.Core/Hosting/TabManager__v0.6__2026-10-12_2200.cs
@@ public bool ReleaseFinishedTabs
-    public bool ReleaseFinishedTabs { get; set; } = true;
+    public bool ReleaseFinishedTabs { get; set; } = false;
*** Add File: docs/raport__v0.1__2026-10-18_0900.md
+# Raport
+Treść.
*** Delete File: docs/stary_raport__v0.1__2026-10-17_1200.md
*** Update File: src/OpenFill.Core/Mcp/McpServer__v0.23__2026-10-17_1130.cs
*** Move to: src/OpenFill.Core/Mcp/McpServer__v0.24__2026-10-18_0900.cs
@@
-// OpenFill - Metadata: wersja 0.23, data 2026-10-17 11:30
+// OpenFill - Metadata: wersja 0.24, data 2026-10-18 09:00
*** End Patch
```

Zasady:
- `*** Add File: <ścieżka>` — nowy plik; każda linia treści zaczyna się od `+`. Jeśli plik istnieje → błąd.
- `*** Delete File: <ścieżka>` — usunięcie istniejącego pliku.
- `*** Update File: <ścieżka>` — zmiana; opcjonalnie zaraz po niej `*** Move to: <nowa ścieżka>` (zmiana nazwy razem ze zmianą treści — pasuje do konwencji wersji w nazwach plików OpenFill).
- Fragment zmiany zaczyna się od `@@` (po nim opcjonalnie tekst ułatwiający znalezienie miejsca, np. nazwa metody). Linie: ` ` (spacja) = kontekst bez zmian, `-` = usuń, `+` = dodaj.
- `*** End of File` na końcu fragmentu = fragment musi pasować do końca pliku.
- Ścieżki: bezwzględne (`C:\...`) albo względne wobec `base_dir`; ukośniki `/` i `\` traktowane tak samo.

## 4. Zachowanie

1. **Najpierw całość jest sprawdzana, potem zapisywana (wszystko albo nic).** Jeśli którykolwiek fragment nie pasuje, nie zmienia się żaden plik.
2. **Dopasowanie fragmentu:** kontekst i linie `-` muszą wystąpić w pliku dokładnie raz (w kolejności fragmentów, każdy szukany za poprzednim). Porównanie ignoruje różnice końców linii (CR) i BOM; inne białe znaki muszą się zgadzać. Gdy fragment pasuje w więcej niż jednym miejscu → błąd „niejednoznaczne” z numerami linii.
3. **Format pliku zostaje zachowany:** końce linii (CRLF/LF — według przeważających w pliku), BOM, kodowanie UTF-8, końcowy znak nowej linii (jest albo go nie ma — jak było). Dodane linie dostają końce linii pliku.
4. **Bezpieczny zapis:** każdy plik zapisywany do pliku tymczasowego w tym samym folderze, potem atomowa podmiana. Przed zapisem sprawdzenie, czy plik nie zmienił się od chwili walidacji (skrót); jeśli tak → przerwanie i przywrócenie już zapisanych plików.
5. **Kopia zapasowa:** przed zmianą/usunięciem oryginały trafiają do `%LOCALAPPDATA%\LupusCommander\patch-backups\<data_godzina>_<id>\` z zachowaniem ścieżek. Ścieżka kopii wraca w wyniku.
6. **Limity techniczne:** łatka do 2 MB, plik do 20 MB. Bez innych ograniczeń.
7. **Log:** wpis w zwykłym logu MultiHost: kto (requester/chat), które pliki, operacje, skróty przed/po, ścieżka kopii.

## 5. Wynik

Sukces:
```json
{
  "status": "applied",            // albo "dry_run_ok"
  "files": [
    { "path": "…/TabManager__v0.6__2026-10-12_2200.cs", "op": "update", "hunks": 1,
      "sha256_before": "…", "sha256_after": "…", "eol": "crlf", "bom": true },
    { "path": "…/McpServer__v0.24__2026-10-18_0900.cs", "op": "move+update", "from": "…/McpServer__v0.23__2026-10-17_1130.cs" }
  ],
  "backup_dir": "C:\\Users\\…\\patch-backups\\2026-10-18_0901_ab12cd"
}
```

Błąd (nic nie zapisano):
```json
{
  "status": "rejected",
  "file": "…/TabManager__v0.6__2026-10-12_2200.cs",
  "hunk": 1,
  "reason": "not_found",          // not_found | ambiguous | file_exists | file_missing | sha256_mismatch | changed_during_apply | parse_error
  "detail": "Linie '-' nie występują w pliku. Najbliższy podobny fragment: linie 41–43:",
  "nearest": "    public bool ReleaseFinishedTabs { get; set; } = true;  // komentarz"
}
```

## 6. Opis narzędzia widziany przez model (do wklejenia w definicję)

> Applies a patch to text files on the chosen computer: add, update, rename or delete files in one call. Use the `*** Begin Patch` / `*** End Patch` format (`*** Add File:`, `*** Update File:`, optional `*** Move to:`, `*** Delete File:`; hunks start with `@@`, lines prefixed with space, `-` or `+`). All changes are applied together or none. Line endings, BOM and encoding of existing files are preserved. Use `dry_run` to check a patch without writing. Use `execute_command` for everything else (reading, searching, building, tests).

Bez list ostrzeżeń i zakazów — opis ma mówić tylko, co narzędzie robi.

## 7. Testy (do zaliczenia przed wdrożeniem)

1. Zmiana jednej linii w pliku CRLF z BOM → plik nadal CRLF + BOM, zmieniona tylko ta linia.
2. To samo dla pliku LF bez BOM.
3. Łatka napisana z LF stosowana do pliku CRLF → wchodzi (przypadek z 17.10).
4. Dwa pliki, drugi fragment nie pasuje → żaden plik niezmieniony, `reason: not_found`, podany najbliższy fragment.
5. Fragment pasujący w dwóch miejscach → `ambiguous` z numerami linii, nic nie zapisane.
6. `Add File` dla istniejącego pliku → `file_exists`.
7. `Move to` + zmiana treści → stary plik zniknął, nowy ma zmienioną treść, kopia starego w `backup_dir`.
8. `Delete File` → plik usunięty, kopia w `backup_dir`.
9. `expect_sha256` niezgodny → `sha256_mismatch`, nic nie zapisane.
10. Plik zmieniony przez inny proces między walidacją a zapisem → `changed_during_apply`, wcześniej zapisane pliki przywrócone.
11. `dry_run` → wynik jak przy zapisie, pliki niezmienione.
12. Polskie znaki w treści i w ścieżce (np. `H:\Mój dysk\…`) → zapis poprawny w UTF-8.
13. Plik bez końcowego znaku nowej linii → po zmianie nadal bez niego.
14. Prawdziwe użycie z ChatGPT: zmiana `releaseFinishedTabs` w kopii OpenFill (OF-230), potem `dotnet build` przez `execute_command`.

## 8. Czego nie robić

- Nie dodawać kolejnych narzędzi plikowych (read/list/move/search) — to robi `execute_command`.
- Nie dodawać ograniczeń folderów, list zakazanych plików ani potwierdzeń — Paweł o nie nie prosił.
- Nie uruchamiać PowerShella w środku tego narzędzia.
