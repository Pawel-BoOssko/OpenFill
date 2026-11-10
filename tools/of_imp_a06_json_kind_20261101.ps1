$ErrorActionPreference='Stop'
$secret=(Get-Content 'C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt' -Raw).Trim()
$url='http://127.0.0.1:48732/mcp/'+$secret
$args=@{requester='ChatGPT';chat='OpenFill krokowe wdrożenie OF-IMP';title='A06 JSON elements inferred kind acceptance';idempotency_key='OFIMP-A06-JSON-KIND-20261101-01';wait_seconds=15;task='STAGING QA file-only. Do not navigate or read WebView2. Call internal read_view exactly once with capture_id=capture_20261101_060621_803_6cf7bb, view_type=list, operation=json_select, query=.[].label, OMIT file_kind. The existing saved capture has elements JSON. Report exact tool args, status, file_kind or source_path, preview and reused_snapshot. No actions, no changes. Finish.'}
$body=@{jsonrpc='2.0';id=8613;method='tools/call';params=@{name='openfill_start_task';arguments=$args}} | ConvertTo-Json -Depth 12 -Compress
$r=Invoke-RestMethod -Uri $url -Method POST -Body $body -ContentType 'application/json' -TimeoutSec 30
$r.result.content[0].text