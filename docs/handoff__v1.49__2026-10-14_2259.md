# OpenFill 1.50: Firefox picker UI integration

## Metadata
- Document version: 1.49
- Date: 2026-10-14 22:59
Version 1.0, 2026-10-14 04:17 Europe/Warsaw

## Implemented
- MainForm version 0.23 now has a visible Import Firefox button in the top navigation bar.
- Clicking it opens the existing FirefoxPasswordPicker, passing the active browser page URL as the preferred website filter.
- The existing one-entry selection and explicit human confirmation remain mandatory. Password content is not returned to the caller.
- Version metadata updated to 1.50 (2026-10-14 04:16).

## Verification
- Full OpenFill tests: 529 passed, 0 failed. Log: tools/testlog__v1.49.1__2026-10-14_0411.txt.
- WinForms App build: exit 0, 0 errors, 1 pre-existing WindowsBase reference warning, including after MainForm versioned rename.
- Release-mode application and CLI were published to C:\Users\pboru\OpenFill\releases\app__v1.50__2026-10-14_0416.
- Password import acceptance test with a real Firefox account awaits an explicit selection and confirmation by the person at PBOX1.

## Radar integration review
- OpenFill 1.49 MCP implements idempotency_key with conflict behavior and disk-based recovery; unit tests included in 529 passing tests.
- Networking Radar's current app/services/openfill_mcp.py start() does not pass idempotency_key to openfill_start_task.
- Radar holds local registration state in SQLite and protects a rapid double click using BEGIN IMMEDIATE. This does not protect against loss of a successful MCP start response unless the same stable key is passed in retry.
- Networking Radar repository currently has uncommitted parallel edits. No Radar source file was modified by this maintenance work.

## Deployment boundary
- Do not interrupt any active OpenFill task; use openfill_maintenance and tools/deploy_safely__v0.1__2026-10-13_2153.ps1 from interactive Windows session (not session 0).
- Verify live openfill_info version after deployment. Keep rollback.