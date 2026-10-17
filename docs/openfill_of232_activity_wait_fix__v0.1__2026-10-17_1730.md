# OpenFill OF-232: activity vs task state, regression 2026-10-17
Scope: SOURCE and full TEST on PBOX1. Production 1.52 was not modified or restarted.

## Code
- McpTaskManager v0.17: ActivityVer is distinct from Ver; OnActivity increments only ActivityVer, preserves UpdatedUtc as last state update, and retains LastActivityUtc.
- StatusAsync waits for either task-state or activity generation to change.
- StartAsync (including duplicate start) and ReplyAsync wait for task-state generation, not tool activity.
- DomainLocks: exact previous refusal output behavior restored from the 2026-10-16 pre-privacy-change source backup.
- ParallelTests: "refusal text: what" assertion restored. The original "refusal: what it does" check is also restored.
- McpTests: added regression requiring start to return final state after early tool activity.

## Full tests
Test runner: dotnet run --project tests/OpenFill.Tests/OpenFill.Tests__v0.3__2026-10-14_2259.csproj --configuration Release
Run 1: 714 PASS, 0 FAIL, exit code 0 (rebuilt Release dependencies; --no-restore).
Run 2: 714 PASS, 0 FAIL, exit code 0 (--no-build --no-restore).
Run 3: 714 PASS, 0 FAIL, exit code 0 (--no-build --no-restore).
Logs for runs 2 and 3 are in Documents/OpenFill-Backups/Source-Patches/OF232_full_test_2__2026-10-17.log and OF232_full_test_3__2026-10-17.log.
All tests include start/status activity wake, stopped_by_user, waiting_for_user, tools/call, upgrade, and refusal text: what.

## State
SOURCE: changed locally. BUILD: test project and Core dependencies compiled in Release on run 1; NOT an application release build.
TEST: full suite 3/3 passed with no failures.
STAGED: no. DEPLOYED: no. LIVE-VERIFIED: existing OpenFill 1.52 only, PID 17808 SessionId 1.
No release attempted. GitHub synchronization not verified.
