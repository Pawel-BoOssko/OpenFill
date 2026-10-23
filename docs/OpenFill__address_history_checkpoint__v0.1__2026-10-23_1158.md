# OpenFill: address-bar history, checkpoint 2026-10-23 11:58

## User issue
Address bar has no history, forcing full URL entry each time. User requested normal browser-style autocomplete.

## Implemented in SOURCE
- `src/OpenFill.Core/Hosting/AddressHistory__v0.1__2026-10-23_1141.cs`: local MRU history, up to 200 HTTP(S) addresses in `data/<instance>/address_history.json`. Search-friendly display omits leading `https://www.` (e.g. `carrefour.ke`, `example.com/checkout`). Resolves displayed suggestion to its recorded full website URL. Persistent across app restarts.
- `src/OpenFill.App/MainForm__v0.26__2026-10-23_1143.cs` replaces v0.25. TextBox -> editable WinForms ComboBox with `AutoCompleteMode.SuggestAppend`, `AutoCompleteSource.ListItems`, arrow/dropdown, Enter and Go; loads saved history at startup and records navigation for all tab WebViews through `SourceChanged`. Refresh preserves user's typed text.
- Privacy: no passwords/userinfo, query strings, fragments or known recovery/oauth callback paths persisted. Rejects non-HTTP URLs including file:, javascript:, about:. Writes atomic JSON; read errors/malformed history do not prevent browser startup.
- `tests/OpenFill.Tests/AddressHistoryTests__v0.1__2026-10-23_1146.cs`: sanitization, recent ordering, unique entries, 200 cap, URI resolution, persistence and corrupted history tests. `Program__v0.24__2026-10-23_1147.cs` wires tests in.
- Existing WebView2 navigation history is not scraped or imported retroactively. The new list starts collecting visited URLs when this version runs.

## Build and verification
- Release build of full solution: **0 errors**, 1 known MSB3277 warning, exit 0 (PBOX1).
- Full Release regression: **1221 PASS / 0 FAIL**, exit 0.
- `Directory.Build.props`: OpenFillVersion 1.57, OpenFillVersionDate 2026-10-23T11:55.
- `dotnet publish` of OpenFill.App, Release, to **isolated candidate path** `C:\Users\pboru\OpenFill\staging\1.57`: exit 0, 13 top-level files, executable `1.57.0.0`, SHA-256 `A88CCD9D7B6D49101BC1C3F94F66712850816E527DB513346449AD9F050C0B02`. Candidate is not installed/started.
- Production OpenFill remains **1.56**, MCP healthy. At 11:57 it reported two active tasks: n8n audit `OF-20261023-1140-e47a` and an active Carrefour Kenya purchase task `OF-20261023-1157-11f5`.
- To preserve live checkout/form state, **no production restart**, no maintenance, no interrupted tasks, no installation; particularly do not destroy active Carrefour browser session to change the address bar. User's prior instructions make preserving pages for payment a priority.
- No ChatGPT automations or manual git push/commit. Existing Autosync may publish sources on its normal schedule.

## Deployment steps after the browser is free
1. Read OpenFill MCP `openfill_info`, ensure none of running/needs_input/waiting_for_user. Also check no page is currently waiting for payment before restarting, because any process restart destroys live WebView2 tab DOM even with a saved URL.
2. Backup whole current `C:\Users\pboru\OpenFill\app` (1.56) into distinct `app-1.56`, verify `OpenFill.exe` and SHA-256; never touch `data\stable`.
3. Maintenance, controlled installer `tools/install__v0.13__2026-10-18_1650.ps1 -NoShortcuts`, smoke `openfill_info` v1.57, read-only navigation into `https://example.com` with GUI evidence of history suggestions.
4. On failure restore old app and verify 1.56, then end maintenance. Full documentation and concrete status required. Do not claim deployment based on SOURCE/BUILD/STAGING.

STATUS: SOURCE COMPLETE, TEST PASS, STAGED, **NOT DEPLOYED**, waiting for a safe release point that does not erase in-flight checkout.
