# OF-140 / OF-143: release process v0.1, 2026-10-17
No release was executed. Installed OpenFill is 1.52, PID 17808 in SessionId 1 (tasklist); the scheduled task OpenFill is Interactive only and points to C:\Users\pboru\OpenFill\app\OpenFill.exe.
The source project declares 1.54, but the existing 1.54 candidate predates current sources. Do not label it tested or staged.
Release gates, each requiring separate evidence:
1. SOURCE: inventory exact changed files and version; freeze candidate.
2. BUILD: restore/build/publish from this source; require zero build errors.
3. TEST: automated suite zero FAIL, including OF-212/218/219/232; five earlier failures unresolved.
4. STAGED: separate complete candidate tree; check manifest, version and package hash. Prior stage operation was denied; do not reroute it.
5. PREFLIGHT: verify current production and MCP, maintenance readiness, all task owners, no active work, backup and rollback material.
6. BACKUP: real-data encrypted restore on SATANAMD, hashes checked. Not yet verified.
7. DEPLOYED: authorized interactive SessionId > 0 channel, Windows Scheduler task, controlled maintenance and atomic rollback path. Session 0 shell must not launch GUI.
8. LIVE-VERIFIED: actual version, MCP status/start, UI, one real authorized workflow, no profile loss, telemetry.
9. ROLLBACK: test restoring the prior binary and state without losing current profile.
Current decision: NO-GO. No .NET build, staging, shutdown, wrapper execution or deployment in this run.
Observed wrapper tools/deploy_safely__v0.1__2026-10-14_2235.ps1 checks SessionId and maintenance, but invokes Start-Process in its own session. Do not execute it from MultiHost SessionId 0.
