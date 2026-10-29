# OpenFill monitor - Metadata
#   nazwa:  OpenFill_monitor
#   wersja: 0.4
#   data:   2026-10-29 22:35
#   autor:  Claude Bishop (dla Pawla Boruty-Osko)
#   cel:    co 10 minut zapisuje stan infrastruktury tej maszyny do logu NDJSON (internet, Wi-Fi, zasilanie, Tailscale/Funnel, OpenFill, MCP).
#           Nic nie naprawia i niczego nie zatrzymuje. Logi: %USERPROFILE%\OpenFill\monitor\logs, ostatni stan: ...\monitor\status.json
param([switch]$TestMode)
$ErrorActionPreference = 'SilentlyContinue'
$ProgressPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$sw0 = [Diagnostics.Stopwatch]::StartNew()
$inv = [Globalization.CultureInfo]::InvariantCulture
$home0 = Join-Path $env:USERPROFILE 'OpenFill'
$base = Join-Path $home0 'monitor'
if ($TestMode) { $base = Join-Path $base 'test-v04' }
$logDir = Join-Path $base 'logs'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$statusFile = Join-Path $base 'status.json'
$eventsFile = Join-Path $logDir 'events.ndjson'
$now = (Get-Date).ToUniversalTime()
$prev = $null
if (Test-Path $statusFile) { try { $prev = Get-Content $statusFile -Raw | ConvertFrom-Json } catch {} }
$sinceUtc = $now.AddMinutes(-12)
if ($prev -and $prev.tsUtc) { try { $sinceUtc = [DateTimeOffset]::Parse($prev.tsUtc, $inv).UtcDateTime } catch {} }
if (($now - $sinceUtc).TotalHours -gt 24) { $sinceUtc = $now.AddHours(-24) }
$sinceLocal = $sinceUtc.ToLocalTime()

function PingMs($h) { try { $p = New-Object Net.NetworkInformation.Ping; $r = $p.Send($h, 2000); if ($r.Status -eq 'Success') { return [int]$r.RoundtripTime } else { return -1 } } catch { return -1 } }
function HttpProbe($url, $to) {
    $sw = [Diagnostics.Stopwatch]::StartNew(); $code = 0; $err = $null; $date = $null
    try {
        $rq = [Net.HttpWebRequest]::Create($url); $rq.Timeout = $to; $rq.ReadWriteTimeout = $to; $rq.Method = 'GET'; $rq.AllowAutoRedirect = $false; $rq.UserAgent = 'OpenFillMonitor/0.1'
        $rs = $rq.GetResponse(); $code = [int]$rs.StatusCode; $date = $rs.Headers['Date']; $rs.Close()
    } catch [Net.WebException] {
        if ($_.Exception.Response) { $code = [int]$_.Exception.Response.StatusCode; $date = $_.Exception.Response.Headers['Date']; $_.Exception.Response.Close() } else { $err = $_.Exception.Status.ToString() }
    } catch { $err = $_.Exception.Message }
    return @{ url = $url; code = $code; ms = [int]$sw.ElapsedMilliseconds; err = $err; date = $date }
}

# ---- internet
$net = @{}
$gw = (Get-NetRoute -DestinationPrefix '0.0.0.0/0' | Sort-Object RouteMetric | Select-Object -First 1).NextHop
$net.gateway = [string]$gw
$net.pingGw = if ($gw) { PingMs $gw } else { -1 }
$net.ping1111 = PingMs '1.1.1.1'
$net.ping8888 = PingMs '8.8.8.8'
$sd = [Diagnostics.Stopwatch]::StartNew()
$dnsOk = $false; try { $dnsOk = [bool](Resolve-DnsName 'api.openai.com' -QuickTimeout -ErrorAction Stop) } catch {}
$net.dnsOk = $dnsOk; $net.dnsMs = [int]$sd.ElapsedMilliseconds
$g = HttpProbe 'http://www.gstatic.com/generate_204' 8000
$net.httpCode = $g.code; $net.httpMs = $g.ms; $net.httpErr = $g.err
$o = HttpProbe 'https://api.openai.com/' 8000
$net.openaiCode = $o.code; $net.openaiMs = $o.ms; $net.openaiErr = $o.err
$skew = $null
if ($g.date) { try { $skew = [int](($now - [DateTimeOffset]::Parse($g.date, $inv).UtcDateTime).TotalSeconds) } catch {} }
$net.clockSkewSec = $skew

