param(
    [string]$PythonWExe = "",
    [string]$TaskName = "OpenFillTelegramBridge"
)
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$Supervisor = Join-Path $Root "bridge_supervisor.py"
if (!(Test-Path -LiteralPath $Supervisor)) { throw "Missing bridge_supervisor.py" }
if (!$PythonWExe) {
    $PythonWExe = (Get-Command pythonw.exe -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty Source)
}
if (!$PythonWExe -or !(Test-Path -LiteralPath $PythonWExe)) { throw "Python 3 pythonw.exe is required; supply -PythonWExe" }
$User = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$Action = New-ScheduledTaskAction -Execute $PythonWExe -Argument ('"' + $Supervisor + '"')
$Trigger = New-ScheduledTaskTrigger -AtLogOn -User $User
$Principal = New-ScheduledTaskPrincipal -UserId $User -LogonType Interactive -RunLevel LeastPrivilege
$Settings = New-ScheduledTaskSettingsSet -ExecutionTimeLimit (New-TimeSpan -Seconds 0) -MultipleInstances IgnoreNew -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1)
Register-ScheduledTask -TaskName $TaskName -Action $Action -Trigger $Trigger -Principal $Principal -Settings $Settings -Force | Out-Null
Start-ScheduledTask -TaskName $TaskName
Get-ScheduledTask -TaskName $TaskName | Select-Object TaskName, State
