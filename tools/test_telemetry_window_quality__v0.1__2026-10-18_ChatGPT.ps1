# Deterministic OF-032/033/034 fixture test. No production telemetry read.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Join-Path $PSScriptRoot 'telemetry_window_quality__v0.1__2026-10-18_ChatGPT.ps1'
$fixture=Join-Path $root 'tests\OpenFill.Tests\telemetry_quality_fixture__v0.1__2026-10-18.ndjson'
$data=(& $source -File $fixture -AsJson) | ConvertFrom-Json
$checks=@(
    @('six valid samples', ($data.validSamples -eq 6)),
    @('invalid JSON accounted for', ($data.invalidLines -eq 1)),
    @('one 90-second gap', ($data.gapsOverOnePointFiveIntervals -eq 1 -and $data.largestGapSeconds -eq 90)),
    @('process restart detected', ($data.rootPidChanges -eq 1 -and $data.rootStartChanges -eq 1)),
    @('two idle samples', ($data.groups.idle.samples -eq 2)),
    @('two active samples', ($data.groups.active.samples -eq 2)),
    @('two waiting samples', ($data.groups.waiting.samples -eq 2)),
    @('idle CPU average', ($data.groups.idle.cpuPctMachineAvg -eq 1.5)),
    @('active Private Bytes average', ($data.groups.active.privateBytesMbAvg -eq 225)),
    @('waiting CPU average', ($data.groups.waiting.cpuPctMachineAvg -eq 5.5)),
    @('continuity warning is present', ($data.warning -match 'mixed-uptime')),
    @('memory definition warns of summed shared pages', ($data.memoryDefinition -match 'shared'))
)
foreach($check in $checks) {
    if(-not $check[1]) { throw ('FAIL: ' + $check[0]) }
    Write-Output ('PASS: ' + $check[0])
}
Write-Output ('TOTAL: ' + $checks.Count + ' PASS, 0 FAIL')