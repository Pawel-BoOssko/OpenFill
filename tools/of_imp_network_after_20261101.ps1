$ErrorActionPreference='Stop'
$secret=[IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt').Trim()
$url='http://127.0.0.1:48732/mcp/'+$secret
$args=@{requester='ChatGPT';chat='OpenFill krokowe wdrożenie OF-IMP';title='A11 identical network task after locator+controls';idempotency_key='OFIMP-A11-REALISTIC-NETWORK-AFTER-20261101-01';wait_seconds=15;task='STAGING local multi-stage QA part 2. Only use local http://127.0.0.1:18877/network.html. Navigate with view_mode=structure and call read_view with NO arguments. Confirm semantic list and visible conversation are returned. Use the search UI to locate a professional profile containing Jordan Hill, inspect the profile, send EXACTLY one LOCAL test message OFIMP local benchmark B with act and selective verification. Observe no separate full get_page after the final send, then finish.'}
$body=@{jsonrpc='2.0';id=88102;method='tools/call';params=@{name='openfill_start_task';arguments=$args}} | ConvertTo-Json -Depth 12 -Compress
$r=Invoke-RestMethod -Uri $url -Method Post -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json; charset=utf-8' -TimeoutSec 30
$r.result.content[0].text
