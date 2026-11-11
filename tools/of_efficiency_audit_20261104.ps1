param(
    [string]$UsageLog = "$env:USERPROFILE\OpenFill\data\stable\logs\usage_202611.ndjson",
    [int]$Last = 100,
    [string]$Output = "C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill\docs\OPENFILL_EFFICIENCY_LIVE_20261104.md"
)
$ErrorActionPreference = 'Stop'
if (-not [IO.File]::Exists($UsageLog)) { throw "USAGE_LOG_MISSING: $UsageLog" }
$items = New-Object 'System.Collections.Generic.List[object]'
foreach ($line in (Get-Content -LiteralPath $UsageLog -ErrorAction Stop)) {
    if (-not $line.Contains('"kind":"task"')) { continue }
    try { $r = $line | ConvertFrom-Json -ErrorAction Stop; $items.Add($r) } catch { continue }
}
$items = @($items | Sort-Object ts | Select-Object -Last ([Math]::Max(1, [Math]::Min($Last, 2000))))
# Historical versions labelled GUI/MCP/maintenance cancellation identically as user stop
# and logged the same session as failed. Recover the fact of cancellation only.
$runIds = @{}
$dates = @{}
foreach ($item in $items) {
    $id = [string]$item.runId
    if ([string]::IsNullOrWhiteSpace($id)) { continue }
    $runIds[$id] = $true
    if ($id -match '^(20[0-9]{6})_') { $dates[$Matches[1]] = $true }
}
$legacyStops = @{}
$legacyCancels = @{}
foreach ($date in $dates.Keys) {
    $eventLog = Join-Path (Split-Path $UsageLog -Parent) ("app_" + $date + ".ndjson")
    if (-not [IO.File]::Exists($eventLog)) { continue }
    foreach ($line in (Get-Content -LiteralPath $eventLog -ErrorAction Stop)) {
        if (-not ($line.Contains('"eventType":"stop"') -or $line.Contains('"eventType":"cancel"'))) { continue }
        try {
            $ev = $line | ConvertFrom-Json -ErrorAction Stop
            $id = [string]$ev.runId
            if (-not $runIds.ContainsKey($id)) { continue }
            if ($ev.source -eq 'app' -and $ev.eventType -eq 'stop') { $legacyStops[$id] = $true }
            if ($ev.source -eq 'session' -and $ev.eventType -eq 'cancel') { $legacyCancels[$id] = $true }
        } catch { continue }
    }
}
function Category([string]$task) {
    if ($task -match '(?i)linkedin') { return 'LinkedIn' }
    if ($task -match '(?i)whatsapp') { return 'WhatsApp' }
    if ($task -match '(?i)gmail|outlook|mail|poczt') { return 'Mail' }
    if ($task -match '(?i)google|drive|sheets|calendar') { return 'Google' }
    if ($task -match '(?i)facebook|instagram|meta') { return 'Meta' }
    return 'Other'
}
function Median($values) {
    $a = @($values | Sort-Object)
    if ($a.Count -eq 0) { return $null }
    $m = [int][Math]::Floor($a.Count / 2)
    if ($a.Count % 2 -eq 1) { return [double]$a[$m] }
    return ([double]$a[$m - 1] + [double]$a[$m]) / 2
}
$intentional = @('user_stop','requester_cancel','upgrade_interrupted')
$rows = @($items | ForEach-Object {
    $runId = [string]$_.runId
    $originalOutcome = [string]$_.outcome
    $legacyCancelled = $originalOutcome -eq 'failed' -and
        $legacyStops.ContainsKey($runId) -and $legacyCancels.ContainsKey($runId) -and
        [string]::IsNullOrWhiteSpace([string]$_.stopReason)
    $reason = if ($_.stopReason) { [string]$_.stopReason }
              elseif ($legacyCancelled) { 'legacy_cancel_actor_unknown' }
              else { 'unknown_historical' }
    [pscustomobject]@{
        ts = [string]$_.ts
        recordedOutcome = $originalOutcome
        runId = $runId
        taskKey = [string]$_.taskKey
        category = Category ([string]$_.task)
        outcome = if ($legacyCancelled) { 'cancelled_legacy' } else { $originalOutcome }
        stopReason = $reason
        calls = [int]$_.calls
        toolErrors = [int]$_.toolErrors
        readRequests = [int]$_.pageReadRequests
        repeatedReads = [int]$_.repeatedPageReadRequests
        maxReadStreak = [int]$_.maxReadStreak
        planDomReads = [int]$_.planDomReads
        planActionAttempts = [int]$_.planActionAttempts
        mode = [string]$_.executionMode
        costUsd = [double]$_.costUsd
    }
})
$baselines = @{}
foreach ($group in ($rows | Where-Object {$_.outcome -eq 'success'} | Group-Object category)) {
    $sample = @($group.Group | ForEach-Object {$_.calls})
    if ($sample.Count -ge 3) { $baselines[$group.Name] = Median $sample }
}
$results = @($rows | ForEach-Object {
    $base = if ($baselines.ContainsKey($_.category)) { [double]$baselines[$_.category] } else { 0 }
    $ratio = if ($base -gt 0) { [Math]::Round($_.calls / $base, 1) } else { 0 }
    $notSuccessful = $_.outcome -in @('failed','partial','blocked') -and ($intentional -notcontains $_.stopReason)
    $high = $_.calls -ge 60
    $candidate = $notSuccessful -or $high -or $_.maxReadStreak -ge 12 -or $_.repeatedReads -ge 3
    $_ | Add-Member -NotePropertyName ratioToCategoryMedian -NotePropertyValue $ratio -PassThru |
        Add-Member -NotePropertyName reviewCandidate -NotePropertyValue $candidate -PassThru
})
$success = @($rows | Where-Object {$_.outcome -eq 'success'}).Count
$partial = @($rows | Where-Object {$_.outcome -eq 'partial'}).Count
$failed = @($rows | Where-Object {$_.outcome -eq 'failed'}).Count
$unknownOutcome = @($rows | Where-Object {[string]::IsNullOrWhiteSpace($_.outcome)}).Count
$interrupted = @($rows | Where-Object {$_.outcome -eq 'interrupted'}).Count
$userStop = @($rows | Where-Object {$_.stopReason -eq 'user_stop'}).Count
$legacyCancelledCount = @($rows | Where-Object {$_.outcome -eq 'cancelled_legacy'}).Count
$zeroCallCount = @($rows | Where-Object {$_.calls -eq 0}).Count
$earlyTerminationCount = @($rows | Where-Object {$_.stopReason -eq 'pre_runner_termination'}).Count
$modelExceptionCount = @($rows | Where-Object {$_.stopReason -eq 'model_exception'}).Count
$runtimeExceptionCount = @($rows | Where-Object {$_.stopReason -eq 'runtime_exception'}).Count
$cost = ($rows | Measure-Object costUsd -Sum).Sum
$calls = ($rows | Measure-Object calls -Sum).Sum
$review = @($results | Where-Object reviewCandidate)
$report = @(
    '# OpenFill: audyt rzeczywistych przebiegów',
    '',
    "Źródło: $UsageLog",
    "Rekordy task: $($rows.Count). Sukcesy: $success. Częściowe: $partial. Failed: $failed. Historyczne anulowania: $legacyCancelledCount. Interrupted: $interrupted. Bez outcome: $unknownOutcome. Jawny user_stop: $userStop.",
    "Wywołania modelu: $calls. Koszt według logów USD: $([Math]::Round($cost,4)). Kandydaci do przeglądu: $($review.Count).",
    "Zarejestrowane rekordy zero-call: $zeroCallCount. Przed AgentRunner: $earlyTerminationCount. Model exception: $modelExceptionCount. Runtime exception: $runtimeExceptionCount.",
    '',
    'cancelled_legacy oznacza jedynie parę zdarzeń app/stop i session/cancel w tym samym runId. Stara aplikacja błędnie opisywała każdy Stop jako żądanie użytkownika; faktyczny inicjator pozostaje nieustalony. Oryginalnych rekordów nie zmieniono.',
    'Status partial/failed nie dowodzi zbędnych operacji. Historyczne stopReason może być nieznane. Progi to selekcja do analizy, nie automatyczny stop.',
    'Median szerokich kategorii nie używamy do automatycznej klasyfikacji, gdyż zadania mają różny zakres.',
    '',
    '| Task | Kategoria | Wynik | Stop reason | Calls | Read req | Same reads | Read streak | Koszt USD |',
    '|---|---|---|---|---:|---:|---:|---:|---:|'
)
foreach ($x in ($review | Sort-Object calls -Descending | Select-Object -First 20)) {
    $report += "| $($x.taskKey) | $($x.category) | $($x.outcome) | $($x.stopReason) | $($x.calls) | $($x.readRequests) | $($x.repeatedReads) | $($x.maxReadStreak) | $([Math]::Round($x.costUsd,4)) |"
}
$report += ''
$report += 'Aktualna wersja telemetryki zapisuje ponadto executionMode, planDomReads i planActionAttempts. Brak tych pól w starszych rekordach oznacza brak pomiaru, nie zero czynności.'
[IO.File]::WriteAllLines($Output, [string[]]$report, [Text.UTF8Encoding]::new($false))
"REPORT_OK $Output tasks=$($rows.Count) review=$($review.Count) calls=$calls"
