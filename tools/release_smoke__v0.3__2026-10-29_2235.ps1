# OpenFill - Metadata: wersja 0.3, data 2026-10-29 22:35
# Read-only post-release smoke. Never print MCP secret, JEV key or task contents.
param([int]$TimeoutSeconds = 5)
$ErrorActionPreference = 'Stop'
$root = 'C:\Users\pboru\OpenFill'
$fail = New-Object System.Collections.Generic.List[string]
$warn = New-Object System.Collections.Generic.List[string]
try {
    if ($env:COMPUTERNAME -ne 'PBOX1') { throw "WRONG_HOST: $env:COMPUTERNAME" }
    $process = @(Get-Process -Name OpenFill -ErrorAction SilentlyContinue)
    if ($process.Count -ne 1) {
        $fail.Add("OPENFILL_PROCESS_COUNT="+$process.Count)
    } else {
        "OPENFILL_PID="+$process[0].Id
        "OPENFILL_STARTED="+$process[0].StartTime.ToString('o')
    }
    $exe = Join-Path $root 'app\OpenFill.exe'
    if (!(Test-Path $exe)) { $fail.Add('LIVE_EXE_MISSING') }
    $tcp = @(Get-NetTCPConnection -LocalPort 48731 -State Listen -ErrorAction SilentlyContinue)
    if ($tcp.Count -eq 0) { $fail.Add('MCP_PORT_NOT_LISTENING') }
    $mcpSecretFile = Join-Path $root 'data\stable\runtime\mcp-secret.txt'
    if (!(Test-Path $mcpSecretFile)) { $fail.Add('MCP_SECRET_FILE_MISSING') }
    else {
        $secret = (Get-Content $mcpSecretFile -Raw).Trim()
        $url = 'http://127.0.0.1:48731/mcp/' + $secret
        $body = '{"jsonrpc":"2.0","id":301,"method":"tools/call","params":{"name":"openfill_info","arguments":{}}}'
        try {
            $response = Invoke-RestMethod -Uri $url -Method POST -Body $body -ContentType 'application/json' -TimeoutSec $TimeoutSeconds
            $status = $response.result.content[0].text | ConvertFrom-Json
            if ($null -eq $status.working) { $fail.Add('MCP_COMPACT_CONTRACT_MISSING_WORKING') }
            else { 'MCP_STATUS_OK=True';'ACTIVE_TASK_COUNT='+@($status.working).Count }
        } catch { $fail.Add('MCP_LOCAL_RPC_FAILED:'+ $_.Exception.GetType().Name) }
    }
    # JEV is a direct OpenRouter API call from PBOX1; SATANAMD must not
    # appear in the smoke health path. This probe does not spend tokens or
    # claim to verify the actual model score.
    $jevKey = Join-Path $root 'data\stable\runtime\jev-api-key.bin'
    if (!(Test-Path $jevKey)) { $fail.Add('JEV_LOCAL_KEY_NOT_CONFIGURED') }
    elseif ((Get-Item $jevKey).Length -lt 40) { $fail.Add('JEV_LOCAL_KEY_FILE_INVALID_SIZE') }
    else { 'JEV_LOCAL_DPAPI_KEY_PRESENT=True' }
    $jevApiUrl = 'https://openrouter.ai/api/alpha/decisions'
    try {
        $rq = [System.Net.HttpWebRequest]::Create($jevApiUrl)
        $rq.Method = 'GET'
        $rq.Timeout = [Math]::Max(1000,$TimeoutSeconds * 1000)
        $rq.AllowAutoRedirect = $false
        try {
            $rs = $rq.GetResponse()
            $code = [int]$rs.StatusCode
            $rs.Close()
        } catch [System.Net.WebException] {
            if (-not $_.Exception.Response) { throw }
            $code = [int]$_.Exception.Response.StatusCode
            $_.Exception.Response.Close()
        }
        'JEV_DIRECT_API_TRANSPORT_REACHABLE=True'
        'JEV_API_HTTP_GET_STATUS='+$code
        'JEV_MODEL_SCORE_TESTED=False'
    } catch { $fail.Add('JEV_DIRECT_API_TRANSPORT_FAILED:'+ $_.Exception.GetType().Name + ': ' + $_.Exception.Message) }
    $launch = Get-ScheduledTask -TaskName 'OpenFill-open' -ErrorAction SilentlyContinue
    if ($null -eq $launch) { $fail.Add('LAUNCH_TASK_MISSING') }
    else { 'LAUNCH_TASK='+$launch.State }
} catch {
    $fail.Add('CHECK_FAILED:'+ $_.Exception.GetType().Name)
}
'FAILED_CHECK_COUNT='+$fail.Count
foreach($f in $fail) { 'FAIL='+$f }
foreach($w in $warn) { 'WARN='+$w }
if ($fail.Count -gt 0) { exit 2 }
'LIVE_SMOKE=PASS'
exit 0
