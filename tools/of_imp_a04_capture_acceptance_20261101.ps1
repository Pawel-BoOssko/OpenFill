$ErrorActionPreference='Stop'
$secret=(Get-Content 'C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt' -Raw).Trim()
$url='http://127.0.0.1:48732/mcp/'+$secret
$args=@{requester='ChatGPT';chat='OpenFill krokowe wdrożenie OF-IMP';title='A04 manifest QA';idempotency_key='OFIMP-A04-CAPTURE-MANIFEST-20261101-01';wait_seconds=15;task='QA read-only. Open https://example.com and create one fresh DOM capture of the page body using page_capture mode=text selector=body. Do not submit forms or change any data. Return the actual capture_id and manifest_path from the tool. The purpose is checking the new A04 manifest metadata on STAGING.'}
$body=@{jsonrpc='2.0';id=733;method='tools/call';params=@{name='openfill_start_task';arguments=$args}} | ConvertTo-Json -Depth 12 -Compress
$r=Invoke-RestMethod -Uri $url -Method POST -Body $body -ContentType 'application/json' -TimeoutSec 25
$r.result.content[0].text
