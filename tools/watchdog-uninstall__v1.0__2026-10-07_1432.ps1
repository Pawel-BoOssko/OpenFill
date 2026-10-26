# OpenFill watchdog uninstaller - Metadata: wersja 1.0, data 2026-10-07 14:32
# Removes the scheduled task "OpenFill-watchdog" (and the copy of the script). Does not touch OpenFill itself.
$ErrorActionPreference = 'Continue'
& schtasks.exe /delete /tn OpenFill-watchdog /f 2>$null | Out-Null
Remove-Item (Join-Path $env:USERPROFILE 'OpenFill\watchdog') -Recurse -Force -ErrorAction SilentlyContinue
Write-Host 'Watchdog removed.'
