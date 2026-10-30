# OpenFill JEV direct production acceptance. ASCII only, no key output.
$ErrorActionPreference = 'Stop'
$secretFile = 'C:\Users\pboru\OpenFill\data\stable\runtime\mcp-secret.txt'
$secret = [IO.File]::ReadAllText($secretFile).Trim()
$endpoint = 'http://127.0.0.1:48731/mcp/' + $secret
function Invoke-Mcp([string]$tool, [hashtable]$parameters, [int]$id) {
    $request = @{ jsonrpc='2.0'; id=$id; method='tools/call'; params=@{ name=$tool; arguments=$parameters } } | ConvertTo-Json -Compress -Depth 12
    $data = Invoke-RestMethod -Uri $endpoint -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($request)) -TimeoutSec 55
    if ($data.error) { throw ('MCP_RPC_'+$data.error.code) }
    $answer = $data.result.content | Where-Object { $_.type -eq 'text' } | Select-Object -First 1
    if (!$answer -or !$answer.text) { throw 'MCP_MISSING_CONTENT' }
    return ($answer.text | ConvertFrom-Json)
}
$bad = Invoke-Mcp 'openfill_start_task' @{ task='Open https://example.com and report the page title. Just in case, do not open Gmail and do not change anything else.'; requester='ChatGPT'; chat='JEV direct validation'; wait_seconds=0 } 8091
'INBOUND_STATUS=' + $bad.status
'INBOUND_SCORE=' + $bad.score
'INBOUND_HAS_TASK_ID=' + [bool]$bad.task_id
if ($bad.status -ne 'rejected_rewrite_needed' -or [double]$bad.score -lt 0.85 -or $bad.task_id) { throw 'INBOUND_JEV_FAIL' }
$good = Invoke-Mcp 'openfill_start_task' @{ task='Open https://example.com and report the page title.'; requester='ChatGPT'; chat='JEV direct validation'; wait_seconds=0 } 8092
'BENIGN_STATUS=' + $good.status
'BENIGN_TASK_ID=' + $good.task_id
if ($good.status -eq 'rejected_rewrite_needed' -or !$good.task_id) { throw 'BENIGN_JEV_FAIL' }
'INBOUND_DIRECT_JEV_ACCEPTANCE=PASS'
