$ErrorActionPreference='Continue'
$root='C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill'
$from=Join-Path $root 'staging\of_e03_act_locator_20261104'
$to=Join-Path $root 'staging\of_imp_20261031'
Write-Output ('TIME='+ (Get-Date -Format o))
Write-Output 'APP_PROCESSES'
Get-CimInstance Win32_Process -Filter "name='OpenFill.exe'" | Select-Object ProcessId,ExecutablePath,CommandLine | Format-List
Write-Output 'DEV_TASK'
Get-ScheduledTask -TaskName 'OpenFill-OFIMP-dev-20261031' | Select-Object TaskName,State | Format-List
foreach($p in @((Join-Path $from 'OpenFill.Core.dll'),(Join-Path $to 'OpenFill.Core.dll'),(Join-Path $from 'OpenFill.dll'),(Join-Path $to 'OpenFill.dll'))){
try{ $f=[IO.File]::Open($p,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::None);$f.Dispose();Write-Output ('EXCLUSIVE_OK '+$p) }
catch{Write-Output ('EXCLUSIVE_FAIL '+$p+' :: '+$_.Exception.GetType().Name+' :: '+$_.Exception.Message)}
}
