# OpenFill - Metadata: wersja 0.1, data 2026-10-14 23:08
param(
    [string]$Path = "",
    [string]$ReportPath = ""
)

$ErrorActionPreference = "Stop"

if (-not $Path) {
    $dir = "C:\Users\pboru\OpenFill\data\stable\logs\telemetry"
    $latest = Get-ChildItem $dir -Filter "resources_*.ndjson" -File -ErrorAction Stop |
        Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    if (-not $latest) { throw "No resource telemetry file found in $dir" }
    $Path = $latest.FullName
}

if (-not (Test-Path -LiteralPath $Path)) { throw "Telemetry file not found: $Path" }

$rows = @(
    Get-Content -LiteralPath $Path |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        ForEach-Object {
            try { $_ | ConvertFrom-Json }
            catch { }
        }
)

if ($rows.Count -eq 0) { throw "No valid telemetry samples in $Path" }

function Num([object]$v) {
    if ($null -eq $v) { return $null }
    return [double]$v
}

function Stats([object[]]$items, [string]$field) {
    $vals = @($items | ForEach-Object { Num $_.$field } | Where-Object { $null -ne $_ })
    if ($vals.Count -eq 0) { return $null }
    $sorted = @($vals | Sort-Object)
    $p95Index = [Math]::Min($sorted.Count - 1, [Math]::Max(0, [Math]::Ceiling($sorted.Count * 0.95) - 1))
    [pscustomobject]@{
        Count = $vals.Count
        Avg = [Math]::Round(($vals | Measure-Object -Average).Average, 3)
        Min = [Math]::Round(($vals | Measure-Object -Minimum).Minimum, 3)
        Max = [Math]::Round(($vals | Measure-Object -Maximum).Maximum, 3)
        P95 = [Math]::Round($sorted[$p95Index], 3)
    }
}

function SlopePerHour([object[]]$items, [string]$field) {
    if ($items.Count -lt 2) { return $null }
    $points = @()
    $t0 = [DateTime]$items[0].tsUtc
    foreach ($r in $items) {
        $v = Num $r.$field
        if ($null -eq $v) { continue }
        $x = (([DateTime]$r.tsUtc) - $t0).TotalHours
        $points += [pscustomobject]@{ X = $x; Y = $v }
    }
    if ($points.Count -lt 2) { return $null }
    $sx = 0.0; $sy = 0.0; $sxx = 0.0; $sxy = 0.0
    foreach ($p in $points) {
        $sx += $p.X; $sy += $p.Y; $sxx += $p.X * $p.X; $sxy += $p.X * $p.Y
    }
    $n = [double]$points.Count
    $den = $n * $sxx - $sx * $sx
    if ([Math]::Abs($den) -lt 1e-12) { return $null }
    return [Math]::Round((($n * $sxy - $sx * $sy) / $den), 3)
}

$first = $rows[0]
$last = $rows[-1]
$firstTime = [DateTime]$first.tsUtc
$lastTime = [DateTime]$last.tsUtc
$duration = $lastTime - $firstTime
$active = @($rows | Where-Object { [int]$_.activeTasks -gt 0 })
$idle = @($rows | Where-Object { [int]$_.activeTasks -eq 0 })

$fields = @(
    "workingSetMb",
    "privateBytesMb",
    "cpuPctMachine",
    "processCount",
    "webViewProcessCount",
    "handles",
    "threads",
    "gcManagedMb",
    "gcHeapSizeMb",
    "gcCommittedMb"
)

$sb = [Text.StringBuilder]::new()
[void]$sb.AppendLine("OPENFILL RESOURCE TELEMETRY ANALYSIS")
[void]$sb.AppendLine("File: $Path")
[void]$sb.AppendLine("Samples: $($rows.Count)")
[void]$sb.AppendLine("From UTC: $($first.tsUtc)")
[void]$sb.AppendLine("To UTC: $($last.tsUtc)")
[void]$sb.AppendLine(("Duration: {0:hh\:mm\:ss}" -f $duration))
[void]$sb.AppendLine("Logical processors: $($last.logicalProcessors)")
[void]$sb.AppendLine("CPU scale: $($last.cpuScale)")
[void]$sb.AppendLine()

