$ErrorActionPreference='Stop'
$secret=[IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt').Trim()
$url='http://127.0.0.1:48732/mcp/'+$secret
$instruction=@'
STAGING A16 CONTINUATION, LOCAL FIXTURE ONLY. This task CONTINUES the earlier local task in its same browser tab. First inspect the automatically attached INITIAL LIVE VIEW in your first prompt; if present it should be from network.html and contain visible records/controls. Do not navigate or call read_view/get_page if the current page is already the local ProNetwork fixture. Use exactly one act_many to send one more LOCAL message to Alex Hill #2:
1 {selector:"#search",action:"set",value:"Alex Hill #2",expected_host:"127.0.0.1"}
2 {selector:"[role=listitem]",action:"click",expected_text:"Alex Hill #2",timeout_ms:2500,expected_host:"127.0.0.1",verify:{selector:"#profile",check:"text_contains",expected:"Alex Hill #2",expected_host:"127.0.0.1"}}
3 {selector:"#draft",action:"set",value:"OFIMP A16 continuation local",expected_host:"127.0.0.1"}
4 {selector:"#send",action:"click",expected_host:"127.0.0.1",include_view_after:true,verify:{selector:"#conversation article.bubble.out:last-child",check:"text_contains",expected:"OFIMP A16 continuation local",expected_host:"127.0.0.1"}}
If the prior tab is unavailable, do not guess: report partial. No external sites or real sends. Report whether the initial live view was attached, model calls, steps and verified result.
'@
$args=@{requester='ChatGPT';chat='OpenFill krokowe wdrożenie OF-IMP';title='A16 generic initial context continuation';continue_task_id='OF-20261101-2027-2f14';wait_seconds=15;task=$instruction}
$body=@{jsonrpc='2.0';id=88103;method='tools/call';params=@{name='openfill_start_task';arguments=$args}}|ConvertTo-Json -Depth 12 -Compress
$r=Invoke-RestMethod -Uri $url -Method POST -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json; charset=utf-8' -TimeoutSec 30
$r.result.content[0].text
