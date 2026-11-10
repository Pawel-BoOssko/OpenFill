$ErrorActionPreference='Stop'
$secret=(Get-Content 'C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt' -Raw).Trim()
$url='http://127.0.0.1:48732/mcp/'+$secret
$args=@{requester='ChatGPT';chat='OpenFill krokowe wdrożenie OF-IMP';title='A05 verified live view reuse';idempotency_key='OFIMP-A05-LIVE-VIEW-REUSE-20261101-01';wait_seconds=20;task='Read-only STAGING acceptance. Navigate to https://example.com/ and call page_capture exactly once with ONLY use_best=true, without mode, selector or regex arguments. Do not change any data. Report exact recipe_selection, mode, selector, capture_id and view_signature returned by page_capture. Finish after saved capture.'}
$body=@{jsonrpc='2.0';id=8502;method='tools/call';params=@{name='openfill_start_task';arguments=$args}} | ConvertTo-Json -Depth 12 -Compress
$r=Invoke-RestMethod -Uri $url -Method POST -Body $body -ContentType 'application/json' -TimeoutSec 30
$r.result.content[0].text
