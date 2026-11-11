$ErrorActionPreference='Stop'
$secret=[IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt').Trim()
$url='http://127.0.0.1:48732/mcp/'+$secret
$instruction=@'
STAGING A18 usage telemetry check. Open local http://127.0.0.1:18877/crm.html using navigate(view_mode=content,expected_view_type=table,wait_ms=4000), count visible rows and finish read-only. The outcome should generate a new usage task record containing taskKey, continuedFrom and requesterChat.
'@
$args=@{requester='ChatGPT';chat='OpenFill krokowe wdrożenie OF-IMP';title='A12 semantic batch acceptance';idempotency_key='OFIMP-A18-USAGE-CHAIN-20261101-01';wait_seconds=15;task=$instruction}
$body=@{jsonrpc='2.0';id=88103;method='tools/call';params=@{name='openfill_start_task';arguments=$args}}|ConvertTo-Json -Depth 12 -Compress
$r=Invoke-RestMethod -Uri $url -Method POST -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json; charset=utf-8' -TimeoutSec 30
$r.result.content[0].text
