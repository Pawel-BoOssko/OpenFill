$ErrorActionPreference='Stop'
if ($env:COMPUTERNAME -ne 'PBOX1') { throw 'WRONG_HOST' }
$secret=(Get-Content 'C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt' -Raw).Trim()
$url='http://127.0.0.1:48732/mcp/'+$secret
$args=@{requester='ChatGPT';chat='OpenFill krokowe wdrożenie OF-IMP';title='A07 act plus targeted local verification';idempotency_key='OFIMP-A07-ACT-VERIFY-20261101-01';wait_seconds=15;task='STAGING QA ONLY, NO EXTERNAL SUBMISSION. Navigate https://example.com/. To create a harmless temporary local fixture use run_js exactly once in expression mode with code (()=>{let i=document.createElement("input");i.id="ofimp-a07-field";i.type="text";i.setAttribute("aria-label","OFIMP QA input");document.body.appendChild(i);return true})(). Then call get_page exactly once to identify the element ID for the new input. Call act exactly once with that ID, action=set, value=OFIMP-A07-PASS and optional verify object {selector:"#ofimp-a07-field",check:"value_equals",expected:"OFIMP-A07-PASS",expected_host:"example.com"}. Check that the same act tool result includes action_executed=true, verification_status=observed and repeat_action_automatically=false. No separate verify_view tool call and no further get_page. Report tool evidence, cost, steps. Do not click links or submit data. Finish.'}
$body=@{jsonrpc='2.0';id=8722;method='tools/call';params=@{name='openfill_start_task';arguments=$args}} | ConvertTo-Json -Depth 12 -Compress
$r=Invoke-RestMethod -Uri $url -Method POST -Body $body -ContentType 'application/json' -TimeoutSec 30
$r.result.content[0].text
