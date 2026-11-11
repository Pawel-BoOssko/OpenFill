# OpenFill 1.61, Telegram bridge integration

Created 2026-11-11 22:03:17 Europe/Warsaw. Author GPT-6.

## Added
1. MobileCardGateway and MobileCardPipeServer integration into AppHost (.NET 10).
2. Long-poll Telegram bridge, DPAPI helpers and supervisor.
3. Per-card replies and optional plain-message routing with exactly one active ask card.
4. Unit tests for duplicate updates, two-card order, expiring cards, callback tokens, outages, 429 retry and routing safeguards.
5. Gateway tests (15) and Python bridge tests (16).
6. Task Scheduler installation script and deployment instructions.

## Version
Directory.Build.props: 1.61 and dated build metadata.
OpenFill 1.60 was deployed earlier to PBOX1 on 2026-11-11.
Git source branch: feature/openfill-telegram-20261111, based on the existing self-chat feature.
Source commit does not imply that 1.61 OpenFill.exe is installed.

## Acceptance gaps
Confirm testing requires enabling OpenFill confirmation flow, local sound needs physical listening, code prompts and two-card replies need phone testing.
Unrelated project changes are preserved.
