# OpenFill, OF-187: inwentaryzacja materiałów repo

Data: 2026-10-18, wieczorny odczyt na PBOX1. Zakres wyłącznie READ-ONLY, bez kopiowania ani usuwania plików.

## Źródła danych i kryteria

- Repo: `C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill`.
- Produkcyjne binarki: `C:\Users\pboru\OpenFill\app`, poza repo; produkcyjny profil `C:\Users\pboru\OpenFill\data\stable`, także poza repo. Nie były przedmiotem skanowania ani zapisu.
- Kontrola `Get-ChildItem -File -Recurse` z wykluczeniem segmentów katalogów `.git`, `bin`, `obj`, `packages`. To kontrola drzewa na dysku, **nie** niezależne porównanie ze wszystkimi obiektami GitHub.
- Wynik: **311 plików**. Najwięcej: `.cs` 112, `.md` 85, `.ps1` 38, `.log` 29, `.txt` 19, `.csproj` 7, `.props` 4, `.html` 3, `.bak` 3.
- W tym zakresie **0 plików** z rozszerzeniami `.dll`, `.exe`, `.pdb`, `.db`, `.sqlite`; nie jest to dowód braku binarek w wykluczonych `bin`/`obj`.
- Filtr nazw `password|credential|secret|\.pfx|\.p12|\.pem|\.key|\.identity|\.age|\.env` zwrócił 15 dopasowań, m.in. nazwy klas obsługi Firefox/DPAPI/credential vault, testów i dokumentacji. **To tylko słowa w nazwie, nie znalezione poświadczenia.** Nie odnaleziono wśród nich pliku prywatnej tożsamości age na podstawie nazwy.

## Pozostały zakres OF-187

- Przegląd zawartości plików `.log`, `.bak`, `.txt`, historycznych `__pre_...` pod kątem diagnostyki zawierającej tokeny, danych osobowych i duplikatów klas. Nie wolno publikować znalezionych wartości.
- Porównać dopuszczone pliki z faktycznym drzewem `autosync/PBOX1`; samo `git status` w repo z niestandardową konstrukcją lokalnego main nie jest dowodem pełnej synchronizacji.
- Potwierdzić politykę wyłączeń binarek, materiałów profilu, prywatnego klucza i plików urządzeń, bez omijania istniejącego skanera ani poprawiania jego wyjątków.
- W razie znalezienia historycznego sekretu: zgłosić lokalizację i zakres bez wypisywania treści, skierować do zatwierdzonej procedury usunięcia/rotacji; nie wprowadzać samowolnie rebase, force push ani resetu lokalnego repo.

## Status

**OF-187: częściowa inwentaryzacja wykonana, audyt treści i zdalnego drzewa jeszcze otwarte.** Skan był wyłącznie odczytowy. Brak automatyzacji ChatGPT i ręcznych pushy.
