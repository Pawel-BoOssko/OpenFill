# OpenFill Telegram bridge 1.62.1: mobile acceptance capture

Created 2026-11-12T04:35:34.614030 (Europe/Warsaw), author GPT-6.

## Reason
The previous acceptance challenge (bot message 57) was sent but unmatched replies
were not stored with their source message and answer, so later audit could not
deterministically prove the challenge was answered.

## Fix
- Added a durable phone_acceptance_tests SQLite table.
- Pairing identity, bot message ID, and challenge SHA256 all must match.
- Only an actual Telegram Reply to the bot's exact test message qualifies.
- Updates are idempotent; a duplicate update cannot overwrite the first answer.
- Capture metadata: update_id, reply_message_id, received_at. The challenge's
  plaintext answer is not persisted in the test table.
- The existing generic reply receipt is unchanged.
- No OpenFill.exe binary restart needed; only Python bridge restarted.
- Rollback source and deployment files retained on PBOX1.

## Results at deployment
- 29/29 Python unit tests passed, including 4 new acceptance tests.
- Live bridge restarted, supervisor and worker each running.
- OpenFill named pipe ready, paired Telegram account remains one.
- Acceptance test C3_2026-11-12_phone_57 was registered as pending.
- A reply is still required from the real phone; a Telegram update alone
  cannot reveal the client type and is not by itself proof of a physical phone.

## Product versions
OpenFill Windows binary remains v1.62. Only the standalone Telegram Python
bridge receives its independent QA patch version 1.62.1.