[void]$sb.AppendLine("LATEST")
foreach ($name in @("uptimeSeconds","activeTasks","waitingTasks","tabs","processCount","webViewProcessCount","workingSetMb","privateBytesMb","handles","threads","gcManagedMb","gcHeapSizeMb","gcCommittedMb","cpuPctMachine","cpuPctCoreSum")) {
    [void]$sb.AppendLine("$name = $($last.$name)")
}
[void]$sb.AppendLine()

[void]$sb.AppendLine("ALL SAMPLES")
foreach ($f in $fields) {
    $s = Stats $rows $f
    if ($null -ne $s) {
        $slopeText = if ($duration.TotalMinutes -ge 30) { SlopePerHour $rows $f } else { "n/a (<30 min)" }
        [void]$sb.AppendLine(("{0}: avg={1}, min={2}, max={3}, p95={4}, slope/hour={5}" -f
            $f, $s.Avg, $s.Min, $s.Max, $s.P95, $slopeText))
    }
}
[void]$sb.AppendLine()

[void]$sb.AppendLine("ACTIVE VS IDLE")
[void]$sb.AppendLine("active samples = $($active.Count)")
[void]$sb.AppendLine("idle samples = $($idle.Count)")
foreach ($f in @("workingSetMb","privateBytesMb","cpuPctMachine","processCount","webViewProcessCount","handles","threads")) {
    $a = Stats $active $f
    $i = Stats $idle $f
    $aText = if ($a) { "avg=$($a.Avg), max=$($a.Max)" } else { "n/a" }
    $iText = if ($i) { "avg=$($i.Avg), max=$($i.Max)" } else { "n/a" }
    [void]$sb.AppendLine(("{0}: active {{{1}}}; idle {{{2}}}" -f $f, $aText, $iText))
}
[void]$sb.AppendLine()

$procRows = foreach ($r in $rows) {
    foreach ($p in @($r.processes)) {
        [pscustomobject]@{
            Ts = $r.tsUtc
            Pid = [int]$p.pid
            Name = [string]$p.name
            StartUtc = [string]$p.startUtc
            WorkingSetMb = Num $p.workingSetMb
            PrivateBytesMb = Num $p.privateBytesMb
            CpuPctMachine = Num $p.cpuPctMachine
        }
    }
}

[void]$sb.AppendLine("TOP PROCESS INSTANCES BY MAX PRIVATE BYTES")
$top = $procRows |
    Group-Object Pid |
    ForEach-Object {
        $g = $_.Group
        [pscustomobject]@{
            Pid = [int]$_.Name
            Name = $g[0].Name
            StartUtc = $g[0].StartUtc
            Samples = $g.Count
            MaxPrivateMb = [Math]::Round(($g | Measure-Object PrivateBytesMb -Maximum).Maximum, 3)
            MaxWorkingSetMb = [Math]::Round(($g | Measure-Object WorkingSetMb -Maximum).Maximum, 3)
            MaxCpuMachinePct = [Math]::Round(($g | Where-Object {$null -ne $_.CpuPctMachine} | Measure-Object CpuPctMachine -Maximum).Maximum, 3)
        }
    } |
    Sort-Object MaxPrivateMb -Descending |
    Select-Object -First 12

[void]$sb.AppendLine(($top | Format-Table -AutoSize | Out-String).TrimEnd())

$report = $sb.ToString()
$report

if ($ReportPath) {
    $parent = Split-Path -Parent $ReportPath
    if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
    [IO.File]::WriteAllText($ReportPath, $report, [Text.UTF8Encoding]::new($false))
}