# ---- Wi-Fi i adaptery
$wifi = @{ ssid = $null; signal = $null; state = $null }
foreach ($l in @(netsh wlan show interfaces 2>$null)) {
    if ($l -match '^\s*SSID\s*:\s*(.+)$' -and -not $wifi.ssid) { $wifi.ssid = $Matches[1].Trim() }
    elseif ($l -match '^\s*(Signal|Sygna\S*)\s*:\s*(\d+)%') { $wifi.signal = [int]$Matches[2] }
    elseif ($l -match '^\s*(State|Stan)\s*:\s*(.+)$') { $wifi.state = $Matches[2].Trim() }
}
$wifi.adapters = (@(Get-NetAdapter | Where-Object { $_.InterfaceDescription -notmatch 'VirtualBox|Bluetooth|Hyper-V|VMware' } | ForEach-Object { '{0}={1}' -f $_.Name, $_.Status }) -join ';')
$wifi.flapsSinceLast = @(Get-WinEvent -FilterHashtable @{ LogName = 'Microsoft-Windows-WLAN-AutoConfig/Operational'; StartTime = $sinceLocal; Id = 11004, 8003, 8002 } -MaxEvents 300).Count

# ---- zasilanie i uspienie
$pw = @{}
$bat = Get-CimInstance Win32_Battery | Select-Object -First 1
if ($bat) { $pw.hasBattery = $true; $pw.onAC = ($bat.BatteryStatus -in 2, 3, 6, 7, 8, 9, 11); $pw.chargePct = [int]$bat.EstimatedChargeRemaining } else { $pw.hasBattery = $false; $pw.onAC = $true }
$pe = @()
$pe += @(Get-WinEvent -FilterHashtable @{ LogName = 'System'; StartTime = $sinceLocal; Id = 41, 42, 107, 1074, 6008 } | Where-Object { $_.ProviderName -match 'Kernel-Power|User32|EventLog' } | ForEach-Object { '{0}#{1}' -f $_.TimeCreated.ToString('HH:mm'), $_.Id })
$pe += @(Get-WinEvent -FilterHashtable @{ LogName = 'System'; StartTime = $sinceLocal; ProviderName = 'Microsoft-Windows-Power-Troubleshooter'; Id = 1 } | ForEach-Object { '{0}#wake' -f $_.TimeCreated.ToString('HH:mm') })
$pw.eventsSinceLast = @($pe | Select-Object -First 8)

# ---- Tailscale
$ts = @{ installed = $false }
$tsExe = 'C:\Program Files\Tailscale\tailscale.exe'
if (Test-Path $tsExe) {
    $ts.installed = $true
    $ts.service = [string](Get-Service Tailscale).Status
    try {
        $j = (& $tsExe status --json 2>$null | Out-String) | ConvertFrom-Json
        $ts.backend = [string]$j.BackendState; $ts.selfOnline = [bool]$j.Self.Online; $ts.host = [string]$j.Self.HostName
        $ts.peers = @($j.Peer.PSObject.Properties | Where-Object { $_.Value.HostName -ne 'funnel-ingress-node' } | ForEach-Object { '{0}={1}' -f $_.Value.HostName, $(if ($_.Value.Online) { 'online' } else { 'offline' }) })
    } catch { $ts.backend = 'unparsable' }
    $fs = & $tsExe funnel status 2>$null | Out-String
    $m = [regex]::Match($fs, 'https://[A-Za-z0-9\.\-]+\.ts\.net(?=\s|$)')
    if ($m.Success) { $h = HttpProbe $m.Value 10000; $ts.funnelUrl = $m.Value; $ts.funnelCode = $h.code; $ts.funnelMs = $h.ms; $ts.funnelErr = $h.err }
}

