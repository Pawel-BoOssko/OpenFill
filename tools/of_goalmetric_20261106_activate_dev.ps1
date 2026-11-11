$ErrorActionPreference = 'Stop'
if ($env:COMPUTERNAME -ne 'PBOX1') { throw 'WRONG_HOST' }
$root = 'C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill'
$from = Join-Path $root 'staging\of_goalmetric_20261106'
$to = Join-Path $root 'staging\of_imp_20261031'
$backup = Join-Path $root 'staging\of_goalmetric_20261106_backup_pre_dev_activation'
$task = 'OpenFill-OFIMP-dev-20261031'
if (!(Test-Path (Join-Path $from 'OpenFill.Core.dll'))) { throw 'FINAL_E02_D04_20261104_PUBLISH_NOT_FOUND' }
Stop-ScheduledTask -TaskName $task -ErrorAction Stop
# OF-000053: the scheduled task may stop while its old DEV process still locks DLLs.
$oldDev = @(Get-CimInstance Win32_Process -Filter "name='OpenFill.exe'" | Where-Object { $_.ExecutablePath -ieq (Join-Path $to 'OpenFill.exe') })
foreach ($item in $oldDev) { Stop-Process -Id $item.ProcessId -Force -ErrorAction Stop }
Start-Sleep -Seconds 1
Copy-Item -Path $to -Destination $backup -Recurse -Force -ErrorAction Stop
Write-Output "DEV_BACKUP_OK $backup"
& robocopy.exe $from $to /E /R:2 /W:1 /NFL /NDL /NJH /NJS /NP
if ($LASTEXITCODE -ge 8) { throw "DEV_COPY_FAILED_$LASTEXITCODE" }
Start-ScheduledTask -TaskName $task -ErrorAction Stop
Start-Sleep -Seconds 2
$want = (Get-FileHash (Join-Path $from 'OpenFill.Core.dll') -Algorithm SHA256).Hash
$actual = (Get-FileHash (Join-Path $to 'OpenFill.Core.dll') -Algorithm SHA256).Hash
if ($want -ne $actual) { throw 'DEV_SHA_MISMATCH' }
$devProcesses = @(Get-CimInstance Win32_Process -Filter "name='OpenFill.exe'" | Where-Object { $_.ExecutablePath -ieq (Join-Path $to 'OpenFill.exe') })
Write-Output "DEV_RELEASE_OK sha256=$actual pid=$($devProcesses[0].ProcessId)"
if ($devProcesses.Count -eq 0) { throw 'DEV_PROCESS_NOT_RUNNING' }
exit 0






