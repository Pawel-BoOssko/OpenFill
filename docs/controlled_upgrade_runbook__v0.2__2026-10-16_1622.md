# OpenFill: controlled upgrade operator runbook
Version 0.2 | 2026-10-16 16:22 CEST | DOCUMENTATION ONLY

## Current decision: NO-GO
Installed OpenFill is 1.52. Current local source is not a release: McpTaskManager FindByIdempotencyKey references undeclared diskMatch; PrepareShutdown does not reliably restore the maintenance gate and all previously written interrupted records after a later SaveStrict failure. Build/regression on this code is unverified and the prior specific build was safety-blocked. No staged release or SessionId1 GUI acceptance has been demonstrated.

## Recovery evidence
- Private Google Drive contains encrypted archive 1,090,191,400 bytes and encrypted manifest 2,640,047 bytes, snapshot timestamp 2026-10-16 13:03.
- Presence and size are confirmed by Drive API. Cloud archive SHA-256 equality is NOT confirmed.
- Full real-data decrypt + manifest/hash verified restore on SATANAMD is NOT confirmed. Synthetic 2/2 restore is not equivalent.
- Windows Credential Manager and user-scoped DPAPI dependencies require a separate recovery acceptance check.
- Private age identity remains only on SATANAMD. Independent owner-held offline escrow is not verified.
- Source backup is a separate artifact; it is not a replacement for encrypted state recovery.

## Preconditions before any production stop
1. Verify local source compilation issue is fixed and source provenance is known.
2. Complete a permitted release build, tests and isolated GUI smoke. Never route around a specific safety denial.
3. Confirm the release artifact and its SHA-256 in an isolated staging location. Preserve known-good 1.52 binaries.
4. Verify encrypted state archive and manifest against their original hashes, then perform a full segregated restore on SATANAMD.
5. Check restored file count, SHA-256, manifest path uniqueness, unsafe tar entries, key usability and relevant credential/DPAPI limitations.
6. Record the exact current production process executable, version and interactive SessionId1 ownership.
7. Check active MCP tasks and outstanding external side effects. The user authorizes interrupting OpenFill tasks for a real upgrade, not unrelated processes.
8. Prepare a reversible release switch and tested rollback procedure; do not treat source copy or compiled DLL as a deployed version.

## Controlled operator sequence after all gates pass
1. Use the authenticated interactive SessionId1 operator path, never a SessionId0 console substitute for GUI.
2. Enter maintenance with ETA 1-120 min and prevent new tasks.
3. Durably persist interrupted/partial state for each open task; if any write or rollback fails, ABORT shutdown and reconcile persisted records. Never claim a successful graceful stop.
4. Stop OpenFill tasks only after durable persistence; allow pending MCP long polls their response grace period. Disconnected clients receive no push.
5. Stop only OpenFill and wait for its process and WebView2 children to exit. Never snapshot a live browser profile.
6. Take final consistent offline encrypted state backup and verify the files and recovery path.
7. Activate the already validated release, preserve previous 1.52 binaries and recovery artifacts.
8. Confirm real running version, executable path, GUI in SessionId1, MCP tools/list and task-status continuity.
9. Verify domain notes, credential metadata (no secret disclosure), Firefox batch import with synthetic fixture, WebView2 tab lifecycle and Radar idempotent synthetic workflow.
10. Check pending side effects on external services before any continuation; never blindly repeat payments, messages or registrations.
11. Run 30-60 minute post-release acceptance with representative idle and active loads. If critical checks fail, rollback to 1.52 and verify GUI/MCP again.

## Evidence required for GO
Release SHA-256; build and regression logs; isolated restore manifest and hash counts; real GUI screenshot; MCP version/status and task recovery; synthetic HTTP Basic, form and Radar checks; observed rollback readiness. All timestamps and versions must be recorded separately. Do not infer deployment from source, tests or PR.

## Failure boundaries
- Safety-denied staging, deployment wrapper, Core/dependent build and other precise operations remain prohibited by alternate tools or smaller equivalent commands.
- If rollback of task records fails, maintenance must remain gated until reconciliation; do not resume normal work with inconsistent history.
- If real recovery is incomplete, leave installed 1.52 running. This runbook authorizes no deployment by itself.