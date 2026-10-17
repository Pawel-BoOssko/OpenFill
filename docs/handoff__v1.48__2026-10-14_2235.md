# OpenFill 1.48 – structured temporary credential handoff

## Metadata
- Document version: 1.48
- Date: 2026-10-14 22:35
Version: 1.48; date: 2026-10-13 21:40 Europe/Warsaw

## Flow
1. Luna calls save_temporary_password with service, login and a generated password.
2. The tool saves the value to Windows Credential Manager, verifies it, and records a task-scoped credential object with the save result.
3. At finish, RunConclusion carries this object independently from narrative summary.
4. McpTaskManager persists that object in task history and exposes temporary_credentials in task result and subsequent status lookups. Fields: service, login, password, temporary, vault_saved, change_immediately.
5. The calling model is instructed to pass the exact temporary password and immediate-change instruction to the user.
6. Password-field masking in page snapshots and log redaction stay unchanged.

## Verification
- Live 1.47 HTTP integration: PUT 201, GET 200, SHA256 matches both ways on 128 KiB, reused PUT URL returns 404, no MCP master secret in scoped URLs.
- MCP server advertises 8 tools including openfill_file_transfer.
- All 1.48 tests: 511 passed, 0 failed. Includes credential handoff persistence, Windows vault smoke, file transfer integration and regressions.
- Release package: C:\Users\pboru\OpenFill\release__v1.48__2026-10-13_2138
- Deployment script: tools\deploy__v1.48__2026-10-13_2139.ps1

## Caveats
- Temporary credentials in narrative task summaries and task JSON history are not encrypted at rest. Windows Credential Manager itself is protected.
- No actual registration on a third-party website was part of the tests.
- ChatGPT might cache earlier MCP tool definitions until the connection reloads.
- Do not interrupt active OpenFill jobs to deploy.