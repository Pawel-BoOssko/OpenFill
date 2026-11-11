# OpenFill OF-IMP: short in-place release, only while LIVE has no active work.
$ErrorActionPreference = 'Stop'
if ($env:COMPUTERNAME -ne 'PBOX1') { throw 'WRONG_HOST' }
$src = 'C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\staging\of_imp_20261031'
$dst = 'C:\Users\pboru\OpenFill\app'
$bak = 'C:\Users\pboru\OpenFill\app__rollback__OFIMP_A14_20261101'
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
Write-Output 'LIVE_RELEASE_START'
$switched = $false
try {
    Stop-ScheduledTask -TaskName 'OpenFill-open'
    Start-Sleep -Milliseconds 800
    Get-Process -Name OpenFill -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -eq (Join-Path $dst 'OpenFill.exe') } |
        Stop-Process -Force -ErrorAction Stop
    $switched = $true
    Copy-Item -Path (Join-Path $src '*') -Destination $dst -Recurse -Force
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
        Start-Sleep -Milliseconds 800
        Copy-Item -Path (Join-Path $bak '*') -Destination $dst -Recurse -Force
        Start-ScheduledTask -TaskName 'OpenFill-open'
        Write-Output 'LIVE_ROLLBACK_RESTART_REQUESTED'
    } else {
        Start-ScheduledTask -TaskName 'OpenFill-open' -ErrorAction SilentlyContinue
    }
    throw
}