# ---- OpenFill i MCP
$of = @{ installed = (Test-Path (Join-Path $home0 'app\OpenFill.exe')) }
if ($of.installed) {
    $procs = @(Get-Process | Where-Object { $_.Path -like '*\OpenFill\app\OpenFill.exe' })
    if ($procs.Count -gt 0) {
        $p = $procs[0]; $of.running = $true; $of.pid = $p.Id; $of.start = $p.StartTime.ToUniversalTime().ToString('o'); $of.responding = [bool]$p.Responding; $of.wsMB = [int]($p.WorkingSet64 / 1MB)
    } else { $of.running = $false }
    $wv = @(Get-Process -Name msedgewebview2)
    $of.webview2 = $wv.Count; $of.webview2MB = [int](($wv | Measure-Object WorkingSet64 -Sum).Sum / 1MB)
    $of.portListening = [bool](Get-NetTCPConnection -LocalPort 48731 -State Listen)
    # OpenFill calls OpenRouter TypeSafe directly from PBOX1. Never query the
    # obsolete SATANAMD JEV relay or report its health as OpenFill health.
    $of.jevMode = 'direct_openrouter'
    $of.jevConfigured = Test-Path (Join-Path $home0 'data\stable\runtime\jev-api-key.bin')
    $of.jevHealthy = $null    # no authenticated model scoring in the monitor
    $of.jevApiEndpoint = 'https://openrouter.ai/api/alpha/decisions'
    $j = HttpProbe $of.jevApiEndpoint 5500
    $of.jevCode = [int]$j.code
    $of.jevMs = [int]$j.ms
    $of.jevErr = $j.err
    $of.jevApiReachable = ($of.jevCode -gt 0)  # 4xx also means HTTPS answered; NOT a successful JEV score
    $of.mcpOk = $false
    try {
        $u = (Get-Content (Join-Path $home0 'data\stable\runtime\mcp-url.txt') -Raw).Trim()
        $local = 'http://127.0.0.1:48731' + ([uri]$u).AbsolutePath
        $body = @{ jsonrpc = '2.0'; id = 1; method = 'tools/call'; params = @{ name = 'openfill_info'; arguments = @{} } } | ConvertTo-Json -Depth 8
        $t0 = [Diagnostics.Stopwatch]::StartNew()
        $r = Invoke-RestMethod -Uri $local -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 15 -ErrorAction Stop
        $of.mcpMs = [int]$t0.ElapsedMilliseconds
        $info = ($r.result.content | Where-Object type -eq 'text' | Select-Object -First 1).text | ConvertFrom-Json
        # OF-243: openfill_info is compact and contains ONLY the working array.
        if ($null -eq $info -or @($info.PSObject.Properties.Name) -notcontains 'working') { throw 'MCP_COMPACT_INFO_CONTRACT_MISSING_WORKING' }
        $of.mcpOk = $true
        $of.version = [string](Get-Item (Join-Path $home0 'app\OpenFill.exe')).VersionInfo.ProductVersion
        $of.uptimeMin = if ($p) { [int]((Get-Date) - $p.StartTime).TotalMinutes } else { 0 }
        $tasks = @($info.working | Where-Object { $null -ne $_ })
        $of.activeTasks = $tasks.Count
        # Compact MCP deliberately does not reveal task start time or identity.
        $of.oldestTaskMin = $null
        $of.taskAgeAvailable = $false
    } catch { $of.mcpErr = $_.Exception.Message }
    $lf = Get-ChildItem (Join-Path $home0 'data\stable\logs') -Filter 'app_*.ndjson' | Sort-Object LastWriteTime | Select-Object -Last 1
    if ($lf) { $of.logAgeMin = [int]((Get-Date) - $lf.LastWriteTime).TotalMinutes
    try {
        $errN = 0; $e429 = 0; $panelDied = 0
        foreach ($ln in (Get-Content $lf.FullName -Tail 4000)) {
            if ($ln -notmatch '"tsUtc":"([^"]+)"') { continue }
            $t = [DateTimeOffset]::Parse($Matches[1], $inv).UtcDateTime
            if ($t -lt $sinceUtc) { continue }
            if ($ln -match '"status":"error"' -and $ln -match '"source":"(app|mcp|model|panel|license)"') { $errN++ }
            if ($ln -match '"source":"model"' -and $ln -match 'HTTP 429|status.{0,3}429|rate.?limit|Too Many Requests') { $e429++ }
            if ($ln -match 'UtilityProcessExited|Panel process failed') { $panelDied++ }
        }
        $of.logErrorsSinceLast = $errN; $of.model429SinceLast = $e429; $of.panelDiedSinceLast = $panelDied
    } catch {}    }
}

