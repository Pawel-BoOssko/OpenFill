$ErrorActionPreference='Stop'
$secret=[IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt').Trim()
$url='http://127.0.0.1:48732/mcp/'+$secret
$args=@{
 requester='ChatGPT';chat='OpenFill szybkie wydanie A20'
 title='A20 compact prompt smoke'
 idempotency_key='OFIMP-A20-PROMPT-20261101-01'
 wait_seconds=15
 task='STAGING READ ONLY. Navigate to https://example.com/ using navigate view_mode=structure. Report the view_type and whether full_dom_capture is false. Do not use any other read tool and do not change the site. Finish.'
}
$body=@{jsonrpc='2.0';id=1820;method='tools/call';params=@{name='openfill_start_task';arguments=$args}} | ConvertTo-Json -Depth 12 -Compress
$r=Invoke-RestMethod -Uri $url -Method POST -Body $body -ContentType 'application/json' -TimeoutSec 30
if ($r.error) {throw ('DEV_RPC_ERROR_'+$r.error.code)}
$r.result.content[0].text
