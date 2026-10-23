# OpenFill 1.56: deployment & production validation
Date: 2026-10-23 11:35, PBOX1.

## Why
1. OF-230: MainForm v0.24 constructed `TabManager(... releaseFinishedTabs: true)`, so EndAsync immediately closed a finished task tab or navigated it to `about:blank`. This discarded checkout/form/session state while the agent falsely claimed the tab was left for the person to pay.
2. OF-241: long agent history grew unbounded and repeatedly rewrote the cached prefix; see `OpenFill__issue_cache_cost_resolution__v0.1__2026-10-23_1110.md`. Cache improvements prepared 23.10 now deployed, but their actual monetary benefit remains to be measured.

## Code and configuration deployed
- `src/OpenFill.App/MainForm__v0.25__2026-10-23_1122.cs`, old v0.24 renamed, instantiates `TabManager(... releaseFinishedTabs: false)`.
- `tests/OpenFill.Tests/TabTests__v0.7__2026-10-23_1125.cs`, old v0.6 renamed, 8 new assertions showing that the completed checkout tab keeps its live browser and payment URL and that same-chat continuation reuses the physical tab.
- Cache files already tested from prior checkpoint: `AgentRunner__v0.28__2026-10-23_1055.cs`, `PromptHistoryCompactor__v0.1__2026-10-23_1054.cs`, `PromptHistoryTests__v0.1__2026-10-23_1100.cs`.
- `Directory.Build.props` version 1.56; version_date 2026-10-23T11:26.
- Installer `tools/install__v0.13__2026-10-18_1650.ps1`, controlled CloseMainWindow, no `Stop-Process -Force`, waits at most 25 seconds before abort.

## Backup and tests
- Backup of **entire app directory**: `C:\Users\pboru\OpenFill\app-1.55`; 23 files, 5,202,778 bytes; initial comparison of source/backup file count, size, and executable SHA-256 passed.
- Old exe backup: version 1.55.0.0, SHA-256 `D1EB2143D56973F90BF31D7747F32FAABCA0D2F997D91339BBAE9946EB840783`.
- Source .NET Release build: 0 errors, 1 known MSB3277 warning (WindowsBase/WebView2).
- Full Release tests before version bump: **1200 PASS, 0 FAIL**, exit 0 (checkout regression included). Build after version 1.56 bump: 0 errors, 1 known MSB3277 warning, exit 0.
- Install: `powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File ...\tools\install__v0.13__2026-10-18_1650.ps1 -NoShortcuts -NoAutostart`; exit 0, log `%TEMP%\openfill_install_20261023_112749.log`.
- Controlled shutdown: the 1.55 window terminated normally, no force kill; stable user profile reused without reinitialization.

## Production health
- `openfill_info`: **version 1.56**, build 2026-10-23 11:28, original MCP endpoint healthy, same identity/route.
- Actual exe version 1.56.0.0; SHA-256 `C2EC4D8D157B74652E7C1B4ADB0053B2AB0D82231BFB75BB30F8E99CF7C4045A`.
- Maintenance 15 minutes was started, then explicitly stopped with `minutes=0` after health verification.
- Smoke task `OF-20261023-1129-b85c`, 6 steps, done/success: `https://example.com/`, exact title `Example Domain`, **physical tab `t74df580d` remained open**, `state=finished`, `reuse_ready=true`.
- Follow-up with first sentence not identifying navigation domain created a second tab `te856f28d`, although the model summary claimed reuse. Treat that message as not reliable evidence. Explicit recognized task `Open https://example.com/` then reused **the same physical `te856f28d`** (`reason=existing_session_reused`, done/success). User can manually close these two read-only example.com test tabs when desired.
- `openfill_info` at 11:33:51 showed v1.56, maintenance false, 0 running tasks.
- **Limit still 5 live tabs**. Beyond the limit, the oldest finished tab may be evicted to make room for new tasks. Manual/user-created tabs and busy tabs are protected. Existing tabs from the previous process are inherently destroyed by installation/restart; this fix applies to tabs created by 1.56 while it remains running. Overnight/restart and checkout on real paid sites remain unverified.

## Three tasks interrupted intentionally by controlled upgrade
They had been running in 1.55 when maintenance began. No external actions were automatically retried:
- `OF-20261023-1123-57db` Jina AI API key lookup: `interrupted/partial`.
- `OF-20261023-1125-f864` Ziwa WhatsApp read-only check: `interrupted/partial`.
- `OF-20261023-1125-6ff0` Carrefour Kenya purchase workflow: `interrupted/partial`.
Their owners should check the external side effects and use the same task ID for continuation where needed. No statements of completion or payment are justified by these task statuses.

## Rollback if future smoke fails
1. Stop current OpenFill through controlled window shutdown. Only if truly necessary and after separately evaluating task state use a stronger termination; never silently lose running tasks.
2. With application stopped, rename `C:\Users\pboru\OpenFill\app` to a distinct `app-1.56-failed` and rename `app-1.55` to `app`.
3. Start existing interactive-session Windows task `OpenFill` via `schtasks /run /tn OpenFill`. Confirm `openfill_info` version 1.55 and app health.
4. **Never modify** `C:\Users\pboru\OpenFill\data\stable` as part of app rollback; this procedure concerns application binaries only.
No rollback was required for this release.

## Acceptance still open
- OF-241 true cache hit rate >=90% except checkpoint and cost <=$0.05 for comparable 14-contact workload **not yet proven**. No unexpected invitations or payments sent by rollout smoke.
- OF-230 checkout form persistence across >5 tasks, day-long inactivity, and WebView2 restart not yet proven.
- Production rollout DONE, TESTS PASS, backout verified; no ChatGPT automations and no manual git push/commit.
