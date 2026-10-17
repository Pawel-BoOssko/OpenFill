# OpenFill: controlled upgrade implementation checkpoint v0.1
Date: 2026-10-16 Europe/Warsaw
Release status: SOURCE ONLY, NOT BUILT/TESTED OR DEPLOYED as a new OpenFill version.

## New user authorization
Operator may deliberately stop OpenFill and interrupt ALL OpenFill jobs even if activeTasks > 0; no need to wait for completion before controlled upgrading. Do not touch processes other than OpenFill. Existing explicit tool safety denials on staging/deployment are still binding.

## Results in this working session
1. The active hourly ChatGPT automation 6acfe335853c8191941554ada2e9d690 was updated with the new stop/interrupt authorization, correct MCP delivery limits, 4-8 work packages, backups and safety.
2. New master plan docs/openfill_master_action_plan__v1.2__2026-10-16_1142.md, 9 additional numbered requirements 203-211, previous plan preserved. This section supersedes the old wait-until-zero requirement.
3. src/OpenFill.Core/Mcp/McpTaskManager__v0.17__2026-10-14_2235.cs: PrepareShutdown now accepts estimatedMinutes (default 10; 1-120), sets maintenance gate first, marks every open MCP task interrupted/partial, persists recovery advice and ETA, then stops each task. New maintenance guidance allows operator-controlled shutdown with active tasks. New content is source-only, not tested.
4. src/OpenFill.App/ControlledUpgradeDialog__v0.1__2026-10-16_1144.cs: WinForms local operator dialog to choose downtime (1-120 min; default 10), confirms interruption of all running tasks, cautions about disconnected clients and potentially incomplete external actions.
5. src/OpenFill.App/MainForm__v0.24__2026-10-14_2259.cs: atlas Close flow now opens the controlled upgrade dialog, forwards ETA and a maintenance reason; OnFormClosing allows ~1500ms for in-flight MCP JSON-RPC long polls before disposing server. Ordinary window close uses 10 min default.
6. src/OpenFill.Core/Mcp/McpServer__v0.22__2026-10-14_2235.cs: clarified openfill_maintenance tool instructions (blocks new work; only LOCAL operator closes and interrupts; no unsolicited push to disconnected clients).
7. tests/OpenFill.Tests/McpTests__v0.12__2026-10-14_2235.cs: added 13 assertions on parallel interrupt/ETA/durable recovery plus 5 on actual HTTP long-poll notification. They were NOT RUN. Prior 669 PASS does NOT apply to these changes.
8. Fresh private source/docs backup C:\Users\pboru\Documents\OpenFill-Backups\OpenFill-source__v0.2__2026-10-16_114816: 158 files, SHA256 manifest, 158-file isolated restore verified, 0 mismatch. A later small text amendment occurred after this snapshot and will be captured by daily source backup.
9. Additional runbook docs/controlled_upgrade_runbook__v0.1__2026-10-16_1151.md: authorized steps, recovery and caveats.

## Important technical limits
- MCP is currently only stateless HTTP POST JSON-RPC with JSON replies, NOT SSE / persistent push. A client waiting on a live status/start/reply HTTP request may see interrupted within the 1500ms grace; disconnected/idle clients cannot be proactively contacted. They can poll historical task_id after reconnect. No fabricated guarantee of delivery.
- Current task Save() catches write errors as best effort. Interrupted notices are persisted when disk works, but durable write failure detection still needs a hardening/test before release.
- The old installed OpenFill 1.52 remains in production and was NOT stopped, upgraded or restarted in this session. The new UI exists only in source.
- Real production profile/data backup is not completed, and DPAPI-only offline synthetic backup is not a portable disaster recovery solution.
- Earlier specific dotnet Core/dependent builds and staging/deployment-wrapper commands were denied by tool safety. They were not retried using an alternate build or installer; new tests are NOT counted as passed.
- One failed source edit at 11:49 was due to a mismatched search string (ordinary PowerShell exception, NOT safety refusal). Inspected the exact line, corrected it in a permitted independent operation; no security block claimed.

## Priority next work
A. Harden the maintenance durable status write/recovery and verify concurrent clients with newly added tests, when an authorized build/test operation becomes available.
B. Real offline backup/portable recovery with private key escrow, restore acceptance and rollback manifest. Do not stop production merely to produce an unverified copy.
C. Complete source feature quality: WinForms technology atlas and credentials, Firefox multi-domain importer/realm and exact-origin, PSL tenant handling, WebView2 cleanup, agent and MCP.
D. Preserve concurrent other-chat source edits; review changed files before editing and stage for legal SessionId1 rollout only if approved tools permit.
E. Deploy, inspect GUI and MCP live, check task recovery and measure resource change. Source-only results are not production acceptance.