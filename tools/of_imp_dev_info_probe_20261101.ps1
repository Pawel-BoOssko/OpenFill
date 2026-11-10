$ErrorActionPreference='Stop'
$secret=(Get-Content 'C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt' -Raw).Trim()
$url='http://127.0.0.1:48732/mcp/'+$secret
$body='{"jsonrpc":"2.0","id":731,"method":"tools/call","params":{"name":"openfill_info","arguments":{}}}'
$r=Invoke-RestMethod -Uri $url -Method POST -Body $body -ContentType 'application/json' -TimeoutSec 8
$s=$r.result.content[0].text | ConvertFrom-Json
if($null -eq $s.working){throw 'MCP_INFO_MISSING_WORKING'}
'ACTIVE_TASK_COUNT='+@($s.working).Count
'MCP_DEV_INFO_OK=True'
