# OpenFill OF-IMP: short in-place release, only while LIVE has no active work.
$ErrorActionPreference = 'Stop'
if ($env:COMPUTERNAME -ne 'PBOX1') { throw 'WRONG_HOST' }
$src = 'C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\staging\of_imp_20261031'
$dst = 'C:\Users\pboru\OpenFill\app'
$bak = 'C:\Users\pboru\OpenFill\app__rollback__OFIMP_A23_20261101'
$secret = [IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\stable\runtime\mcp-secret.txt').Trim()
$endpoint = 'http://127.0.0.1:48731/mcp/' + $secret
$req = '{"jsonrpc":"2.0","id":1731,"method":"tools/call","params":{"name":"openfill_info","arguments":{}}}'
function Read-LiveInfo {
    $res = Invoke-RestMethod -Uri $endpoint -Method POST -Body $req -ContentType 'application/json' -TimeoutSec 5
    if ($res.error) { throw "LIVE_RPC_ERROR_$($res.error.code)" }
    return ($res.result.content[0].text | ConvertFrom-Json)
}
$info = Read-LiveInfo
$active = @($info.working).Count
if ($active -ne 0) { Write-Output "DEFERRED_ACTIVE_TASKS=$active"; exit 10 }
$srcCore = Join-Path $src 'OpenFill.Core.dll'
$dstCore = Join-Path $dst 'OpenFill.Core.dll'
if (!(Test-Path $srcCore) -or !(Test-Path (Join-Path $src 'OpenFill.exe'))) { throw 'STAGING_BINARIES_MISSING' }
if (!(Test-Path (Join-Path $bak 'OpenFill.exe'))) { throw 'BACKUP_MISSING' }
$want = (Get-FileHash $srcCore -Algorithm SHA256).Hash
if ((Get-FileHash $dstCore -Algorithm SHA256).Hash -eq $want) { Write-Output "LIVE_ALREADY_CURRENT sha256=$want"; exit 0 }
function Stop-LiveAndWait {
    # Do not begin copying while Windows still holds the native SQLite library.
    $exe = Join-Path $dst 'OpenFill.exe'
    for ($i = 0; $i -lt 40; $i++) {
        $running = @(Get-CimInstance Win32_Process -Filter "name='OpenFill.exe'" |
            Where-Object { $_.ExecutablePath -ieq $exe })
        if ($running.Count -eq 0) {
            Start-Sleep -Milliseconds 400
            return
        }
        foreach ($proc in $running) {
            Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue
        }
        Start-Sleep -Milliseconds 250
    }
    throw 'LIVE_PROCESS_DID_NOT_EXIT'
}
function Copy-BinariesWithRetry([string] $from) {
    for ($attempt = 1; $attempt -le 12; $attempt++) {
        try {
            Copy-Item -Path (Join-Path $from '*') -Destination $dst -Recurse -Force -ErrorAction Stop
            return
        }
        catch [System.IO.IOException] {
            if ($attempt -ge 12) { throw }
            Start-Sleep -Milliseconds 500
        }
    }
}
Write-Output 'LIVE_RELEASE_START'
$switched = $false
try {
    Stop-ScheduledTask -TaskName 'OpenFill-open'
    $switched = $true
    Stop-LiveAndWait
    Copy-BinariesWithRetry $src
    Start-ScheduledTask -TaskName 'OpenFill-open'
    $healthy = $false
    for ($i=0; $i -lt 25; $i++) {
        Start-Sleep -Seconds 1
        try { $newInfo = Read-LiveInfo; $healthy = $true; break } catch { }
    }
    $got = (Get-FileHash $dstCore -Algorithm SHA256).Hash
    if (!$healthy -or $got -ne $want) { throw "LIVE_VERIFY_FAILED healthy=$healthy hash=$got expected=$want" }
    Write-Output "LIVE_RELEASE_OK sha256=$got active=$(@($newInfo.working).Count)"
}
catch {
    $errorText = $_.Exception.Message
    Write-Output "LIVE_RELEASE_FAILED $errorText"
    if ($switched) {
        Stop-ScheduledTask -TaskName 'OpenFill-open' -ErrorAction SilentlyContinue
        Stop-LiveAndWait
        Copy-BinariesWithRetry $bak
        Start-ScheduledTask -TaskName 'OpenFill-open'
        Write-Output 'LIVE_ROLLBACK_RESTART_REQUESTED'
    } else {
        Start-ScheduledTask -TaskName 'OpenFill-open' -ErrorAction SilentlyContinue
    }
    throw
}




