$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$src=Get-Content -LiteralPath (Join-Path $root 'src\OpenFill.Core\Mcp\McpTaskManager__v0.17__2026-10-14_2235.cs') -Raw
$activity=[regex]::Match($src,'(?s)private void OnActivity\(.*?\n    private void Complete\(').Value
if(-not $activity){throw 'OnActivity method missing'}
if(-not $activity.Contains('rec.ActivityVer++')){throw 'Activity does not increment ActivityVer'}
if($activity.Contains('rec.Ver++')){throw 'Activity incorrectly changes state version'}
if(-not $src.Contains('await WaitAsync(rec, v, waitSeconds, ct, av)')){throw 'Status does not observe ActivityVer'}
if(-not $src.Contains('await WaitAsync(recNew, ver, waitSeconds, ct)')){throw 'Start state-only wait missing'}
if(-not $src.Contains('o["poll_wait_max_seconds"] = MaxWaitSeconds')){throw 'Polling bound missing'}
if(-not $src.Contains('o["estimated_completion_seconds"] = null')){throw 'Unknown ETA not explicit'}
Write-Output 'PASS: ActivityVer-only activity; status wakes on activity; start waits for state; poll ceiling distinct from ETA'
Write-Output 'SOURCE CONTRACT TEST ONLY; no executable build or live deployment'
