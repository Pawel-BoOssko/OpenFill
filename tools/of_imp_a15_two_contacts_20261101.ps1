$ErrorActionPreference='Stop'
$secret=[IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt').Trim()
$url='http://127.0.0.1:48732/mcp/'+$secret
$instruction=@'
STAGING multi-recipient benchmark, LOCAL FIXTURE ONLY at http://127.0.0.1:18877/network.html (350 ms delayed search). Navigate ONCE with view_mode=content. Use exactly ONE act_many call with these 8 steps and NO get_page/read_view:
1 {selector:"#search",action:"set",value:"Jordan Hill #43",expected_host:"127.0.0.1"}
2 {selector:"[role=listitem]",action:"click",expected_text:"Jordan Hill #43",timeout_ms:2500,expected_host:"127.0.0.1",verify:{selector:"#profile",check:"text_contains",expected:"Jordan Hill #43",expected_host:"127.0.0.1"}}
3 {selector:"#draft",action:"set",value:"OFIMP A15 local message one",expected_host:"127.0.0.1"}
4 {selector:"#send",action:"click",expected_host:"127.0.0.1",verify:{selector:"#conversation article.bubble.out:last-child",check:"text_contains",expected:"OFIMP A15 local message one",expected_host:"127.0.0.1"}}
5 {selector:"#search",action:"set",value:"Jordan Kim #50",expected_host:"127.0.0.1"}
6 {selector:"[role=listitem]",action:"click",expected_text:"Jordan Kim #50",timeout_ms:2500,expected_host:"127.0.0.1",verify:{selector:"#profile",check:"text_contains",expected:"Jordan Kim #50",expected_host:"127.0.0.1"}}
7 {selector:"#draft",action:"set",value:"OFIMP A15 local message two",expected_host:"127.0.0.1"}
8 {selector:"#send",action:"click",expected_host:"127.0.0.1",include_view_after:true,verify:{selector:"#conversation article.bubble.out:last-child",check:"text_contains",expected:"OFIMP A15 local message two",expected_host:"127.0.0.1"}}
Both recipients must be matched exactly, the two LOCAL sends verified independently and next_view_status=available. Stop on any failure; no retries, no real sites, no other action tools. Report actual calls, batch steps, preflight probes and cost.
'@
$args=@{requester='ChatGPT';chat='OpenFill krokowe wdrożenie OF-IMP';title='A12 semantic batch acceptance';idempotency_key='OFIMP-A15-TWO-RECIPIENTS-20261101-01';wait_seconds=15;task=$instruction}
$body=@{jsonrpc='2.0';id=88103;method='tools/call';params=@{name='openfill_start_task';arguments=$args}}|ConvertTo-Json -Depth 12 -Compress
$r=Invoke-RestMethod -Uri $url -Method POST -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json; charset=utf-8' -TimeoutSec 30
$r.result.content[0].text
