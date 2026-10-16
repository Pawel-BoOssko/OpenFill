# OpenFill watchdog installer - Metadata: wersja 1.0, data 2026-10-07 14:32
# Registers the scheduled task "OpenFill-watchdog": runs watchdog-openfill__*.ps1 every 3 minutes as the current user.
# NOT run automatically. Run it only when you decide where OpenFill is going to live.
# Remove with: schtasks /delete /tn OpenFill-watchdog /f   (or watchdog-uninstall__*.ps1)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$wd = Get-ChildItem $here -Filter 'watchdog-openfill__*.ps1' | Sort-Object Name | Select-Object -Last 1
if (-not $wd) { throw 'watchdog-openfill__*.ps1 not found next to this script.' }
# keep a stable copy outside the repository so a repository clean-up cannot break the task
$dir = Join-Path $env:USERPROFILE 'OpenFill\watchdog'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$copy = Join-Path $dir 'watchdog-openfill.ps1'
Copy-Item $wd.FullName $copy -Force
$arg = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + $copy + '"'
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $arg
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes 3)
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Minutes 2)
Register-ScheduledTask -TaskName 'OpenFill-watchdog' -Action $action -Trigger $trigger -Settings $settings -Description 'OpenFill watchdog (checks every 3 minutes, restarts OpenFill when it is not running)' -Force | Out-Null
Write-Host 'Task OpenFill-watchdog registered (every 3 minutes). Log: data\stable\logs\watchdog.log'
