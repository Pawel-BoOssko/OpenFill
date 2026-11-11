$ErrorActionPreference='Stop'
$secret=[IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt').Trim()
$endpoint='http://127.0.0.1:48732/mcp/'+$secret
$body='{"jsonrpc":"2.0","id":21,"method":"tools/list","params":{}}'
$r=Invoke-RestMethod -Uri $endpoint -Method POST -Body $body -ContentType 'application/json' -TimeoutSec 10
if ($r.error) { throw ('MCP_ERROR_'+$r.error.code) }
$r.result.tools | Where-Object { $_.name -eq 'openfill_page_snapshot' } | ConvertTo-Json -Depth 14
