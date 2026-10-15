# OpenFill watchdog - Metadata: wersja 1.0, data 2026-10-07 14:32
# One check, meant to be run every few minutes by a scheduled task (see watchdog-install__*.ps1).
# If OpenFill is not running (no process, or - with MCP on - its local port does not answer) it is started
# through the scheduled task "OpenFill" (the same way the installer starts it, independent of any session).
# Safety: never restarts while an installer runs, while the app started less than 3 minutes ago, or more than
# 5 times in an hour (then it only logs). Log: <data>\logs\watchdog.log
param([string]$Root = (Join-Path $env:USERPROFILE 'OpenFill'))
$ErrorActionPreference = 'Stop'
$inst = Join-Path $Root 'data\stable'
$logDir = Join-Path $inst 'logs'
$log = Join-Path $logDir 'watchdog.log'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
function Log($m) { Add-Content -Path $log -Value ((Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + '  ' + $m) -Encoding UTF8 }

try {
    # an installer is running: leave everything alone
    $installer = Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -match 'install__|install-tester__' }
    if ($installer) { exit 0 }

    $proc = Get-Process -Name 'OpenFill' -ErrorAction SilentlyContinue | Sort-Object StartTime | Select-Object -Last 1
    $reason = $null
    if (-not $proc) {
        $reason = 'no OpenFill process'
    } else {
        # with MCP on, the local port must answer too
        $cfgPath = Join-Path $inst 'config.json'
        $mcpOn = $false; $port = 0
        if (Test-Path $cfgPath) {
            $cfg = Get-Content $cfgPath -Raw | ConvertFrom-Json
            $mcpOn = [bool]$cfg.mcpEnabled
            if ($cfg.mcpPort) { $port = [int]$cfg.mcpPort }
        }
        if ($mcpOn -and $port -gt 0) {
            $tcp = New-Object Net.Sockets.TcpClient
            try {
                $ok = $tcp.ConnectAsync('127.0.0.1', $port).Wait(3000) -and $tcp.Connected
            } catch { $ok = $false } finally { $tcp.Dispose() }
            if (-not $ok -and ((Get-Date) - $proc.StartTime).TotalMinutes -ge 3) { $reason = "port $port does not answer" }
        }
    }
    if (-not $reason) { exit 0 }
    if ($proc -and ((Get-Date) - $proc.StartTime).TotalMinutes -lt 3) { exit 0 }

    # restart budget: at most 5 per hour
    $stateFile = Join-Path $inst 'runtime\watchdog-restarts.txt'
    New-Item -ItemType Directory -Force -Path (Split-Path $stateFile) | Out-Null
    $recent = @()
    if (Test-Path $stateFile) { $recent = @(Get-Content $stateFile | ForEach-Object { [datetime]$_ } | Where-Object { $_ -gt (Get-Date).AddHours(-1) }) }
    if ($recent.Count -ge 5) { Log "RESTART SKIPPED ($reason): 5 restarts in the last hour, something is wrong"; exit 0 }

    if ($proc) { Log "stopping stuck process $($proc.Id) ($reason)"; Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue; Start-Sleep -Seconds 3 }
    Log "starting OpenFill ($reason)"
    & schtasks.exe /run /tn OpenFill | Out-Null
    ($recent + (Get-Date)) | ForEach-Object { $_.ToString('o') } | Set-Content $stateFile
} catch {
    Log ('watchdog error: ' + $_.Exception.Message)
}
