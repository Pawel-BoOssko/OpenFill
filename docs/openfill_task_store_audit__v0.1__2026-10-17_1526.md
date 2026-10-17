# OF-212/218/219: production task store read-only audit
Date: 2026-10-17 15:23 CEST; PBOX1, stable tasks directory.
719 task JSON files; all 719 parsed; 719 unique IDs.
Statuses: done 608; cancelled 44; failed 30; running 19; stopped_by_user 17; blocked 1.
All 19 persisted running records have UpdatedUtc older than 24 hours; oldest 2026-10-05T18:42:21Z.
Telemetry at the same time reports 0 active tasks. These 19 records are stale candidates; not proof that a live task is currently running.
67 records have idempotency_key; zero duplicate requester/key pairs found in this static dataset.
No records modified, no restart, no recovery executed. Runtime 1.52 is older than SOURCE recovery logic.
New test after build: simulate crash with running/needs_input/waiting_for_user and ensure readback interrupted/partial, preserving original task ID and scoped idempotency.
TEST: read-only JSON parsing and aggregation passed; source regression/build not run.
