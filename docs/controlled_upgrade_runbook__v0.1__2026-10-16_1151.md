# OpenFill – controlled maintenance/upgrade, runbook
Version 0.1 | 2026-10-16 | Source only, NOT deployed or production-accepted.

## Authorization and limits
The operator has explicitly authorized terminating ALL active OpenFill jobs during an upgrade. There is no requirement to wait for activeTasks=0. This applies only to OpenFill, not other tools or machines. Tool safety blocks on earlier specific staging/deployment actions remain in force and must not be bypassed.

## Client notification contract
OpenFill MCP currently only supports POST JSON-RPC request/response (not event-stream subscriptions). A waiting openfill_start_task/openfill_task_status/openfill_reply caller can receive an interrupted response during the brief grace period before listener disposal. Idle or disconnected models cannot be proactively messaged. After restart clients poll the original task_id and get the persisted interrupted status, ETA and recovery instructions. External side effects of a cut-off operation are UNKNOWN until checked. Never automatically repeat a purchase, RSVP, payment or message without verifying whether it already succeeded.

## Required ordered operator procedure
1. Confirm known-good release artifact, tests, exact version and expected offline downtime. No new installer or workaround for earlier denied staging/wrapper writes.
2. Confirm a private, verified rollback of source and the required production state. Source archive v0.2 and its 152-file baseline restore test exist; latest source backup includes 158 restored/158 verified at 2026-10-16 11:48. Encrypted state backup v0.2 is synthetic-tested only; real production recovery is NOT yet verified and is tied to the same Windows DPAPI user.
3. Start operator-only Upgrade/Close dialog in NEW WinForms build, set estimated downtime (default 10 minutes). If this UI has not been deployed, do not pretend the old 1.52 GUI sends these messages.
4. Code will enable the host maintenance gate, mark each active task interrupted/partial with persisted task ID and continuation guidance, stop each task, and let pending JSON-RPC responses flush for approximately 1.5 seconds before disposal. The operator need not wait for any task to finish.
5. After the old process has exited, create and verify the offline state/profile backup with appropriate user-private encryption and rollback; never copy running WebView2 profile. Do not reveal credential data in chat, public GitHub, general logs or unprotected Drive.
6. Deploy a built and validated release ONLY via a permitted, authenticated Windows interactive SessionId1 path; this runbook is not itself a deploy wrapper. Keep a separate previous release and reversible install. If deployment mechanism is blocked, leave production intact and continue independent source work.
7. Confirm running executable/version, stable profile, real WebView2 GUI, MCP tools/list, openfill_task_status interruption recovery, credentials and domain notes. If any essential check fails, rollback to previous release, retain state and log outcome.
8. Observe 30–60 minutes after successful release and compare resource/functional acceptance. Do not call the source-only change 'deployed'.

## Failure semantics
- Abrupt kill (TaskKill / process termination) cannot guarantee any advance notice or final durable state; prefer controlled WinForms shutdown. Do not assert delivery to disconnected clients.
- The final action may have partially succeeded externally. Recovery requires readback/confirmation before performing another write.
- Requester identity and idempotency key remain associated with the original stored task record. Reconnecting clients should use task_id, not search by title.
- If writing durable interrupted status fails, the feature is not considered production-safe; investigate failure before declaring acceptance.