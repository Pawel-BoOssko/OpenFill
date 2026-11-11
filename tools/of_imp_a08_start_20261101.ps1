$ErrorActionPreference='Stop'
$secret=[IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt').Trim()
$endpoint='http://127.0.0.1:48732/mcp/'+$secret
$args=@{
 task='QA read-only A08. Navigate to https://example.com/. Invoke inspect_view exactly once to identify the current page structure and return the actual compact manifest: status, view_type, confidence, route_key, view_signature, full_dom_capture, counts and elapsed_ms. Do not use get_page, page_capture, observe_query, or any consequential action. Do not repeat inspect_view. Finish with a factual report of the tool result.'
 requester='ChatGPT'
 chat='OpenFill krokowe wdrozenie OF-IMP'
 title='A08 semantic inspection QA'
 idempotency_key='OFIMP-A08-INSPECT-20261101-01'
 wait_seconds=5
}
$body=@{jsonrpc='2.0';id=20261108;method='tools/call';params=@{name='openfill_start_task';arguments=$args}} | ConvertTo-Json -Compress -Depth 10
$response=Invoke-RestMethod -Uri $endpoint -Method Post -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json; charset=utf-8' -TimeoutSec 20
if ($response.error) { throw ('DEV_RPC_ERROR_'+$response.error.code) }
$response.result.content[0].text
