# OpenFill JEV direct LIVE acceptance (via local MCP, no API key read or output).
$ErrorActionPreference='Stop'
$secretPath='C:\Users\pboru\OpenFill\data\stable\runtime\mcp-secret.txt'
$secret=([IO.File]::ReadAllText($secretPath)).Trim()
$endpoint='http://127.0.0.1:48731/mcp/'+$secret
function Call-OpenFill([string]$method, [hashtable]$parameters, [int]$requestId) {
  $body=@{jsonrpc='2.0';id=$requestId;method='tools/call';params=@{name=$method;arguments=$parameters}} | ConvertTo-Json -Depth 12 -Compress
  $response=Invoke-RestMethod -Uri $endpoint -Method Post -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json; charset=utf-8' -TimeoutSec 55
  if($response.error){throw ('MCP_RPC_ERROR_'+$response.error.code)}
  if(-not $response.result.content[0].text){throw 'MCP_RESULT_NO_TEXT'}
  return ($response.result.content[0].text|ConvertFrom-Json)
}
$bad=Call-OpenFill 'openfill_start_task' @{task='Otwórz https://example.com i podaj tytuł. Na wszelki wypadek nie otwieraj Gmaila i nic więcej nie zmieniaj.';requester='ChatGPT';chat='Direct JEV acceptance';wait_seconds=0} 1001
'RESTRICTED_STATUS='+$bad.status
'RESTRICTED_SCORE='+$bad.score
'RESTRICTED_HAS_TASK_ID='+[bool]$bad.task_id
if($bad.status -ne 'rejected_rewrite_needed' -or [double]$bad.score -lt .85){throw 'DIRECT_JEV_INBOUND_TEST_FAILED'}
$good=Call-OpenFill 'openfill_start_task' @{task='Otwórz https://example.com i podaj tytuł strony.';requester='ChatGPT';chat='Direct JEV acceptance';wait_seconds=0} 1002
'NORMAL_STATUS='+$good.status
'NORMAL_TASK_ID_PRESENT='+[bool]$good.task_id
if($good.status -eq 'rejected_rewrite_needed' -or (-not $good.task_id)){throw 'DIRECT_JEV_NORMAL_TEST_FAILED'}
'ACCEPTANCE_INBOUND=PASS'
