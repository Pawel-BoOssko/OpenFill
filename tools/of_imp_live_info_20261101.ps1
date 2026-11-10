$ErrorActionPreference='Stop'
$secret=[IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\stable\runtime\mcp-secret.txt').Trim()
$endpoint='http://127.0.0.1:48731/mcp/'+$secret
$body='{"jsonrpc":"2.0","id":16701,"method":"tools/call","params":{"name":"openfill_info","arguments":{}}}'
$r=Invoke-RestMethod -Uri $endpoint -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 10
if ($r.error){throw ('LIVE_RPC_ERROR_'+$r.error.code)}
$r.result.content[0].text
