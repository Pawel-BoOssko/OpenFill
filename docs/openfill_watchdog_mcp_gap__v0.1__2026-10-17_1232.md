# OF-239: watchdog MCP gap, 2026-10-17
State: ANALYZED, not BUILT or DEPLOYED.
Source inspected: tools/watchdog-openfill__v1.0__2026-10-07_1432.ps1.
Existing script checks process/TCP and starts the OpenFill scheduled task; it does not bind the MCP endpoint, accept JSON-RPC or return maintenance.
It does not inspect the approved maintenance state before deciding to restart OpenFill. A replacement listener must coordinate ownership of the same port with the main process.
Approved behavior: while OpenFill is stopped for update, openfill_start_task returns maintenance without creating a task; openfill_task_status returns maintenance with remaining minutes and the same task_id after up to 30 seconds.
The replacement must stop accepting requests when the new main MCP listener becomes available. After restart, the main server returns interrupted and continue_task_id from persisted records.
No active deployment, restart or port binding was attempted. Production 1.52 remains unchanged.
Release gate: source implementation, isolated protocol tests, handoff without port collision, SessionId1 startup, real MCP smoke and rollback all remain required.
