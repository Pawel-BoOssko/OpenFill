$ErrorActionPreference='Stop'
$secret=(Get-Content 'C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt' -Raw).Trim()
$url='http://127.0.0.1:48732/mcp/'+$secret
$args=@{requester='ChatGPT';chat='OpenFill krokowe wdrożenie OF-IMP';title='A06 positive-match read_view acceptance';idempotency_key='OFIMP-A06-POSITIVE-MATCH-20261101-01';wait_seconds=15;task='STAGING QA read-only. Navigate to https://example.com/. Call internal read_view exactly once with view_type=list, operation=text_literal, query=documentation. Save its capture_id. Then call read_view with the SAME capture_id, view_type=list, operation=text_count, query=domain. Verify first has positive matches and second has reused_snapshot=true with positive count. Report exact tool args, capture_id, statuses and counts. No submissions, no messages, no changes to websites. Finish.'}
$body=@{jsonrpc='2.0';id=8612;method='tools/call';params=@{name='openfill_start_task';arguments=$args}} | ConvertTo-Json -Depth 12 -Compress
$r=Invoke-RestMethod -Uri $url -Method POST -Body $body -ContentType 'application/json' -TimeoutSec 30
$r.result.content[0].text