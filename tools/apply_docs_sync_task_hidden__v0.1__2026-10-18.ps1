# OpenFill - Metadata: wersja 0.1, data 2026-10-18 14:42
# Execute manually from an elevated PowerShell after reviewing the plan.
$ErrorActionPreference = 'Stop'
$name = 'OpenFill_Docs_GDrive_Sync'
$launcher = Join-Path $PSScriptRoot 'run_openfill_docs_sync__v0.1__2026-10-18_1440.ps1'
if (-not (Test-Path -LiteralPath $launcher -PathType Leaf)) { throw 'Launcher missing; no task changed.' }
$task = Get-ScheduledTask -TaskName $name -ErrorAction Stop
if (@($task.Actions).Count -ne 1) { throw 'Unexpected task action count; no task changed.' }
if ($task.Actions[0].Execute -notmatch '(?i)powershell') { throw 'Unexpected scheduled executable; no task changed.' }
$backupDir = Join-Path $env:USERPROFILE 'Documents\OpenFill-Backups\Diagnostics'
[IO.Directory]::CreateDirectory($backupDir) | Out-Null
$backupPath = Join-Path $backupDir ('OpenFill_Docs_GDrive_Sync_before_hidden_' + (Get-Date -Format yyyyMMdd_HHmmss) + '.xml')
Export-ScheduledTask -TaskName $name | Set-Content -LiteralPath $backupPath -Encoding UTF8
$arguments = '-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File "' + $launcher + '"'
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $arguments
Set-ScheduledTask -TaskName $name -Action $action -ErrorAction Stop | Out-Null
$verify = Get-ScheduledTask -TaskName $name
if ($verify.Actions[0].Arguments -ne $arguments) { throw 'Task action did not match requested command.' }
Write-Output ('TASK_UPDATED ' + $name)
Write-Output ('ACTION ' + $verify.Actions[0].Arguments)
Write-Output ('BACKUP ' + $backupPath)
Write-Output ('LOG %LOCALAPPDATA%\OpenFillDocsSync\docs-sync-YYYY-MM-DD.log')
