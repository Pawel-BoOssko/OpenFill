# A07 staging QA read-only
$ErrorActionPreference='Stop'
if ($env:COMPUTERNAME -ne 'PBOX1') { throw 'WRONG_HOST' }
$secret=(Get-Content 'C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt' -Raw).Trim()
$url='http://127.0.0.1:48732/mcp/'+$secret
$args=@{requester='ChatGPT';chat='OpenFill krokowe wdrożenie OF-IMP';title='A07 verify_view QA';idempotency_key='OFIMP-A07-VERIFY-VIEW-20261101-01';wait_seconds=15;task='STAGING QA read-only. Navigate to https://example.com/. Call verify_view with selector=a[href], check=visible, expected_host=example.com, timeout_ms=1000. Then call verify_view with selector=#ofimp-nonexistent-a07, check=absent, expected_host=example.com, timeout_ms=0. Report both results, reason_codes, probes, full_dom_capture and elapsed_ms. Do not call get_page, page_capture, screenshot, or perform writes. Finish.'}
$body=@{jsonrpc='2.0';id=2026110107;method='tools/call';params=@{name='openfill_start_task';arguments=$args}} | ConvertTo-Json -Depth 12 -Compress
$r=Invoke-RestMethod -Uri $url -Method POST -Body $body -ContentType 'application/json' -TimeoutSec 30
if($r.error){throw ('DEV_RPC_ERROR_'+$r.error.code)}
$r.result.content[0].text
