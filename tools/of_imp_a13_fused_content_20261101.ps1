$ErrorActionPreference='Stop'
$secret=[IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt').Trim()
$url='http://127.0.0.1:48732/mcp/'+$secret
$instruction=@'
STAGING BENCHMARK ON LOCAL FIXTURE ONLY. Navigate to http://127.0.0.1:18877/network.html using view_mode=content, which must return both records and controls in the SAME call. Do NOT call read_view or get_page separately. Next make EXACTLY ONE act_many call with exactly these four selector-based steps, in order:
1 {selector:"#search",action:"set",value:"Jordan Hill",expected_host:"127.0.0.1"}
2 {selector:"[role=listitem]",action:"click",expected_text:"Jordan Hill #1",expected_host:"127.0.0.1"}
3 {selector:"#draft",action:"set",value:"OFIMP local batch benchmark C",expected_host:"127.0.0.1"}
4 {selector:"#send",action:"click",expected_host:"127.0.0.1",verify:{selector:"#conversation article.bubble.out:last-child",check:"text_contains",expected:"OFIMP local batch benchmark C",expected_host:"127.0.0.1"}}
If the batch returns completed_steps=4, verification_status=observed and no uncertain step, finish success. Do not call other action tools, do not call get_page and do not interact with any real provider. Report number of actual model calls, batch steps and cost.
'@
$args=@{requester='ChatGPT';chat='OpenFill krokowe wdrożenie OF-IMP';title='A12 semantic batch acceptance';idempotency_key='OFIMP-A13-NAV-CONTENT-BENCHMARK-20261101-01';wait_seconds=15;task=$instruction}
$body=@{jsonrpc='2.0';id=88103;method='tools/call';params=@{name='openfill_start_task';arguments=$args}}|ConvertTo-Json -Depth 12 -Compress
$r=Invoke-RestMethod -Uri $url -Method POST -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json; charset=utf-8' -TimeoutSec 30
$r.result.content[0].text
