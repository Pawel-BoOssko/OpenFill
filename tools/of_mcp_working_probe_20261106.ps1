$ErrorActionPreference='Stop'
$uri=[uri](Get-Content 'C:\Users\pboru\OpenFill\data\stable\runtime\mcp-url.txt' -Raw).Trim()
$localUrl='http://127.0.0.1:48731'+$uri.AbsolutePath
$body=@{jsonrpc='2.0';id=1;method='tools/call';params=@{name='openfill_info';arguments=@{}}} | ConvertTo-Json -Depth 8 -Compress
$response=Invoke-RestMethod -Uri $localUrl -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 12
$content=$response.result.content | Where-Object type -eq 'text' | Select-Object -First 1
if (!$content) {throw 'OPENFILL_MCP_RESULT_NOT_TEXT'}
$details=$content.text | ConvertFrom-Json
if ($null -eq $details.working) {throw 'OPENFILL_MCP_WORKING_MISSING'}
Write-Output ('OPENFILL_WORKING_COUNT='+@($details.working).Count)
