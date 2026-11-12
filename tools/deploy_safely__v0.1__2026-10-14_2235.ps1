# OpenFill - Metadata: wersja 0.1, data 2026-10-14 22:35
# OpenFill deployment helper - version 0.1, 2026-10-13 21:53
# 2026-11-12 15:08 CET | GPT-6: accept current MCP info schema and prevent nested rollback folders.
param(
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$true)][string]$ReleaseDirectory
)
$ErrorActionPreference = 'Stop'
$root = Join-Path $env:USERPROFILE 'OpenFill'
$app = Join-Path $root 'app'
$taskName = 'OpenFill'
$session = (Get-Process -Id $PID).SessionId
if ($session -le 0) { throw 'Do not deploy from Windows Session 0. Run only from an authorized interactive Windows user session. Session 0 is not supported.' }
if ($env:OPENFILL_MAINTENANCE_OK -ne '1') { throw 'Maintenance verification gate missing.' }
if (-not (Test-Path (Join-Path $ReleaseDirectory 'OpenFill.exe'))) { throw 'Release executable missing.' }
if (-not (Test-Path (Join-Path $ReleaseDirectory 'cli'))) { throw 'Release CLI folder missing.' }
$config = Get-Content -LiteralPath (Join-Path $root 'data\stable\config.json') -Raw | ConvertFrom-Json
$port = [int]$config.mcpPort
if ($port -le 0) { throw 'MCP port not configured.' }
$master = [uri]((Get-Content -LiteralPath (Join-Path $root 'data\stable\runtime\mcp-url.txt') -Raw).Trim())
$endpoint = 'http://127.0.0.1:' + $port + $master.AbsolutePath
function Invoke-OpenFill([string]$name) {
    $q = @{jsonrpc='2.0';id=1;method='tools/call';params=@{name=$name;arguments=@{}}} | ConvertTo-Json -Compress -Depth 7
    $res = Invoke-RestMethod -Uri $endpoint -Method Post -Body $q -ContentType 'application/json' -TimeoutSec 4
    if ($res.error) { throw 'MCP returned an RPC error.' }
    return ($res.result.content[0].text | ConvertFrom-Json)
}
$current = Invoke-OpenFill 'openfill_info'
$maintenance = Invoke-OpenFill 'openfill_maintenance'
if (-not $maintenance.active -or $maintenance.running_tasks -ne 0) { throw 'Deployment refused: maintenance is off or tasks remain active.' }
$foreign = @(Get-Process -Name OpenFill -ErrorAction SilentlyContinue | Where-Object { $_.SessionId -ne $session })
if ($foreign.Count -gt 0) { throw 'Deployment refused: OpenFill is also running outside the interactive session.' }
$timestamp = Get-Date -Format 'yyyy-MM-dd_HHmmss'
$diskVersion = (Get-Item (Join-Path $app 'OpenFill.exe')).VersionInfo.ProductVersion
$backupVersion = $diskVersion.Split('+')[0]
$old = Join-Path $root ('app__rollback__' + (Get-Date -Format 'yyyy-MM-dd_HH-mm-ss') + '_CET_GPT-6_OpenFill_v' + $backupVersion + '_BinariaPrzedAktualizacja_DlaAutomatycznegoCofniecia')
$failed = Join-Path $root ('app__failed__v' + $Version + '__' + $timestamp)
if (Test-Path $old) { throw 'Rollback destination already exists.' }
Write-Output ('DEPLOY_BEGIN v' + $Version)
Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
Get-Process -Name OpenFill -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
if (Get-Process -Name OpenFill -ErrorAction SilentlyContinue) {
    throw 'LIVE_PROCESS_STILL_RUNNING_AFTER_SCHEDULER_STOP'
}
try {
    Move-Item -LiteralPath $app -Destination $old
    Move-Item -LiteralPath $ReleaseDirectory -Destination $app
    Start-ScheduledTask -TaskName $taskName -ErrorAction Stop
    $new = $null
    for ($i=0; $i -lt 15; $i++) {
        Start-Sleep -Seconds 1
        $new = Get-Process -Name OpenFill -ErrorAction SilentlyContinue |
            Where-Object { $_.Path -eq (Join-Path $app 'OpenFill.exe') } | Select-Object -First 1
        if ($new) { break }
    }
    if (-not $new) { throw 'OPENFILL_AUTOSTART_DID_NOT_LAUNCH' }
    if ((Get-Process -Id $new.Id).SessionId -ne $session) { throw 'OpenFill started in the wrong Windows session.' }
    $healthy = $false
    for ($i=0; $i -lt 60; $i++) {
        Start-Sleep -Seconds 1
        try {
            $health = Invoke-OpenFill 'openfill_info'
            # The current openfill_info returns {working:[]} without status/app.version.
            # Successful MCP JSON-RPC plus a matching on-disk executable version is the compatibility gate.
            $liveVersion = (Get-Item (Join-Path $app 'OpenFill.exe')).VersionInfo.ProductVersion
            $matching = $liveVersion -eq $Version -or $liveVersion.StartsWith($Version + '+')
            if ($null -ne $health -and $matching) { $healthy = $true; break }
        } catch { }
    }
    if (-not $healthy) { throw 'MCP health check failed: expected version did not answer.' }
    $direct = Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:17873/health' -TimeoutSec 6
    if ($direct.StatusCode -ne 200) { throw 'DIRECT_HTTP_API_FAILED' }
    Write-Output ('DEPLOY_OK version=' + $Version + ' session=' + $session + ' mcp=ok')
} catch {
    Write-Output ('DEPLOY_FAILED ' + $_.Exception.Message)
    Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    Get-Process -Name OpenFill -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    if (Test-Path $app) {
        $parked = $false
        for ($attempt = 0; $attempt -lt 10; $attempt++) {
            try {
                Move-Item -LiteralPath $app -Destination $failed -ErrorAction Stop
                $parked = $true
                break
            } catch {
                Start-Sleep -Seconds 1
            }
        }
        if (-not $parked -or (Test-Path $app)) {
            throw 'ROLLBACK_BLOCKED: failed app is still in place; original is preserved in rollback directory.'
        }
    }
    if (Test-Path $old) {
        if (Test-Path $app) { throw 'ROLLBACK_REFUSED_NESTED_TARGET' }
        Move-Item -LiteralPath $old -Destination $app -ErrorAction Stop
        Start-ScheduledTask -TaskName $taskName -ErrorAction Stop
        Write-Output 'ROLLBACK_STARTED'
    }
    throw
}