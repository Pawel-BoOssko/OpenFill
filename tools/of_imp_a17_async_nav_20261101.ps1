$ErrorActionPreference='Stop'
$secret=[IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt').Trim()
$url='http://127.0.0.1:48732/mcp/'+$secret
$instruction=@'
STAGING QA A17: local dynamic SPA has a 1200 ms delayed message/list renderer. Navigate ONLY to http://127.0.0.1:18877/network.html using {url:"http://127.0.0.1:18877/network.html",view_mode:"content",expected_view_type:"inbox",wait_ms:6000}. The single navigate call should wait locally until the semantic inbox appears and include visible list+conversation records. Report READY, EXPECTED VIEW, OBSERVED VIEW, PROBES, LOCAL WAIT MS and record count from its result. Finish after this observation. This is a read-only benchmark on a local fixture.
'@
$args=@{requester='ChatGPT';chat='OpenFill krokowe wdrożenie OF-IMP';title='A12 semantic batch acceptance';idempotency_key='OFIMP-A17-ASYNC-NAV-20261101-01';wait_seconds=15;task=$instruction}
$body=@{jsonrpc='2.0';id=88103;method='tools/call';params=@{name='openfill_start_task';arguments=$args}}|ConvertTo-Json -Depth 12 -Compress
$r=Invoke-RestMethod -Uri $url -Method POST -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json; charset=utf-8' -TimeoutSec 30
$r.result.content[0].text
