$ErrorActionPreference='Stop'
$root='C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill'
$from=Join-Path $root 'staging\of_e03_act_locator_20261104'
$to=Join-Path $root 'staging\of_imp_20261031'
$backup=Join-Path $root 'staging\of_e03_act_locator_20261104_backup_pre_dev_activation'
$task='OpenFill-OFIMP-dev-20261031'
if(!(Test-Path $backup)){throw 'E03_DEV_BACKUP_NOT_FOUND'}
if(!(Test-Path (Join-Path $from 'OpenFill.Core.dll'))){throw 'E03_STAGING_MISSING'}
Stop-ScheduledTask -TaskName $task -ErrorAction Stop
$old=@(Get-CimInstance Win32_Process -Filter "name='OpenFill.exe'" | Where-Object {$_.ExecutablePath -ieq (Join-Path $to 'OpenFill.exe')})
foreach($p in $old){Stop-Process -Id $p.ProcessId -Force -ErrorAction Stop}
Start-Sleep -Seconds 2
Write-Output ('E03_RETRY_EXISTING_BACKUP '+$backup)
& robocopy.exe $from $to /E /R:2 /W:2 /NFL /NDL /NJH /NJS /NP
if($LASTEXITCODE -ge 8){throw "E03_DEV_COPY_FAILED_$LASTEXITCODE"}
$srcsha=(Get-FileHash (Join-Path $from 'OpenFill.Core.dll') -Algorithm SHA256).Hash
$dstsha=(Get-FileHash (Join-Path $to 'OpenFill.Core.dll') -Algorithm SHA256).Hash
if($srcsha -ne $dstsha){throw 'E03_DEV_CORE_SHA_MISMATCH'}
Start-ScheduledTask -TaskName $task -ErrorAction Stop
Start-Sleep -Seconds 3
$dev=@(Get-CimInstance Win32_Process -Filter "name='OpenFill.exe'" | Where-Object {$_.ExecutablePath -ieq (Join-Path $to 'OpenFill.exe')})
if($dev.Count -ne 1){throw "E03_DEV_PID_COUNT_$($dev.Count)"}
Write-Output "E03_DEV_OK pid=$($dev[0].ProcessId) core_sha=$dstsha backup=$backup"
