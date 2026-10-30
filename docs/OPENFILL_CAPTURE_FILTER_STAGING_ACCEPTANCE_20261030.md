# OpenFill: SOURCE -> STAGING acceptance report (2026-10-30, PBOX1)

## Deployment state
- SOURCE modifications completed. STAGING bundle generated outside LIVE. NOT DEPLOYED TO LIVE.
- SOURCE: C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill
- STAGING: SOURCE\backups\STAGING_CAPTURE_CANDIDATE_20261030
- LIVE: C:\Users\pboru\OpenFill\app (OpenFill PID 21012 throughout checks). Production process and DLLs have not been changed or restarted.
- Pre-modification copies of modified source files: SOURCE\backups\source_capture_20261030.

## Architecture
- Default data flow: browser DOM -> full source file(s) -> deterministic filtering -> compact Luna output.
- Existing get_page remains for interactive form UI; page_capture is intended for bulk exports.
- PageCaptureService uses one browser JS DOM read, writes text/elements/HTML plus filtered text, UTF-8, SHA-256, manifest with paths, byte sizes and short previews. The source file remains unfiltered.
- Source recipes per exact host and URL path, selector, mode and regex; captures marked successful increment a counter and store duration for reuse.
- ToolRegistry now exposes page_capture, capture_recipes, terminal_exec, run_js statements/save_to and shared_save from_overflow.
- TerminalProcessService supports powershell, python, installed Git Bash/grep/sed/awk and optional rg/jq (not installed), writes stdout and stderr to files, reports status/exit/timeout/truncation, previews max 400 chars.
- FailedCallGuard blocks an identical third failed call, and unlocks on a proven state change.
- WebView2 main tabs and popups report permission requests, download start/completion and Basic authentication challenges; task-scoped MultipleAutomaticDownloads permission is automatically accepted.
- openfill_page_snapshot via MCP reads ONLY a physically open tab belonging to matching task_id, requester and chat, and validates optional expected URL. No Luna model call, no navigation, data to files.

## Tests performed and evidence
1. SOURCE quick smoke after terminal updates: 111 PASS, 0 FAIL, exit 0. Log: docs\SOURCE_CAPTURE_FINAL_QUICK_20261030.log
2. Final full regression (AFTER all source changes): 1441 PASS, 0 FAIL, exit 0. Log: docs\SOURCE_CAPTURE_RELEASE_CANDIDATE_FULL_20261030.log
3. The integration tests include real headless Chromium/CDP DOM captures and real file writes, filtered text and hashes; ToolRegistry's page_capture/run_js; Git Bash, Python and PowerShell; oversized overflow copy, duplicate-error blocker, HTTP MCP direct capture on test host and identity isolation.
4. STAGING publish from SOURCE: exit 0. Log: docs\SOURCE_CAPTURE_STAGING_FINAL_REPUBLISH_20261030.log
5. STAGING OpenFill.exe independently started as --instance qa_capture_staged_20261030, PID 940, main window titled with [qa_capture_staged_20261030], WebView2 initialized and QA logs created. Controlled CloseMainWindow succeeded; QA process no longer running and temporary QA data directory removed. LIVE PID21012 remained running.
6. SHA256 byte-for-byte match STAGING vs latest compiled SOURCE:
   OpenFill.Core.dll = FF17D148F44056C6B56BFA9C2A2A79C04EFE52615A10B971609E64E223115EA0
   OpenFill.dll = E61E1A35AAF2B28A3153FE9A615B1039ABE6F065C4C31289C428DF1373371F1E
   LIVE files have different hashes, confirming no accidental rollout.

## Limitations requiring real pre-production acceptance
- STAGING QA started, but no test on a logged-in real LinkedIn tab. Browser CDP fixture is validated, real LinkedIn 3-file workload not yet re-run on staging.
- WebView2 PermissionRequested event on actual Microsoft WebView2 UI with multiple simultaneous Blob downloads has not been independently exercised by staging fixture; event handler compiled and its tab-scoped messaging tested at the bus level.
- Direct MCP authorization + HTTP is tested against FakeRunHost, not the physical WebView2 task tab in separately started STAGING.
- The recipe quality score currently counts successful technical captures and last capture duration. It does NOT infer semantic task success or compare failure rates by full user workflow.
- The direct MCP endpoint captures an EXISTING task tab; it does not accept an arbitrary URL and launch a new page by itself.
- A successful DOM capture includes only presently loaded content; it does not crawl lazily loaded or off-screen content automatically.
- Build has pre-existing warnings: CS0168 unused exception variables in JevGate; MSB3277 WindowsBase version 4/5 conflict in WebView2 WPF references. Build exit code remains 0, with no compilation errors.
- A future live deployment must be authorized in a separate stage after real staging acceptance. NO deployment is proposed in this report.

## Status
SOURCE: implemented.
REGRESSION: PASS 1441 / 0.
STAGING: package built, source DLL hashes matched, isolated application start/stop PASS.
LIVE: unchanged.
REAL SITE / WEBVIEW2 PERMISSION / PHYSICAL MCP E2E: pending.
Networking Radar: not touched. No git commit/push/reset performed.
