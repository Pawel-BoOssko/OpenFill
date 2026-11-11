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

## Live Telegram acceptance, 2026-11-11 22:30 CET
- PBOX1 paired bot: @OpenFillTelefonBot. Outbound receipts sent by real Telegram Bot API and displayed in Telegram Web.
- Telegram update 966982738, source message 23: unassigned message -> durable receipt state=sent, returned as a reply to its source message. Unicode codepoints in database include U+015B and U+0107.
- Telegram update 966982739, source message 26, card q273a6997: OpenFill IPC action status=accepted; durable receipt status=sent.
- Telegram Web observed text: "OpenFill: wiadomosc odebrana i przyjeta do zadania." with actual Polish diacritics rendered correctly. Source contains Python Unicode escapes for Polish strings.
- OpenFill task OF-20261111-2229-7f3e returned OK_ACK162.
- 25 Python unit tests pass. No OpenFill.exe binary restart was needed; Python bridge was restarted via the scheduled task.
- PBOX1 Telegram supervisor/bridge after patch: 14120/19284.
- This proves both the accepted-answer path and the unassigned-message path in real Telegram. Physical sound and separate two-card/code/confirm acceptance are still outstanding from original T2C scope.
