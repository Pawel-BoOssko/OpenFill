# OF-029/168/184: static manifest-to-source and Technology Map checks. PowerShell 5.1.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $root 'docs\openfill_technology_manifest__v0.1__2026-10-17_1616.json'
$mapPath = Join-Path $root 'docs\openfill_technology_map__v0.1__2026-10-18_ChatGPT.md'
$m = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$topics = @($m.topics)
$categories = @($topics | Select-Object -ExpandProperty category -Unique)
$names = @($topics | ForEach-Object { ($_.category + '/' + $_.name) })
$sourcePath = Join-Path $root $m.source
$sourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash
$map = Get-Content -LiteralPath $mapPath -Raw
$checks = @(
    @('manifest schema version 1', ($m.schemaVersion -eq 1)),
    @('exactly 41 topics', ($topics.Count -eq 41 -and $m.topicCount -eq $topics.Count)),
    @('exactly 7 categories', ($categories.Count -eq 7)),
    @('topic names unique by category', ((@($names | Select-Object -Unique)).Count -eq $names.Count)),
    @('technology and component fields provided', (@($topics | Where-Object { [string]::IsNullOrWhiteSpace($_.technology) -or [string]::IsNullOrWhiteSpace($_.component) }).Count -eq 0)),
    @('source file hash matches manifest', ($sourceHash -eq $m.sourceSha256)),
    @('dynamic version and PID declared', ($m.dynamicFields -contains 'BuildInfo.Version' -and $m.dynamicFields -contains 'Environment.ProcessId')),
    @('Technology Map references canonical manifest', ($map.Contains('openfill_technology_manifest__v0.1__2026-10-17_1616.json'))),
    @('Technology Map includes all seven areas', ((@($categories | ForEach-Object { $_.Substring(0,2) } | Select-Object -Unique)).Count -eq 7))
)
foreach ($test in $checks) {
    if (-not $test[1]) { throw ('FAIL: ' + $test[0]) }
    Write-Output ('PASS: ' + $test[0])
}
Write-Output ('TOTAL: ' + $checks.Count + ' PASS, 0 FAIL')