$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$src = Get-Content -LiteralPath (Join-Path $root 'src\OpenFill.Core\Mcp\McpTaskManager__v0.17__2026-10-14_2235.cs') -Raw
$test = Get-Content -LiteralPath (Join-Path $root 'tests\OpenFill.Tests\McpTests__v0.12__2026-10-14_2235.cs') -Raw
$checks = @(
    @('poll ceiling', $src.Contains('o["poll_wait_max_seconds"] = MaxWaitSeconds')),
    @('ETA unknown', $src.Contains('o["estimated_completion_seconds"] = null')),
    @('tool activity timestamp', $src.Contains('o["last_tool_activity_at"]')),
    @('long-poll wake version', $src.Contains('rec.Ver++')),
    @('test poll ceiling', $test.Contains('"poll ceiling"')),
    @('test no invented ETA', $test.Contains('"no invented ETA"')),
    @('test activity wake', $test.Contains('"progress woke waiting status"'))
)
$failed = 0
foreach ($check in $checks) {
    if ($check[1]) { Write-Output ('PASS: ' + $check[0]) }
    else { Write-Output ('FAIL: ' + $check[0]); $failed++ }
}
if ($failed -gt 0) { throw "$failed source-contract checks failed" }
Write-Output 'SOURCE CHECKS ONLY: executable regression not run'
