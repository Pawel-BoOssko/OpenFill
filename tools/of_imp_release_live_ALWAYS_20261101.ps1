param([switch]$CheckOnly)
# Nadrzedna zasada: nowa gotowa wersja ma pierwszenstwo przed aktywnymi zadaniami.
# Nie sprawdzamy working=[] jako warunku aktualizacji.
$ErrorActionPreference = 'Stop'
if ($env:COMPUTERNAME -ne 'PBOX1') { throw 'WRONG_HOST' }
$src = 'C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\staging\of_imp_20261031'
$dst = 'C:\Users\pboru\OpenFill\app'
$taskName = 'OpenFill-open'
$srcCore = Join-Path $src 'OpenFill.Core.dll'
$dstCore = Join-Path $dst 'OpenFill.Core.dll'
$srcExe = Join-Path $src 'OpenFill.exe'
$dstExe = Join-Path $dst 'OpenFill.exe'
if (!(Test-Path $srcCore) -or !(Test-Path $srcExe)) { throw 'STAGING_BINARIES_MISSING' }
if (!(Test-Path $dstExe)) { throw 'LIVE_BINARIES_MISSING' }
$wantCore = (Get-FileHash $srcCore -Algorithm SHA256).Hash
$wantExe = (Get-FileHash $srcExe -Algorithm SHA256).Hash
$haveCore = (Get-FileHash $dstCore -Algorithm SHA256).Hash
$haveExe = (Get-FileHash $dstExe -Algorithm SHA256).Hash
if ($haveCore -eq $wantCore -and $haveExe -eq $wantExe) {
    Write-Output "LIVE_ALREADY_CURRENT sha256=$wantCore"
    exit 0
}
$endpoint = $null
try {
    $secret = [IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\stable\runtime\mcp-secret.txt').Trim()
    $endpoint = 'http://127.0.0.1:48731/mcp/' + $secret
} catch {
    Write-Output 'MCP_SECRET_UNAVAILABLE_CONTINUE'
}
if ($CheckOnly) {
    Write-Output "RELEASE_READY_NO_IDLE_GATE source_sha256=$wantCore"
    exit 0
}
$backup = Join-Path 'C:\Users\pboru\OpenFill' ('app__rollback__before_release_' + (Get-Date -Format 'yyyyMMdd_HHmmss'))
$backupReady = $false
try {
    Copy-Item $dst $backup -Recurse -Force -ErrorAction Stop
    $backupReady = $true
    Write-Output "BACKUP_OK $backup"
} catch {
    Write-Output "BACKUP_UNAVAILABLE_CONTINUE $($_.Exception.Message)"
}
if ($endpoint) {
    try {
        $body = @{
            jsonrpc = '2.0'; id = 20261101; method = 'tools/call'
            params = @{ name = 'openfill_maintenance'; arguments = @{
                minutes = 5; reason = 'Teraz nastąpi aktualizacja OpenFill. Wróć za kilka minut.'
                requester = 'ChatGPT'
            }}
        } | ConvertTo-Json -Compress -Depth 8
        $response = Invoke-RestMethod -Uri $endpoint -Method Post -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json; charset=utf-8' -TimeoutSec 4
        if ($response.error) { throw 'MCP_MAINTENANCE_ERROR' }
        Write-Output 'UPDATE_NOTICE_SENT'
    } catch {
        Write-Output "UPDATE_NOTICE_UNAVAILABLE_CONTINUE $($_.Exception.Message)"
    }
}
function Get-LiveProcesses {
    @(Get-CimInstance Win32_Process -Filter "name='OpenFill.exe'" |
        Where-Object { $_.ExecutablePath -ieq $dstExe })
}
Write-Output 'LIVE_RELEASE_START_IGNORE_ACTIVE_TASKS'
foreach ($live in @(Get-LiveProcesses)) {
    try {
        $process = Get-Process -Id $live.ProcessId -ErrorAction Stop
        if ($process.MainWindowHandle -ne 0) {
            [void]$process.CloseMainWindow()
        }
    } catch {
        Write-Output "GRACEFUL_CLOSE_UNAVAILABLE_CONTINUE pid=$($live.ProcessId)"
    }
}
# Tylko krótka szansa dla istniejącego PrepareShutdown, nigdy czekanie na zadania.
Start-Sleep -Seconds 2
function Copy-ReleaseFiles([string] $from) {
    # Robocopy skips unchanged dependencies, unlike Copy-Item -Force.
    # Skip the native SQLite DLL specifically when it is byte-identical:
    # Windows keeps it mapped even during a short process restart.
    $sourceDll = Join-Path $from 'runtimes\win-x64\native\e_sqlite3.dll'
    $liveDll = Join-Path $dst 'runtimes\win-x64\native\e_sqlite3.dll'
    $extra = @()
    if ((Test-Path $sourceDll) -and (Test-Path $liveDll)) {
        if ((Get-FileHash $sourceDll -Algorithm SHA256).Hash -eq
            (Get-FileHash $liveDll -Algorithm SHA256).Hash) {
            $extra = @('/XF', 'e_sqlite3.dll')
            Write-Output 'UNCHANGED_SQLITE_DLL_SKIPPED'
        }
    }
    & robocopy.exe $from $dst /E /R:2 /W:1 /NFL /NDL /NJH /NJS /NP @extra
    $code = $LASTEXITCODE
    if ($code -ge 8) { throw "ROBOCOPY_FAILED_EXIT_$code" }
}
$switched = $false
try {
    # Disable triggers while replacing files: another scheduled launch can race the kill.
    Disable-ScheduledTask -TaskName $taskName -ErrorAction Stop | Out-Null
    Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    # Confirm task shutdown has released LIVE before copying; no waiting for idle workloads.
    for ($i = 0; $i -lt 40; $i++) {
        foreach ($live in @(Get-LiveProcesses)) {
            try { Stop-Process -Id $live.ProcessId -Force -ErrorAction Stop }
            catch { Write-Output "LIVE_FORCE_STOP_ERROR pid=$($live.ProcessId) reason=$($_.Exception.Message)" }
        }
        Start-Sleep -Milliseconds 250
        if (@(Get-LiveProcesses).Count -eq 0) { break }
    }
    if (@(Get-LiveProcesses).Count -gt 0) { throw 'LIVE_PROCESS_STILL_RUNNING' }
    $switched = $true
    Copy-ReleaseFiles $src
    Enable-ScheduledTask -TaskName $taskName -ErrorAction Stop | Out-Null
    Start-ScheduledTask -TaskName $taskName -ErrorAction Stop
    $healthy = $false
    if ($endpoint) {
        $infoBody = '{"jsonrpc":"2.0","id":20261102,"method":"tools/call","params":{"name":"openfill_info","arguments":{}}}'
        for ($i = 0; $i -lt 25; $i++) {
            Start-Sleep -Seconds 1
            try {
                $info = Invoke-RestMethod -Uri $endpoint -Method Post -Body $infoBody -ContentType 'application/json' -TimeoutSec 2
                if (!$info.error -and $info.result) { $healthy = $true; break }
            } catch { }
        }
    } else {
        Write-Output 'MCP_VERIFICATION_NOT_AVAILABLE'
        $healthy = $true
    }
    $gotCore = (Get-FileHash $dstCore -Algorithm SHA256).Hash
    $gotExe = (Get-FileHash $dstExe -Algorithm SHA256).Hash
    $running = @(Get-LiveProcesses).Count -gt 0
    if (!$running -or !$healthy -or $gotCore -ne $wantCore -or $gotExe -ne $wantExe) {
        throw "LIVE_VERIFY_FAILED process=$running mcp=$healthy core=$gotCore expected=$wantCore"
    }
    Write-Output "LIVE_RELEASE_OK core_sha256=$gotCore mcp=$healthy (active tasks never gated release)"
}
catch {
    $err = $_.Exception.Message
    Write-Output "LIVE_RELEASE_FAILED $err"
    if ($switched -and $backupReady) {
        Stop-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
        foreach ($live in @(Get-LiveProcesses)) { Stop-Process -Id $live.ProcessId -Force -ErrorAction SilentlyContinue }
        Start-Sleep -Milliseconds 500
        Copy-ReleaseFiles $backup
        Write-Output 'LIVE_ROLLBACK_BINARIES_RESTORED'
    }
    Enable-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue | Out-Null
    Start-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
    throw
}
# robocopy success codes 1..7 must not leak as script failure after LIVE_RELEASE_OK.
exit 0
