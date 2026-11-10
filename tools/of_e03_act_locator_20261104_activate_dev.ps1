$ErrorActionPreference = 'Stop'
if ($env:COMPUTERNAME -ne 'PBOX1') { throw 'WRONG_HOST' }
$root = 'C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill'
$from = Join-Path $root 'staging\of_e03_act_locator_20261104'
$to = Join-Path $root 'staging\of_imp_20261031'
$backup = Join-Path $root 'staging\of_e03_act_locator_20261104_backup_pre_dev_activation'
$task = 'OpenFill-OFIMP-dev-20261031'
if (!(Test-Path (Join-Path $from 'OpenFill.Core.dll'))) { throw 'E03_PUBLISH_NOT_FOUND' }
Stop-ScheduledTask -TaskName $task -ErrorAction Stop
$oldDev = @(Get-CimInstance Win32_Process -Filter "name='OpenFill.exe'" | Where-Object { $_.ExecutablePath -ieq (Join-Path $to 'OpenFill.exe') })
foreach ($item in $oldDev) { Stop-Process -Id $item.ProcessId -Force -ErrorAction Stop }
Start-Sleep -Seconds 1
if (Test-Path $backup) { throw 'E03_DEV_BACKUP_ALREADY_EXISTS' }
Copy-Item -Path $to -Destination $backup -Recurse -Force -ErrorAction Stop
Write-Output "DEV_BACKUP_OK $backup"
& robocopy.exe $from $to /E /R:2 /W:1 /NFL /NDL /NJH /NJS /NP
if ($LASTEXITCODE -ge 8) { throw "DEV_COPY_FAILED_$LASTEXITCODE" }
Start-ScheduledTask -TaskName $task -ErrorAction Stop
Start-Sleep -Seconds 2
$want = (Get-FileHash (Join-Path $from 'OpenFill.Core.dll') -Algorithm SHA256).Hash
$actual = (Get-FileHash (Join-Path $to 'OpenFill.Core.dll') -Algorithm SHA256).Hash
if ($want -ne $actual) { throw 'E03_DEV_SHA_MISMATCH' }
$dev = @(Get-CimInstance Win32_Process -Filter "name='OpenFill.exe'" | Where-Object { $_.ExecutablePath -ieq (Join-Path $to 'OpenFill.exe') })
if ($dev.Count -ne 1) { throw "E03_DEV_PROCESS_COUNT_$($dev.Count)" }
Write-Output "DEV_RELEASE_OK sha256=$actual pid=$($dev[0].ProcessId)"
