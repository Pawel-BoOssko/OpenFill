param([Parameter(Mandatory=$true)][string]$RunId)
$ErrorActionPreference='Stop'
$path="C:\Users\pboru\OpenFill\data\dev\logs\runs\run_$RunId.ndjson"
$events=@(Get-Content -LiteralPath $path | ForEach-Object { $_ | ConvertFrom-Json })
$usage=@($events | Where-Object { $_.source -eq 'session' -and $_.eventType -eq 'usage' } | Select-Object -Last 1)
$calls=@($events | Where-Object { $_.source -eq 'tool' -and $_.eventType -eq 'call' })
$results=@($events | Where-Object { $_.source -eq 'tool' -and $_.eventType -eq 'result' })
$start=$events | Where-Object { $_.source -eq 'session' -and $_.eventType -eq 'start' } | Select-Object -First 1
$end=$events | Where-Object { $_.source -eq 'session' -and $_.eventType -eq 'end' } | Select-Object -Last 1
$names=@($calls | ForEach-Object { $_.data.name })
[pscustomobject]@{
 run_id=$RunId
 model_calls=$usage.data.totals.calls
 input_tokens=$usage.data.totals.inputTokens
 cached_tokens=$usage.data.totals.cachedTokens
 uncached_tokens=($usage.data.totals.inputTokens-$usage.data.totals.cachedTokens)
 output_tokens=$usage.data.totals.outputTokens
 model_ms=$usage.data.totals.modelMs
 elapsed_ms=[math]::Round(([datetime]$end.tsUtc-[datetime]$start.tsUtc).TotalMilliseconds)
 cost_usd=$usage.data.totals.costUsd
 tools=$names
 dom_probe_calls=@($names | Where-Object { $_ -in @('get_page','inspect_view','verify_view','page_capture','observe_query','read_view') }).Count
 full_dom_read_calls=@($names | Where-Object { $_ -in @('get_page','page_capture','observe_query') }).Count
 tool_response_bytes=($results | ForEach-Object { [int]$_.data.responseBytes } | Measure-Object -Sum).Sum
} | ConvertTo-Json -Depth 6
