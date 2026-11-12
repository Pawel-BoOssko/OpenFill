# OpenFill: sterowanie bez modelu i rejestr odwiedzanych czatów ChatGPT

- Utworzono: 2026-11-12, Europe/Warsaw (CET)
- Autor: GPT-6
- Środowisko: PBOX1, Windows, WebView2, .NET 10
- Gałąź: istniejąca `feature/openfill-telegram-20261111`, bez tworzenia nowej
- Repozytorium: https://github.com/Pawel-BoOssko/OpenFill

## Cel i rozwiązanie

OpenFill obsługuje dwie ścieżki: dotychczasową ścieżkę AI przez agenta oraz niezależne lokalne HTTP API sterujące rzeczywistymi kartami WebView2. Nowa ścieżka NIE tworzy zadania w AgentRunner, NIE wysyła zapytań do Luna ani do innego modelu. Wykorzystuje istniejący profil WebView2 i sesje użytkownika.

Serwer jest dostępny tylko na `127.0.0.1:17873`. Obsługuje `GET /health` i `POST /command`. Polecenia wymagają nagłówka `X-OpenFill-Token`, którego wartość znajduje się w lokalnym pliku `%USERPROFILE%\OpenFill\data\<instance>\runtime\direct-browser-api.token`.

Dostępne akcje: `status`, `tabs`, `new_tab`, `select_tab`, `current_url`, `navigate`, `screenshot`, `js`, `html`, `text`, `click`, `type`, `scroll`, `wait`, `batch`. Gdy karta jest zajęta trwającym zadaniem AI, bezpośrednie polecenia do tej karty zwracają `TAB_BUSY_WITH_AI_TASK`, żeby zapobiec konfliktowi. Wykonywanie poleceń HTTP odbywa się seryjnie na wątku UI.

Przykładowe żądanie:

```json
{"action":"navigate","url":"https://example.com"}
```

```json
{"action":"screenshot","folder":"C:\\Users\\pboru\\Documents\\Screenshots"}
```

```json
{"action":"js","script":"document.title"}
```

```json
{"action":"batch","commands":[{"action":"navigate","url":"https://example.com"},{"action":"text"},{"action":"screenshot"}]}
```

## Klient PowerShell

Skrypt: `tools\direct-browser\2026-11-12_14-46-00_CET_GPT-6_OpenFill_SkryptyHTTPBezLuna_NawigacjaJSZrzutyDlaAutomatyzacji.ps1`.

```powershell
$tool = 'C:\Users\pboru\Documents\Projects\OpenFill-SelfChatSteering-Current-20261110\tools\direct-browser\2026-11-12_14-46-00_CET_GPT-6_OpenFill_SkryptyHTTPBezLuna_NawigacjaJSZrzutyDlaAutomatyzacji.ps1'
& $tool -Instance stable -Action status
& $tool -Instance stable -Action new_tab -Url about:blank
& $tool -Instance stable -Action navigate -Url https://example.com
& $tool -Instance stable -Action js -Script 'document.title'
& $tool -Instance stable -Action screenshot
```

Można również uruchomić `-CommandFile D:\zadanie.json`. Skrypt wysyła JSON poleceń bez wywołań AI. `-Instance dev` służy do testów na odrębnym profilu.

## Automatyczny rejestr czatów ChatGPT

OpenFill śledzi zdarzenia `SourceChanged`, `DocumentTitleChanged`, wybór karty i aktywność użytkownika na `chatgpt.com` i `chat.openai.com`. Ponadto po nawigacji asynchronicznie odczytuje tytuł czatu z odpowiadającego identyfikatorowi linku w historii rozmów. Bez DOM i bez modelu.

Baza SQLite powstaje w `%USERPROFILE%\OpenFill\data\<instance>\memory\chatgpt` i ma nazwę z datą i godziną pierwszego utworzenia. Uruchomienie programu ponownie używa tego samego pliku. SQL:

```sql
CREATE TABLE chat_visits (
  chat_id TEXT PRIMARY KEY NOT NULL,
  title TEXT NOT NULL DEFAULT '',
  url TEXT NOT NULL,
  first_access_utc TEXT NOT NULL,
  last_access_utc TEXT NOT NULL
);
CREATE INDEX ix_chat_visits_last_access ON chat_visits(last_access_utc);
```

Rejestrowane są rozmowy o ścieżce `/c/<chat_id>`, również w obrębie `/g/.../c/<chat_id>`. Parametry zapytania i fragment URL są usuwane. Ogólny tytuł przeglądarki `ChatGPT` nie nadpisuje właściwego tytułu rozmowy. Rejestr przechowuje ostatni dostęp z czasem UTC w ISO 8601. SQL do odczytu:

```sql
SELECT chat_id, title, url, last_access_utc
FROM chat_visits ORDER BY last_access_utc DESC;
```

## Wyniki testów

- Build OpenFill App: sukces, .NET 10, 0 błędów kompilacji.
- Smoke: 11/11 PASS, SQLite upsert, UUID, ponowne otwarcie bazy, HTTP health, token, komenda bez modelu.
- Test WebView2 PBOX1, profil dev: utworzenie karty, nawigacja, odczyt `document.title`, PNG, brak elementu jako `NOT_FOUND`.
- Test integracyjny rejestru: wejście na testowy adres `/c/00000000-0000-4000-8000-000000000001` utworzyło rekord SQLite.
- Wersja zainstalowana na PBOX1 przed zmianą: 1.62; wersja w edytowanym drzewie miała lokalnie numer 1.60. Nie wolno nadpisać produkcji starszym buildem.

## Uwagi eksploatacyjne

Plik SQLite przechowuje stan trwale. Nie jest tworzony nowy log tekstowy codziennie, ponieważ wybrano wariant z jedną bazą SQL, o który poprosił użytkownik. Wersje oprogramowania, historia Git i logi kompilacji pozostają oddzielne od tej bazy.

Nie otwierać tej samej karty równocześnie z agentem AI. Nowy API wymusza tę zasadę na poziomie kodu. Osobna instancja `dev` umożliwia test bez zmiany profilu `stable`.

