$ErrorActionPreference='Stop'
$secret=(Get-Content 'C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt' -Raw).Trim()
$url='http://127.0.0.1:48732/mcp/'+$secret
$args=@{requester='ChatGPT';chat='OpenFill krokowe wdrożenie OF-IMP';title='A05 verified live view seed';idempotency_key='OFIMP-A05-LIVE-VIEW-SEED-20261101-01';wait_seconds=20;task='Read-only QA on STAGING. Navigate to https://example.com/ and use page_capture with mode=all selector=body once. Do not submit forms, log in, send messages, or change any data. Report capture_id, view_signature and recipe_selection. Finish task successfully only if capture is saved.'}
$body=@{jsonrpc='2.0';id=8501;method='tools/call';params=@{name='openfill_start_task';arguments=$args}} | ConvertTo-Json -Depth 12 -Compress
$r=Invoke-RestMethod -Uri $url -Method POST -Body $body -ContentType 'application/json' -TimeoutSec 30
$r.result.content[0].text
