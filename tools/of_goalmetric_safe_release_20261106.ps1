param([switch]$ProbeOnly)
$ErrorActionPreference='Stop'
$root='C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill'
$expectedStage='F9F0615EF87950ABCB68DC4C4D53F11AA6A5E6CABC6FCE58B31E11B827C92E23'
$expectedBefore='79227A504FB58DD6E7737D47A38EEECF1148FAA82FD59F9499D1D5C5981A020B'
$liveDll='C:\Users\pboru\OpenFill\app\OpenFill.Core.dll'
$stageDll=Join-Path $root 'staging\of_imp_20261031\OpenFill.Core.dll'
$marker=Join-Path $root 'docs\OPENFILL_GOALMETRIC_DEPLOYED_20261106.txt'
if (Test-Path $marker) { Write-Output 'GOALMETRIC_ALREADY_DEPLOYED'; exit 0 }
if (!(Test-Path $stageDll)) { throw 'GOALMETRIC_STAGE_NOT_FOUND' }
$stageSha=(Get-FileHash $stageDll -Algorithm SHA256).Hash
if ($stageSha -ne $expectedStage) { throw ('GOALMETRIC_STAGING_CHANGED sha='+$stageSha) }
if (!(Test-Path $liveDll)) { throw 'GOALMETRIC_LIVE_DLL_NOT_FOUND' }
$liveSha=(Get-FileHash $liveDll -Algorithm SHA256).Hash
if ($liveSha -eq $expectedStage) { Set-Content $marker 'LIVE_ALREADY_TARGET_SHA'; Write-Output 'GOALMETRIC_LIVE_ALREADY_TARGET_SHA'; exit 0 }
if ($liveSha -ne $expectedBefore) { throw ('GOALMETRIC_LIVE_SHA_CHANGED_NOT_OURS sha='+$liveSha) }
$uri=[uri](Get-Content 'C:\Users\pboru\OpenFill\data\stable\runtime\mcp-url.txt' -Raw).Trim()
$localUrl='http://127.0.0.1:48731'+$uri.AbsolutePath
function Invoke-LocalMcp($name,$args) {
    $req=@{jsonrpc='2.0';id=42;method='tools/call';params=@{name=$name;arguments=$args}} | ConvertTo-Json -Depth 9 -Compress
    $rpc=Invoke-RestMethod -Uri $localUrl -Method Post -ContentType 'application/json' -Body $req -TimeoutSec 20
    if ($rpc.error) {throw ('GOALMETRIC_MCP_ERROR '+($rpc.error | ConvertTo-Json -Compress))}
    $content=$rpc.result.content | Where-Object type -eq 'text' | Select-Object -First 1
    if (!$content) {throw 'GOALMETRIC_MCP_MISSING_RESULT'}
    return ($content.text | ConvertFrom-Json)
}
$info=Invoke-LocalMcp 'openfill_info' @{}
$active=@($info.working).Count
if ($active -gt 0) {Write-Output ('GOALMETRIC_WAIT_ACTIVE_TASKS='+$active);exit 0}
if ($ProbeOnly) {Write-Output 'GOALMETRIC_IDLE_READY_FOR_RELEASE';exit 0}
$gateHeld=$false
try {
    $gate=Invoke-LocalMcp 'openfill_maintenance' @{minutes=5;reason='Safe token-preserving goal accounting release';requester='ChatGPT'}
    $gateHeld=$true
    if ($gate.running_tasks -gt 0) {
        Write-Output ('GOALMETRIC_WAIT_RACE_ACTIVE='+$gate.running_tasks)
        return
    }
    Write-Output 'GOALMETRIC_SAFE_GATE_IDLE_CONFIRMED'
    & (Join-Path $root 'tools\of_imp_release_live_ALWAYS_20261101.ps1') -CheckOnly
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {throw ('GOALMETRIC_RELEASE_CHECK_FAILED='+$LASTEXITCODE)}
    & (Join-Path $root 'tools\of_imp_release_live_ALWAYS_20261101.ps1')
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {throw ('GOALMETRIC_RELEASE_FAILED='+$LASTEXITCODE)}
    & (Join-Path $root 'tools\of_e03_independent_live_verify_20261104.ps1')
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) {throw ('GOALMETRIC_INDEPENDENT_VERIFY_FAILED='+$LASTEXITCODE)}
    $nowSha=(Get-FileHash $liveDll -Algorithm SHA256).Hash
    if ($nowSha -ne $expectedStage) {throw ('GOALMETRIC_POST_RELEASE_HASH_MISMATCH='+$nowSha)}
    Set-Content -Path $marker -Value ('DEPLOYED '+(Get-Date -Format o)+' SHA='+$nowSha) -Encoding UTF8
    Write-Output ('GOALMETRIC_DEPLOY_SUCCESS='+$nowSha)
}
finally {
    if ($gateHeld) {
        try { $null=Invoke-LocalMcp 'openfill_maintenance' @{minutes=0;reason='Goal accounting release complete';requester='ChatGPT'} }
        catch { Write-Output ('GOALMETRIC_GATE_RESET_NOTICE='+$_.Exception.GetType().Name) }
    }
}
