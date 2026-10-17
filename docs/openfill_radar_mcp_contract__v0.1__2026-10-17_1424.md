# OF-163: OpenFill MCP contract for Networking Radar
Date: 2026-10-17 14:24 CEST
Evidence level: SOURCE AUDIT. Not a deployed-version contract.
Sources: McpServer__v0.23__2026-10-17_1130.cs; McpTaskManager__v0.17__2026-10-14_2235.cs; IRunHost__v0.10__2026-10-12_1810.cs.

## Transport and identity
- MCP: Streamable HTTP JSON-RPC 2.0, POST /mcp/{secret}; local listener on 127.0.0.1 and localhost.
- Endpoint token is confidential; do not include its value in examples or reports.
- initialize, ping, tools/list, tools/call supported; notifications return HTTP 202.
- Exactly eight tool definitions in SOURCE: openfill_start_task, openfill_task_status, openfill_reply, openfill_file_transfer, openfill_screenshot, openfill_info, openfill_cancel_task, openfill_maintenance.
- Every start requires requester and chat. Identity scope is their combination.
- start: task, requester, chat; optional title, continue_task_id, idempotency_key, wait_seconds.
- idempotency_key: 1-128 ASCII letters/digits/dot/dash/underscore/colon; cannot accompany continue_task_id.
- Repeating identical task text with the same key and requester/chat returns the same task_id, including a persisted record after restart. Different task text with same key returns idempotency_conflict.
- status: task_id, wait_seconds 0-30; poll_wait_max_seconds=30 is a transport wait limit, not task ETA.
- estimated_completion_seconds=null in current SOURCE for unfinished tasks. last_tool_activity_at is reported after observed activity.
- reply: task_id, answer, optional wait_seconds. The calling model uses it only for needs_input.
- file_transfer: separate HTTP PUT/GET token, not inline large JSON payloads.

## Integration outcome rules
- A Radar registration is successful only after the external organizer confirms it. An OpenFill task_id or a Calendar entry alone is not proof.
- For an uncertain result, first check the same task_id and the organizer's actual side effects; do not generate a second registration with a new key.
- For interrupted tasks, check actual external effects before using continue_task_id.
- Radar read-only discovery and registration are distinct tasks and have separate success criteria (OF-158).
- Calendar synchronization is downstream of confirmed registration (OF-159).

## Evidence and remaining gates
- SOURCE: tool names, requester/chat identity, idempotency lookup, poll/ETA fields inspected.
- BUILD: not run for the changed SOURCE.
- TEST: new source tests not executed; baseline 703 PASS/5 FAIL remains historical.
- STAGED: none. DEPLOYED: none. LIVE-VERIFIED: only existing 1.52, not this source.
- Radar backend schema parity, retry-after-network-loss, one real intended registration and Calendar verification remain open.

## Evidence refresh, 2026-10-17 18:32 CEST (OF-163/185)
The earlier TEST paragraph is historical. After the activity/state separation and refusal-text restoration, the full OpenFill.Tests suite passed three consecutive times: 714 PASS / 0 FAIL each. The first run compiled Release test dependencies. These results do NOT verify the installed 1.52 application's integration with Networking Radar.
Current source contract: polling may return after an activity signal, but start waits for a task-state change or timeout. poll_wait_max_seconds is not an ETA. Invitation success counts require independently verified external outcomes, not steps or activity messages.
The organizer's confirmation remains mandatory before Radar marks a registration successful or treats the calendar entry as a confirmed registration. Live backend schema parity and one deliberate end-to-end Radar registration remain unverified.
