# OpenFill 1.50 deployment record

## Metadata
- Document version: 1.50
- Date: 2026-10-14 22:35
Version 1.0; 2026-10-14 04:25 Europe/Warsaw

## Change
- Source: src/OpenFill.App/MainForm__v0.23__2026-10-14_0414.cs
- Top navigation button Import Firefox is connected to FirefoxPasswordPicker, passing the active browser URL as its initial filter.
- Picker still requires person-selected entry and explicit confirmation for one saved credential. No real password was imported during this deployment.

## Quality gate
- Full OpenFill test suite: 529 passed, 0 failed (tools/testlog__v1.49.1__2026-10-14_0411.txt).
- Application compilation: success, zero errors, one pre-existing WindowsBase assembly warning.
- Release-mode app and CLI published for version 1.50.

## Safe deployment
- New-task maintenance mode was activated; a task that started concurrently was allowed to finish normally.
- Running task count confirmed to be 0 immediately before deployment.
- Deployment executed in interactive Windows session 1 using tools/deploy_safely__v0.1__2026-10-13_2153.ps1.
- Deployment reported DEPLOY_OK version=1.50 session=1 mcp=ok.
- Independent live openfill_info confirmed version 1.50, version date 2026-10-14 04:16, start time 2026-10-14 04:22:10, 0 active tasks.
- Rollback: C:\Users\pboru\OpenFill\app__rollback__v1.49__2026-10-14_042208.

## Remaining acceptance
- A person at PBOX1 must choose a Firefox entry and confirm the actual password import in the OpenFill dialog. No assistant may silently choose a password or bypass the confirmation.