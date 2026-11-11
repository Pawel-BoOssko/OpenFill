$ErrorActionPreference='Stop'
$secret=[IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt').Trim()
$endpoint='http://127.0.0.1:48732/mcp/'+$secret
$args=@{
 task='QA read-only A08 baseline. Navigate to https://example.com/. Use get_page exactly once to identify the current page structure and classify it as detail, list, table, inbox or form. Report the classification and tool evidence. Do not use inspect_view, page_capture, observe_query or any consequential action. Do not repeat get_page.'
 requester='ChatGPT'
 chat='OpenFill krokowe wdrozenie OF-IMP'
 title='A08 full page baseline'
 idempotency_key='OFIMP-A08-BASELINE-20261101-01'
 wait_seconds=5
}
$body=@{jsonrpc='2.0';id=20261109;method='tools/call';params=@{name='openfill_start_task';arguments=$args}} | ConvertTo-Json -Compress -Depth 10
$response=Invoke-RestMethod -Uri $endpoint -Method Post -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json; charset=utf-8' -TimeoutSec 20
if ($response.error) { throw ('DEV_RPC_ERROR_'+$response.error.code) }
$response.result.content[0].text