# ---- reszta (zdalny dostep, multihost) i zasoby
$oth = @{}
$oth.rustdesk = [bool](Get-Process -Name rustdesk)
$oth.lupus = @(Get-Process | Where-Object { $_.Path -match 'Lupus' } | ForEach-Object { $_.ProcessName } | Sort-Object -Unique)
$os = Get-CimInstance Win32_OperatingSystem
$sys = @{
    host = $env:COMPUTERNAME
    bootUtc = $os.LastBootUpTime.ToUniversalTime().ToString('o')
    uptimeH = [math]::Round(((Get-Date) - $os.LastBootUpTime).TotalHours, 1)
    ramFreeGB = [math]::Round($os.FreePhysicalMemory / 1MB, 1)
    ramTotalGB = [math]::Round($os.TotalVisibleMemorySize / 1MB, 1)
    cpuPct = [int](Get-CimInstance Win32_Processor | Measure-Object LoadPercentage -Average).Average
    diskCFreeGB = [math]::Round((Get-PSDrive C).Free / 1GB, 1)
}

$sys.rebootPending = [bool]((Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired') -or (Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending'))
$sys.powerPlanAC = (powercfg /query SCHEME_CURRENT SUB_SLEEP STANDBYIDLE 2>$null | Select-String 'Current AC Power Setting Index: (0x[0-9a-f]+)' | ForEach-Object { $_.Matches[0].Groups[1].Value })
$sys.powerPlanDC = (powercfg /query SCHEME_CURRENT SUB_SLEEP STANDBYIDLE 2>$null | Select-String 'Current DC Power Setting Index: (0x[0-9a-f]+)' | ForEach-Object { $_.Matches[0].Groups[1].Value })
# ---- ocena
$problems = New-Object System.Collections.Generic.List[string]
if ($net.ping1111 -lt 0 -and $net.ping8888 -lt 0 -and $net.httpCode -eq 0) { $problems.Add('internet_down') }
elseif (-not $net.dnsOk) { $problems.Add('dns_fail') }
if ($net.pingGw -lt 0 -and $gw) { $problems.Add('gateway_unreachable') }
if ($null -ne $net.clockSkewSec -and [math]::Abs($net.clockSkewSec) -gt 30) { $problems.Add('clock_skew') }
if ($wifi.flapsSinceLast -ge 3) { $problems.Add('wifi_flaps') }
if ($wifi.signal -ne $null -and $wifi.signal -lt 35) { $problems.Add('wifi_weak') }
if ($pw.hasBattery -and -not $pw.onAC) { $problems.Add('on_battery') }
if ($pw.eventsSinceLast.Count -gt 0) { $problems.Add('power_or_restart_event') }
if ($ts.installed) {
    if ($ts.service -ne 'Running') { $problems.Add('tailscale_service_down') }
    elseif ($ts.backend -ne 'Running' -or -not $ts.selfOnline) { $problems.Add('tailscale_offline') }
    if ($ts.funnelUrl -and $ts.funnelCode -eq 0) { $problems.Add('funnel_unreachable') }
}
if ($of.installed) {
    if (-not $of.running) { $problems.Add('openfill_not_running') }
    elseif (-not $of.responding) { $problems.Add('openfill_hung') }
    elseif (-not $of.portListening) { $problems.Add('mcp_port_closed') }
    elseif (-not $of.mcpOk) { $problems.Add('mcp_not_answering') }
    if (-not $of.jevConfigured) { $problems.Add('jev_not_configured') }
    elseif (-not $of.jevApiReachable) { $problems.Add('jev_api_unreachable') }
    if ($of.taskAgeAvailable -and $of.oldestTaskMin -gt 120) { $problems.Add('task_over_2h') }
    if ($of.panelDiedSinceLast -gt 0) { $problems.Add('openfill_panel_crash') }
    if ($of.model429SinceLast -ge 3) { $problems.Add('model_429') }
}
if ($sys.rebootPending) { $problems.Add('windows_reboot_pending') }
if ($sys.diskCFreeGB -lt 5) { $problems.Add('disk_low') }
if ($sys.ramFreeGB -lt 0.4) { $problems.Add('ram_low') }

