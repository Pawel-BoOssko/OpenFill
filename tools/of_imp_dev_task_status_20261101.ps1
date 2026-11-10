param([string]$TaskId='OF-20261101-0520-0be8')
$ErrorActionPreference='Stop'
$secret=[IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt').Trim()
$endpoint='http://127.0.0.1:48732/mcp/'+$secret
$body=@{jsonrpc='2.0';id=20261103;method='tools/call';params=@{name='openfill_task_status';arguments=@{task_id=$TaskId;wait_seconds=10}}} | ConvertTo-Json -Compress -Depth 8
$response=Invoke-RestMethod -Uri $endpoint -Method Post -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json; charset=utf-8' -TimeoutSec 25
if ($response.error) { throw ('DEV_RPC_ERROR_'+$response.error.code) }
$result=$response.result.content[0].text | ConvertFrom-Json
$result | ConvertTo-Json -Depth 9
