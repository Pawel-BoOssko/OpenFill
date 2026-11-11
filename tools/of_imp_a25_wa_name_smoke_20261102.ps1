$ErrorActionPreference='Stop'
$secret=[IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt').Trim()
$uri='http://127.0.0.1:48732/mcp/'+$secret
$task="Check new internal tool whatsapp_read_names in STAGING, read-only. Navigate to https://web.whatsapp.com/ if necessary, then call whatsapp_read_names exactly once with names=[A25-UNLIKELY-CHAT-TEST-20261102], max_messages=2. No sends, no account changes, no retries or extra tool calls. Report the resulting status (e.g. name_not_rendered or sidebar_not_loaded). Finish."
$args=@{requester='ChatGPT';chat='OpenFill A25 20261102';title='A25 names read-only smoke';idempotency_key='OFIMP-A25-NAMES-20261102-01';wait_seconds=15;task=$task}
$req=@{jsonrpc='2.0';id=2525;method='tools/call';params=@{name='openfill_start_task';arguments=$args}} | ConvertTo-Json -Depth 10 -Compress
$res=Invoke-RestMethod -Uri $uri -Method Post -Body ([Text.Encoding]::UTF8.GetBytes($req)) -ContentType 'application/json; charset=utf-8' -TimeoutSec 30
if($res.error){throw ('DEV_RPC_ERROR_'+$res.error.code)}
$res.result.content[0].text
