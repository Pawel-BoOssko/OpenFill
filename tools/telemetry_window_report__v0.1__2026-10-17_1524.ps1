param([int]$Samples = 120, [string]$File = '')
$ErrorActionPreference = 'Stop'
if (-not $File) {
    $File = Join-Path $env:USERPROFILE ('OpenFill\data\stable\logs\telemetry\resources_' + (Get-Date -Format yyyyMMdd) + '.ndjson')
}
if (-not (Test-Path -LiteralPath $File)) { throw "Telemetry file unavailable" }
$rows = @(Get-Content -LiteralPath $File -Tail $Samples | Where-Object { $_.Trim() } | ForEach-Object { $_ | ConvertFrom-Json })
if ($rows.Count -lt 2) { throw "At least two samples required" }
$gaps = @(1..($rows.Count-1) | ForEach-Object { ([datetime]$rows[$_].tsUtc - [datetime]$rows[($_-1)].tsUtc).TotalSeconds })
$avgCpu = ($rows | Measure-Object cpuPctMachine -Average).Average
$avgWs = ($rows | Measure-Object workingSetMb -Average).Average
$avgCommit = ($rows | Measure-Object privateBytesMb -Average).Average
$uniquePids = @($rows | Select-Object -ExpandProperty rootPid -Unique)
$active = @($rows | Where-Object { $_.activeTasks -gt 0 }).Count
$waiting = @($rows | Where-Object { $_.waitingTasks -gt 0 }).Count
Write-Output ('Samples: {0}; UTC window: {1} to {2}' -f $rows.Count,$rows[0].tsUtc,$rows[-1].tsUtc)
Write-Output ('Root PIDs: {0}; CPU total-machine avg: {1:N3}%; Working Set avg: {2:N1} MiB; Private Bytes avg: {3:N1} MiB' -f ($uniquePids -join ','),$avgCpu,$avgWs,$avgCommit)
Write-Output ('Active samples: {0}; waiting samples: {1}; gaps >45s: {2}; maximum gap: {3:N2}s' -f $active,$waiting,@($gaps | Where-Object { $_ -gt 45 }).Count,(($gaps | Measure-Object -Maximum).Maximum))
Write-Output ('Last tabs: {0}; last WebView2 processes: {1}; last handles: {2}; last threads: {3}' -f $rows[-1].tabs,$rows[-1].webViewProcessCount,$rows[-1].handles,$rows[-1].threads)
