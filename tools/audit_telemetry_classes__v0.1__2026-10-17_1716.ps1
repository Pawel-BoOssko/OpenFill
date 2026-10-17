param([int]$Samples = 120)
$ErrorActionPreference = 'Stop'
$path = Join-Path $env:USERPROFILE ('OpenFill\data\stable\logs\telemetry\resources_' + (Get-Date -Format 'yyyyMMdd') + '.ndjson')
$rows = @(Get-Content -LiteralPath $path -Tail $Samples | ForEach-Object { $_ | ConvertFrom-Json })
if ($rows.Count -lt 2) { throw 'Insufficient samples' }
$idle = @($rows | Where-Object { $_.activeTasks -eq 0 -and $_.waitingTasks -eq 0 }).Count
$active = @($rows | Where-Object { $_.activeTasks -gt 0 }).Count
$waiting = @($rows | Where-Object { $_.waitingTasks -gt 0 }).Count
$span = ([datetime]$rows[-1].tsUtc - [datetime]$rows[0].tsUtc).TotalMinutes
$gaps = @(1..($rows.Count - 1) | ForEach-Object { ([datetime]$rows[$_].tsUtc - [datetime]$rows[($_ - 1)].tsUtc).TotalSeconds })
$big = @($gaps | Where-Object { $_ -gt 45 }).Count
$pids = @($rows | Select-Object -ExpandProperty rootPid -Unique)
Write-Output ('Samples={0}; ActualMinutes={1:N2}' -f $rows.Count,$span)
Write-Output ('Idle={0}; Active={1}; Waiting={2}; GapsOver45s={3}' -f $idle,$active,$waiting,$big)
Write-Output ('DistinctRootPIDs={0}; LastTabs={1}; LastWebView2={2}' -f $pids.Count,$rows[-1].tabs,$rows[-1].webViewProcessCount)
