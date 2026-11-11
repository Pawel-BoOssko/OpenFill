# One read-only DEV task. Fixed idempotency key prevents duplicate submissions.
$ErrorActionPreference='Stop'
if ($env:COMPUTERNAME -ne 'PBOX1') { throw 'WRONG_HOST' }
$secret=[IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt').Trim()
$endpoint='http://127.0.0.1:48732/mcp/'+$secret
$arguments=@{
  task='Inspect the current Airbnb hosting calendar in this DEV browser. Open https://www.airbnb.com/hosting/calendar, identify whether the account is authenticated, and report the final URL and visible DOM markers for account, listing, calendar day, price and editor. This is read-only research; return observed facts, no modifications.'
  requester='ChatGPT'
  chat='OpenFill krokowe wdrożenie OF-IMP'
  idempotency_key='OFIMP-D03-AIRBNB-DOM-20261101-01'
  wait_seconds=0
}
$body=@{jsonrpc='2.0';id=20261102;method='tools/call';params=@{name='openfill_start_task';arguments=$arguments}} | ConvertTo-Json -Compress -Depth 8
$response=Invoke-RestMethod -Uri $endpoint -Method Post -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json; charset=utf-8' -TimeoutSec 20
if ($response.error) { throw ('DEV_RPC_ERROR_'+$response.error.code) }
$result=$response.result.content[0].text | ConvertFrom-Json
'DEV_PROBE_STATUS='+$result.status
'DEV_PROBE_TASK_ID='+$result.task_id
'DEV_PROBE_OUTCOME='+$result.outcome
