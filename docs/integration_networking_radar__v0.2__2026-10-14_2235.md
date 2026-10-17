# Networking Radar -> OpenFill 1.49 | Technical Integration

## Metadata
- Document version: 0.2
- Date: 2026-10-14 22:35
Version 0.2; Warsaw time 2026-10-13 22:39

## Transport
OpenFill MCP over Streamable HTTP, JSON-RPC 2.0. POST /mcp/{secret}. The already-configured remote HTTPS base URL and secret are only for the Radar server-side backend (never browser JavaScript).
Local endpoint listens on port 48731 in the current PBOX1 configuration.

## Start
JSON-RPC method tools/call, name openfill_start_task.
Required arguments: task, requester, chat.
Recommended:
- requester: "Networking Radar"
- chat: "Networking Radar registration"
- title: event short title
- wait_seconds: 0 to 30
- idempotency_key: a stable identifier for this intended registration (example: radar:user123:event456:registration:v1)

The idempotency key is an optional 1-128 character ASCII string of letters, digits, dash, underscore, dot and colon. Its scope includes requester and chat.
Repeating an IDENTICAL task with the same key returns the original task_id and result (including after restarting OpenFill). Using that key with different task text returns status=idempotency_conflict without creating a task. A different key denotes a different intended registration. Do not generate a new key when merely polling or retrying a network timeout.

Task text should contain event name, date/time, event landing page, registration form URL, person's necessary registration data and clear completion evidence expected (confirmation screen, registration number, admission ticket or confirmation email). Price and payment boundaries should be explicit.

## Lifecycle and result
Use tools/call openfill_task_status with task_id and wait_seconds (max 30).
status: running, needs_input, waiting_for_user, done, failed, blocked, stopped_by_user, cancelled and other refusal statuses.
outcome: success, partial or failed. status=done alone does NOT imply success.
summary: evidence and explanation in natural language.
Do not mark an event as registered unless status=done, outcome=success and the reported evidence matches the requested registration.
If needs_input, use openfill_reply on the same task_id; waiting_for_user requires interaction in the OpenFill UI.
Store task_id, idempotency_key, event and account identity, last status/outcome, summary, timestamp.

No second autonomous model is needed in Networking Radar.

## Browser credentials
OpenFill 1.49 includes a UI "Import Firefox" control. The person filters the account list, selects one entry, confirms, and OpenFill imports ONLY that saved credential to Windows Credential Manager. Firefox primary password, if configured, must be entered by the person in a local dialog.
The Luna tool use_imported_password can then fill a password field for the exact matching origin without returning the password to a model. Newly generated temporary passwords use the preexisting credential vault and temporary_credentials result field.

## Testing
OpenFill suite (pre-release 1.49): 529 passed, 0 failed. The NSS database initialization was tested with the installed Firefox profile; no actual user's password was decrypted without selection.
Actual third-party event registration and Firefox login end-to-end acceptance still require interactive tests.