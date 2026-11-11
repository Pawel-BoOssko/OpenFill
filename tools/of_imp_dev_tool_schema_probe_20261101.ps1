$ErrorActionPreference='Stop'
$secret=(Get-Content 'C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt' -Raw).Trim()
$url='http://127.0.0.1:48732/mcp/'+$secret
$body='{"jsonrpc":"2.0","id":732,"method":"tools/list","params":{}}'
$r=Invoke-RestMethod -Uri $url -Method POST -Body $body -ContentType 'application/json' -TimeoutSec 8
$r.result.tools | Where-Object {$_.name -eq 'openfill_start_task' -or $_.name -eq 'openfill_task_status'} | ForEach-Object { $_ | ConvertTo-Json -Depth 15 }
