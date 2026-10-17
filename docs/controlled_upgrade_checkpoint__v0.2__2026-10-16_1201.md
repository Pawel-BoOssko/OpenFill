# OpenFill – controlowany upgrade, checkpoint v0.2
Time: 2026-10-16 12:00 CEST.
Scope: source-only development, backup integrity, updated hourly continuation.

## Confirmed completed work in THIS chat
- Updated existing hourly automation ID 6acfe335853c8191941554ada2e9d690: 4-8 independent packages, work to completion, new permission to stop/interrupt ALL OpenFill work for controlled upgrades. Never kill other processes; no unapproved bypass.
- Master plan v1.2: docs/openfill_master_action_plan__v1.2__2026-10-16_1142.md, adds items 203-211; new operator authorization supersedes zero-active-task condition for a controlled upgrade.
- New src/OpenFill.App/ControlledUpgradeDialog__v0.1__2026-10-16_1144.cs supports 1-120 minute downtime, default 10, explanatory confirmation.
- MainForm source now invokes the dialog from Tech Atlas Close and marks MCP tasks interrupted, then delays 1500ms before disposing HTTP server to allow active long poll replies. This UI HAS NOT BEEN DEPLOYED.
- McpTaskManager.PrepareShutdown supports ETA 1-120, host maintenance gate before marking running/needs_input/waiting_for_user tasks interrupted/partial, persists their identifiers and recovery instructions, cancels them individually, and isolates failure in individual Stop() calls.
- McpTaskManager maintenance status, McpServer tool help, and AppHost maintenance refusal text now explain that a LOCAL operator can upgrade with running jobs (old texts incorrectly instructed to wait for zero).
- McpTests source gained 20 assertions testing multiple active tasks, estimated downtime, persisted status after restart, reconnected retrieval, HTTP long-poll notification and gate refusal for new tasks. Added maintenance behavior to FakeRunHost. THIS TEST CODE HAS NOT BEEN COMPILED OR EXECUTED.
- docs/controlled_upgrade_runbook__v0.1__2026-10-16_1151.md documents operator steps, client messaging limitations and rollback.
- Verified new local private source/docs backup: C:\Users\pboru\Documents\OpenFill-Backups\OpenFill-source__v0.2__2026-10-16_120002, EXACTLY 160 files verified restored by SHA-256, ZERO mismatches.

## Not done / do not claim
- No release build or automated test execution after these new source changes. Historic earlier refusal on Core and dependent build operations was respected.
- No actual online multi-model notification has been sent to production clients; old installed production OpenFill 1.52 PID5000 Windows SessionId1 was checked alive around 12:00 and untouched.
- No active session restart and no staged/deployed v1.54; earlier specific stage/wrapper requests were security-blocked and not rerouted.
- Protocol today is only stateless POST JSON-RPC; active waiting HTTP calls may receive notice before shutdown. Dormant/disconnected sessions cannot receive proactive push and must poll task history after restart. Do not falsely claim notification to all disconnected models.
- Source has best-effort Save() that silently catches disk write errors: mandatory strict-persist failure handling for interrupted task state needs a separate implementation/test.
- Encrypted offline state backup v0.2 only synthetic 3/3 DPAPI CurrentUser test. No portable key escrow, no validated restore of real production profiles/credentials.
- No real GUI acceptance, release deployment, or post-release performance comparison; these remain open.

## Technical details
- Windows Scheduler OpenFill task uses an Interactive logon user pboru, with executable path C:\Users\pboru\OpenFill\app\OpenFill.exe, task Running; separate OpenFill-open task Ready. An OpenFill-watchdog task was not present when separately queried; do not assume any unattended safe restart without a tested process lifecycle.
- Do not forcibly kill 1.52 merely for testing the still undeployed GUI. User authorizes termination but current release safety and rollback conditions still matter.
- One failed attempted replacement was an ordinary source text mismatch, not a security denial; corrected after reading actual snippet. No blocked staging or Core build retried.

## Next independent packages
A. Add reliable verification/error handling to persistent interrupted task record updates and test failure injection.
B. Obtain an authorized build/test path without circumventing previous tool denial, execute new MCP E2E long-poll tests, real WinForms smoke.
C. Encrypted portable disaster recovery for production state, with user-safe recovery key escrow and verified offline restore, avoiding secrets in public services.
D. Actual Session1 controlled release / rollback, only when backup and build are verifiably ready; test online client notifications and version.
E. Continue independent PSL, credential importer, WebView2 cleanup, MCP tasks and Networking Radar E2E without conflicting with other chats.