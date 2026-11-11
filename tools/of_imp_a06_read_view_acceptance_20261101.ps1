$ErrorActionPreference='Stop'
$secret=(Get-Content 'C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt' -Raw).Trim()
$url='http://127.0.0.1:48732/mcp/'+$secret
$args=@{requester='ChatGPT';chat='OpenFill krokowe wdrożenie OF-IMP';title='A06 typed read_view WebView2 acceptance';idempotency_key='OFIMP-A06-READ-VIEW-20261101-01';wait_seconds=15;task='STAGING QA read-only. Navigate to https://example.com/. Call internal read_view exactly once with view_type=list, operation=text_literal, query=Example Domain. Read its returned capture_id. Then call internal read_view with that SAME capture_id, view_type=list, operation=text_count, query=Example. The second call MUST be file-only, not a fresh DOM read. Report the two tool call arguments, capture_id, statuses, reused_snapshot and match count. Do not send messages, submit forms or change data. Finish.'}
$body=@{jsonrpc='2.0';id=8611;method='tools/call';params=@{name='openfill_start_task';arguments=$args}} | ConvertTo-Json -Depth 12 -Compress
$r=Invoke-RestMethod -Uri $url -Method POST -Body $body -ContentType 'application/json' -TimeoutSec 30
$r.result.content[0].text
