$ErrorActionPreference='Stop'
$secret=[IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt').Trim()
$endpoint='http://127.0.0.1:48732/mcp/'+$secret
$args=@{
 task='QA read-only A08 fused mode. Navigate to https://example.com/ with navigate view_mode=structure exactly once. Classify the page as detail, list, table, inbox or form from the returned structural manifest. Report view_type, confidence, view_signature, full_dom_capture. Do not use get_page, inspect_view, page_capture, observe_query or any consequential action.'
 requester='ChatGPT'
 chat='OpenFill krokowe wdrozenie OF-IMP'
 title='A08 fused navigation QA'
 idempotency_key='OFIMP-A08-FUSED-20261101-01'
 wait_seconds=5
}
$body=@{jsonrpc='2.0';id=20261110;method='tools/call';params=@{name='openfill_start_task';arguments=$args}} | ConvertTo-Json -Compress -Depth 10
$response=Invoke-RestMethod -Uri $endpoint -Method Post -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json; charset=utf-8' -TimeoutSec 20
if ($response.error) { throw ('DEV_RPC_ERROR_'+$response.error.code) }
$response.result.content[0].text
