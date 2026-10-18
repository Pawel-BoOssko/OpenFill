# OF-032/033/034: read-only analysis of OpenFill telemetry. Windows PowerShell 5.1.
param([string]$File = '', [int]$Samples = 120, [int]$ExpectedIntervalSeconds = 30, [switch]$AsJson)
$ErrorActionPreference = 'Stop'
if (-not $File) { $File = Join-Path $env:USERPROFILE ('OpenFill\data\stable\logs\telemetry\resources_' + (Get-Date -Format yyyyMMdd) + '.ndjson') }
if ($Samples -lt 2 -or $ExpectedIntervalSeconds -lt 1) { throw 'At least two samples and a positive interval are required' }
if (-not (Test-Path -LiteralPath $File -PathType Leaf)) { throw "Missing telemetry input: $File" }
$rows = New-Object System.Collections.ArrayList
$invalid = 0
foreach ($line in @(Get-Content -LiteralPath $File -Tail $Samples)) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }
    try {
        $v = $line | ConvertFrom-Json
        if ($null -eq $v.tsUtc -or $null -eq $v.rootPid -or $null -eq $v.cpuPctMachine) { $invalid++; continue }
        $stamp = ([datetime]$v.tsUtc).ToUniversalTime()
        [void]$rows.Add([pscustomobject]@{ Stamp = $stamp; Row = $v })
    } catch { $invalid++ }
}
if ($rows.Count -lt 2) { throw 'Fewer than two valid samples' }
$nonMonotonic = 0
for ($i = 1; $i -lt $rows.Count; $i++) { if ($rows[$i].Stamp -le $rows[$i-1].Stamp) { $nonMonotonic++ } }
$sorted = @($rows | Sort-Object Stamp)
$gaps = 0; $maxGap = 0.0; $pidChanges = 0; $startChanges = 0
for ($i=1; $i -lt $sorted.Count; $i++) {
    $dt = ($sorted[$i].Stamp - $sorted[$i-1].Stamp).TotalSeconds
    if ($dt -gt 1.5 * $ExpectedIntervalSeconds) { $gaps++ }
    if ($dt -gt $maxGap) { $maxGap = $dt }
    if ($sorted[$i].Row.rootPid -ne $sorted[$i-1].Row.rootPid) { $pidChanges++ }
    if ($sorted[$i].Row.rootStartedUtc -ne $sorted[$i-1].Row.rootStartedUtc) { $startChanges++ }
}
$groups = [ordered]@{}
foreach ($name in @('idle','active','waiting')) {
    $items = @($sorted | Where-Object {
        $v = $_.Row
        if ($name -eq 'active') { [int]$v.activeTasks -gt 0 }
        elseif ($name -eq 'waiting') { [int]$v.activeTasks -eq 0 -and [int]$v.waitingTasks -gt 0 }
        else { [int]$v.activeTasks -eq 0 -and [int]$v.waitingTasks -eq 0 }
    } | ForEach-Object { $_.Row })
    if ($items.Count -eq 0) { $groups[$name] = @{ samples=0 }; continue }
    $groups[$name] = [ordered]@{
        samples=$items.Count
        cpuPctMachineAvg=[math]::Round([double](($items | Measure-Object cpuPctMachine -Average).Average),3)
        workingSetMbAvg=[math]::Round([double](($items | Measure-Object workingSetMb -Average).Average),2)
        privateBytesMbAvg=[math]::Round([double](($items | Measure-Object privateBytesMb -Average).Average),2)
        tabsLast=[int]$items[-1].tabs
        webViewCountLast=[int]$items[-1].webViewProcessCount
    }
}
$elapsed = ($sorted[-1].Stamp - $sorted[0].Stamp).TotalSeconds
$warning = if ($invalid -gt 0 -or $gaps -gt 0 -or $pidChanges -gt 0 -or $startChanges -gt 0 -or $nonMonotonic -gt 0) { 'Incomplete or mixed-uptime telemetry; not a continuous workload benchmark.' } else { 'No continuity issue detected.' }
$result = [ordered]@{
    file=[IO.Path]::GetFileName($File); validSamples=$sorted.Count; invalidLines=$invalid
    firstUtc=$sorted[0].Stamp.ToString('o'); lastUtc=$sorted[-1].Stamp.ToString('o')
    durationMinutes=[math]::Round($elapsed/60,2); expectedIntervalSeconds=$ExpectedIntervalSeconds
    estimatedSamplesAtCadence=[math]::Floor($elapsed / $ExpectedIntervalSeconds) + 1
    gapsOverOnePointFiveIntervals=$gaps; largestGapSeconds=[math]::Round($maxGap,2)
    nonMonotonicTransitions=$nonMonotonic; rootPidChanges=$pidChanges; rootStartChanges=$startChanges
    rootPids=@($sorted | ForEach-Object { [int]$_.Row.rootPid } | Select-Object -Unique)
    groups=$groups; warning=$warning
    memoryDefinition='WorkingSetMb sums per-process working sets and may count shared pages twice; PrivateBytesMb is private committed memory, not physical RAM; managed heap excludes WebView2.'
    cpuDefinition='cpuPctMachine is percentage of total logical CPU capacity; cpuPctCoreSum uses 100 per fully occupied core.'
}
if ($AsJson) { ConvertTo-Json -InputObject $result -Depth 12 }
else {
    Write-Output ('Telemetry UTC: {0} to {1}; samples={2}, invalid={3}' -f $result.firstUtc,$result.lastUtc,$result.validSamples,$invalid)
    Write-Output ('Duration={0}min gaps={1} maxGap={2}s pidChanges={3} startChanges={4} nonMonotonic={5}' -f $result.durationMinutes,$gaps,$result.largestGapSeconds,$pidChanges,$startChanges,$nonMonotonic)
    foreach ($name in @('idle','active','waiting')) {
        $x=$groups[$name]
        if ($x.samples -eq 0) { Write-Output ('{0}: no samples' -f $name) }
        else { Write-Output ('{0}: n={1} CPU_machine={2}% WS_sum={3}MiB PrivateBytes={4}MiB tabs={5} WebView2={6}' -f $name,$x.samples,$x.cpuPctMachineAvg,$x.workingSetMbAvg,$x.privateBytesMbAvg,$x.tabsLast,$x.webViewCountLast) }
    }
    Write-Output $warning
    Write-Output $result.memoryDefinition
}