# ---- zdarzenia (zmiany stanu)
$sinceMap = @{}
if ($prev -and $prev.since) { $prev.since.PSObject.Properties | ForEach-Object { $sinceMap[$_.Name] = $_.Value } }
$prevProblems = @(); if ($prev -and $prev.problems) { $prevProblems = @($prev.problems) }
$nowIso = $now.ToString('o')
function WriteEvent($o) { ($o | ConvertTo-Json -Compress -Depth 4) | Out-File -FilePath $eventsFile -Append -Encoding utf8 }
foreach ($pr in $problems) { if ($prevProblems -notcontains $pr) { $sinceMap[$pr] = $nowIso; WriteEvent @{ tsUtc = $nowIso; host = $env:COMPUTERNAME; event = 'problem_start'; problem = $pr } } }
foreach ($pr in $prevProblems) {
    if ($problems -notcontains $pr) {
        $dur = $null; if ($sinceMap[$pr]) { try { $dur = [int](($now - [DateTimeOffset]::Parse($sinceMap[$pr], $inv).UtcDateTime).TotalMinutes) } catch {} }
        WriteEvent @{ tsUtc = $nowIso; host = $env:COMPUTERNAME; event = 'problem_end'; problem = $pr; durationMin = $dur }
        $sinceMap.Remove($pr)
    }
}
if ($prev -and $prev.tsUtc -and (($now - $sinceUtc).TotalMinutes -gt 25)) { WriteEvent @{ tsUtc = $nowIso; host = $env:COMPUTERNAME; event = 'monitor_gap'; minutes = [int]($now - $sinceUtc).TotalMinutes; note = 'brak pomiarow: maszyna wylaczona, uspiona, wylogowana albo zadanie nie dzialalo' } }

$rec = [ordered]@{
    tsUtc = $nowIso; host = $env:COMPUTERNAME; ok = ($problems.Count -eq 0); problems = @($problems); since = $sinceMap
    net = $net; wifi = $wifi; power = $pw; tailscale = $ts; openfill = $of; other = $oth; sys = $sys; runtimeMs = [int]$sw0.ElapsedMilliseconds
}
($rec | ConvertTo-Json -Compress -Depth 6) | Out-File -FilePath (Join-Path $logDir ('mon_' + $now.ToString('yyyyMMdd') + '.ndjson')) -Append -Encoding utf8
($rec | ConvertTo-Json -Depth 6) | Set-Content -Path $statusFile -Encoding UTF8
Get-ChildItem $logDir -Filter 'mon_*.ndjson' | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-30) } | Remove-Item -Force
if ($TestMode) {
    'TEST_STATUS_FILE=' + $statusFile
    'TEST_MCP_OK=' + $of.mcpOk
    'TEST_ACTIVE_TASKS=' + $of.activeTasks
    'TEST_VERSION=' + $of.version
    'TEST_UPTIME_MIN=' + $of.uptimeMin
    'TEST_JEV_KEY_CONFIGURED=' + $of.jevConfigured
    'TEST_JEV_API_REACHABLE=' + $of.jevApiReachable
    'TEST_JEV_HTTP=' + $of.jevCode
    'TEST_PROBLEM_COUNT=' + $problems.Count
}


