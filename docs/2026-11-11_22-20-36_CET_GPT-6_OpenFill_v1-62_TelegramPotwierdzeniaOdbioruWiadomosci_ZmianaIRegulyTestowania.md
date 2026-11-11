# OpenFill 1.62: receipts from Telegram

Created 2026-11-11 22:20:36 CET. Author GPT-6.

## User request
Send a Telegram acknowledgment that the OpenFill bot has received the user's message.

## Implementation
- Persistent SQLite receipts table, update-ID deduplication.
- Acknowledgment is a reply to the user's originating message.
- Positive acknowledgment only after OpenFill IPC accepts the answer.
- For unassigned text, acknowledge receipt but explicitly state no task assignment.
- For rejected actions, report rejection rather than acceptance.
- No false-positive acknowledgment while IPC is unavailable.
- On Telegram 429, retry after retry_after; on unknown send outcome, mark uncertain to avoid duplicate messages.
- Confirm callbacks are not implicitly approved by arbitrary text.

## Existing product
OpenFill v1.61 source already committed at c177b15af24e182d1d49da355bd9a8e670cdb931.
Python bridge running on PBOX1 is upgraded independently from OpenFill.exe.
The actual OpenFill.exe still reports 1.60 as of this source preparation.

## Tests
New tests cover accepted/rejected/unassigned receipt, routing ambiguity,
deduplication, IPC outage, rate limiting, uncertain delivery and unauthorized senders.
