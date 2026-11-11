$ErrorActionPreference='Stop'
$secret=[IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt').Trim()
$url='http://127.0.0.1:48732/mcp/'+$secret
$instruction=@'
A18 telemetry continuation acceptance. Continue the PREVIOUS local CRM fixture task in its same saved tab. Read the automatically attached initial live view and count currently visible table rows, then finish with a one-sentence count. This is read only, no changes or external services. The purpose is to emit a real usage task record with continuedFrom matching the prior task ID.
'@
$args=@{requester='ChatGPT';chat='OpenFill krokowe wdrożenie OF-IMP';title='A16 generic initial context continuation';continue_task_id='OF-20261101-2054-3638';wait_seconds=15;task=$instruction}
$body=@{jsonrpc='2.0';id=88103;method='tools/call';params=@{name='openfill_start_task';arguments=$args}}|ConvertTo-Json -Depth 12 -Compress
$r=Invoke-RestMethod -Uri $url -Method POST -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json; charset=utf-8' -TimeoutSec 30
$r.result.content[0].text
