<#
  OpenFill - package release: file versions, Metadata, app version, INDEX, zip.
  Metadata:
    wersja: 0.5
    data:   2026-10-05 15:15
  Rule: every file has a version in its name (Name__v{X.Y}__{YYYY-MM-DD}_{HHMM}.ext) and in its content (Metadata).
  Only files that changed since the previous release get a new version (content hashes are compared
  without the metadata itself). Exceptions: names required by tools (Directory.Build.props, .gitignore)
  - have Metadata in the content and are marked in the INDEX.
  Usage:
    pwsh tools/release.ps1                  # assign versions, update the INDEX
    pwsh tools/release.ps1 -Zip             # as above + a zip package in dist/
    pwsh tools/release.ps1 -Check           # audit only (changes nothing); exit code = number of problems
    -Version 0.2 -Stamp 2026-10-05_0930     # explicit package version and timestamp
#>
param(
    [string]$Version = "",
    [string]$Stamp = "",
    [switch]$Zip,
    [switch]$Check
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

$MarkerRx = '__v(\d+(?:\.\d+)*)__(\d{4}-\d{2}-\d{2})_(\d{4})'
$Mandated = @("Directory.Build.props", ".gitignore")
$Binary = @(".png", ".jpg", ".jpeg", ".ico", ".gif", ".zip", ".dll", ".exe")

function Get-Files {
    $git = Get-Command git -ErrorAction SilentlyContinue
    if ($git -and (Test-Path (Join-Path $Root ".git"))) {
        $list = & git -C $Root ls-files --cached --others --exclude-standard
        return @($list | Where-Object { $_ -and (Test-Path (Join-Path $Root $_)) } | ForEach-Object { $_.Replace('\', '/') })
    }
    return @(Get-ChildItem -Path $Root -Recurse -File -Force | Where-Object { $_.FullName -notmatch '[\\/](bin|obj|\.git|dist)[\\/]' } |
        ForEach-Object { $_.FullName.Substring($Root.Length + 1).Replace('\', '/') })
}

function Split-Marker([string]$name) {
    # Returns @{ Stem; Ext; Version; Date; Time } for a file name (without the directory).
    $ext = [IO.Path]::GetExtension($name)
    $base = $name.Substring(0, $name.Length - $ext.Length)
    if ($name.StartsWith(".") -and $ext -eq $name) { $ext = ""; $base = $name }
    $m = [regex]::Match($base, $MarkerRx + '$')
    if ($m.Success) {
        return @{ Stem = $base.Substring(0, $m.Index); Ext = $ext; Version = $m.Groups[1].Value; Date = $m.Groups[2].Value; Time = $m.Groups[3].Value }
    }
    return @{ Stem = $base; Ext = $ext; Version = ""; Date = ""; Time = "" }
}

function Read-Text([string]$path) {
    $bytes = [IO.File]::ReadAllBytes($path)
    $bom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    $text = [Text.Encoding]::UTF8.GetString($bytes)
    if ($bom) { $text = $text.Substring(1) }
    return @{ Text = $text; Bom = $bom }
}

function Write-Text([string]$path, [string]$text, [bool]$bom) {
    $enc = New-Object System.Text.UTF8Encoding($bom)
    [IO.File]::WriteAllText($path, $text, $enc)
}

function Get-Hash([string]$text) {
    $sha = [Security.Cryptography.SHA256]::Create()
    $h = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))
    return (($h | ForEach-Object { $_.ToString("x2") }) -join "")
}

function Get-FileHashHex([string]$path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    $h = $sha.ComputeHash([IO.File]::ReadAllBytes($path))
    return (($h | ForEach-Object { $_.ToString("x2") }) -join "")
}

# Content without the metadata values - so that stamping alone does not count as a change.
function Normalize([string]$text) {
    $t = $text -replace '(?m)^(\s*(?:\*|#|//|rem)?\s*wersja:\s*)\S+', '$1#'
    $t = $t -replace '(?m)^(\s*(?:\*|#|//|rem)?\s*data:\s*)\d{4}-\d{2}-\d{2}(?: \d{2}:\d{2})?', '$1#'
    $t = $t -replace 'Metadata: wersja [\d.]+, data \d{4}-\d{2}-\d{2}(?: \d{2}:\d{2})?', 'Metadata: #'
    $t = $t -replace '(?m)^- Document version: .*$', '- Document version: #'
    $t = $t -replace '(?m)^- Date: .*$', '- Date: #'
    $t = $t -replace '<OpenFillVersion>[^<]*</OpenFillVersion>', '<OpenFillVersion>#</OpenFillVersion>'
    $t = $t -replace '<OpenFillVersionDate>[^<]*</OpenFillVersionDate>', '<OpenFillVersionDate>#</OpenFillVersionDate>'
    return $t
}

# Version declared in the content (first occurrence), or "".
function Get-DeclaredVersion([string]$text) {
    $head = $text.Substring(0, [Math]::Min($text.Length, 4000))
    $m = [regex]::Match($head, '(?m)^\s*(?:\*|#|//|rem)?\s*wersja:\s*([\d.]+)')
    if ($m.Success) { return $m.Groups[1].Value }
    $m = [regex]::Match($head, 'Metadata: wersja ([\d.]+)')
    if ($m.Success) { return $m.Groups[1].Value }
    $m = [regex]::Match($head, '(?m)^- Document version: ([\d.]+)')
    if ($m.Success) { return $m.Groups[1].Value }
    return ""
}

function Get-DeclaredDate([string]$text) {
    $head = $text.Substring(0, [Math]::Min($text.Length, 4000))
    $m = [regex]::Match($head, '(?m)^\s*(?:\*|#|//|rem)?\s*data:\s*(\d{4}-\d{2}-\d{2}(?: \d{2}:\d{2})?)')
    if ($m.Success) { return $m.Groups[1].Value }
    $m = [regex]::Match($head, 'Metadata: wersja [\d.]+, data (\d{4}-\d{2}-\d{2}(?: \d{2}:\d{2})?)')
    if ($m.Success) { return $m.Groups[1].Value }
    $m = [regex]::Match($head, '(?m)^- Date: (\d{4}-\d{2}-\d{2}(?: \d{2}:\d{2})?)')
    if ($m.Success) { return $m.Groups[1].Value }
    return ""
}

# Inserts or updates Metadata in the content. Returns the new text.
function Set-Metadata([string]$text, [string]$ext, [string]$ver, [string]$display) {
    $head = $text.Substring(0, [Math]::Min($text.Length, 4000))
    $tail = $text.Substring($head.Length)
    $found = $false
    if ($head -match '(?m)^\s*(?:\*|#|//|rem)?\s*wersja:\s*[\d.]+') {
        $head = [regex]::Replace($head, '(?m)^(\s*(?:\*|#|//|rem)?\s*wersja:\s*)[\d.]+', ('${1}' + $ver), 1)
        $head = [regex]::Replace($head, '(?m)^(\s*(?:\*|#|//|rem)?\s*data:\s*)\d{4}-\d{2}-\d{2}(?: \d{2}:\d{2})?', ('${1}' + $display), 1)
        $found = $true
    }
    elseif ($head -match 'Metadata: wersja [\d.]+, data \d{4}-\d{2}-\d{2}') {
        $head = [regex]::Replace($head, 'Metadata: wersja [\d.]+, data \d{4}-\d{2}-\d{2}(?: \d{2}:\d{2})?', ('Metadata: wersja ' + $ver + ', data ' + $display), 1)
        $found = $true
    }
    elseif ($head -match '(?m)^- Document version: ') {
        $head = [regex]::Replace($head, '(?m)^- Document version: .*$', ('- Document version: ' + $ver), 1)
        $head = [regex]::Replace($head, '(?m)^- Date: .*$', ('- Date: ' + $display), 1)
        $found = $true
    }
    if ($found) { return $head + $tail }

    $line = "Metadata: wersja $ver, data $display"
    $nl = "`n"
    if ($text.Contains("`r`n")) { $nl = "`r`n" }
    switch ($ext.ToLowerInvariant()) {
        { $_ -in ".cs", ".js", ".ts" } { return "// OpenFill - $line$nl" + $text }
        { $_ -in ".ps1", ".gitignore", ".sln", "" } {
            if ($ext -eq ".sln") {
                # After the header and the "# Visual Studio Version" line, so as not to disturb VS version detection.
                $lines = $text -split "`r?`n", 4
                if ($lines.Count -ge 4) { return $lines[0] + $nl + $lines[1] + $nl + $lines[2] + $nl + "# OpenFill - $line" + $nl + $lines[3] }
            }
            return "# OpenFill - $line$nl" + $text
        }
        ".cmd" { return "rem OpenFill - $line$nl" + $text }
        { $_ -in ".csproj", ".props", ".xml", ".html", ".htm" } {
            if ($text.StartsWith("<!doctype", [StringComparison]::OrdinalIgnoreCase)) {
                $i = $text.IndexOf(">") + 1
                return $text.Substring(0, $i) + "$nl<!-- OpenFill - $line -->" + $text.Substring($i)
            }
            return "<!-- OpenFill - $line -->$nl" + $text
        }
        ".md" { return $text -replace '^(# [^\r\n]*\r?\n)', ('$1' + "$nl## Metadata$nl- Document version: $ver$nl- Date: $display$nl") }
        default { return $text }
    }
}

function Bump([string]$v) {
    $parts = $v.Split(".")
    $major = [int]$parts[0]; $minor = 0
    if ($parts.Count -gt 1) { $minor = [int]$parts[1] }
    return "$major.$($minor + 1)"
}

function Max-Version([string]$a, [string]$b) {
    if (-not $a) { return $b }; if (-not $b) { return $a }
    if ([version]($a + ".0") -ge [version]($b + ".0")) { return $a } else { return $b }
}

$Descriptions = @{
    "README" = "app description, installation, usage, data, settings, architecture, development"
    "OpenFill" = "solution file (Visual Studio)"
    "Directory.Build.props" = "shared build settings; app version and version date"
    ".gitignore" = "files ignored by git"
    "install" = "installer: .NET 10, WebView2, build, key, shortcuts"
    "run-dev" = "run the development version (dev instance)"
    "release" = "release: file versions, Metadata, INDEX, zip, audit"
    "CompileCheck" = "compile check of the Windows app outside Windows"
    "AgentRunner" = "model loop (Responses API), limits, history compaction, images"
    "IRunHost" = "interface between the MCP layer and the app: start/stop a task, route questions to the MCP caller"
    "McpTaskManager" = "MCP tasks: one task at a time, task ids, status/reply/cancel, saved to disk"
    "McpServer" = "MCP server over HTTP (hand-written JSON-RPC, strict tool schemas, secret path)"
    "McpService" = "starts MCP: secret address, server, tunnel, self-test"
    "CloudflareTunnel" = "temporary public HTTPS address for MCP (Cloudflare quick tunnel)"
    "McpTests" = "tests of the MCP layer (task life cycle and HTTP server)"
    "HistoryStore" = "task history: saved list of tasks (panel and MCP) with results, and a reader for the steps of one task"
    "ToolRegistry" = "the model's internal tools"
    "AppHost" = "shared core of the app: panel <-> session, question and consent cards, tasks"
    "OpenFillSession" = "session: log, browser, notes, task start"
    "IPanelTransport" = "panel <-> core channel (WebSocket or WebView2)"
    "BrowserController" = "page control through CDP: navigation, extraction, actions, real events, files, screenshots"
    "NetworkMonitor" = "network traffic framed by requestId, response bodies, panel entries"
    "ConsoleMonitor" = "page console and JavaScript errors"
    "PageModel" = "text picture of the page for the model and differences after an action"
    "extractor" = "JS: logical page structure (forms, fields, buttons, options, dialogs, messages)"
    "actions" = "JS: page actions (set/type/click/select...)"
    "panel" = "live panel (HTML) - shared by the app and the CLI"
    "AssetLoader" = "loading embedded JS/HTML resources"
    "EventLog" = "NDJSON log (global, per task, network) and the stream to the panel"
    "Redactor" = "removing secrets from logs and notes"
    "OutputLimiter" = "tool output limit and overflow to a file"
    "AppConfig" = "settings (config.json)"
    "AppPaths" = "instance directories (stable/dev)"
    "SecretStore" = "OpenAI key (variable or encrypted file)"
    "OpenAIClient" = "OpenAI Responses API client"
    "KeyedModelClient" = "model client that reads the key at call time"
    "SiteNotesStore" = "notes about sites (local; a central database later)"
    "GapRecorder" = "gap reports from the model"
    "IUserInteraction" = "questions and consent (interface, unattended variant)"
    "Tool" = "tool and result definition"
    "ICdpConnection" = "CDP channel abstraction"
    "CdpSession" = "CDP calls on a tab (evaluate, call)"
    "WebSocketCdpConnection" = "CDP over WebSocket (CLI)"
    "BuildInfo" = "version, version date, build time"
    "Program" = "entry point"
    "MainForm" = "app window: browser + panel"
    "WebView2CdpConnection" = "CDP through WebView2 (UI thread)"
    "WebViewPanelTransport" = "panel channel through WebView2"
    "DpapiSecretProtector" = "key encryption with DPAPI"
    "PanelServer" = "panel server for the CLI (HTTP + WebSocket)"
    "ChromiumLauncher" = "starting Chromium/Edge with CDP"
    "CdpBootstrap" = "creating a tab and a CDP session"
    "CliOptions" = "CLI parameters"
    "ConsoleInteraction" = "model questions in the console"
    "MockModelClient" = "scripted model for tests (no network)"
    "TestHarness" = "minimal test harness"
    "StaticSite" = "server of test pages with a simple API"
    "xing" = "test page: a simple profile form"
    "profile" = "test page: a hard form (modal, React, autocomplete, custom list, file)"
    "panel-preview" = "panel screenshot from a test (visual check)"
    "INDEX" = "index of the package files (this file)"}

function Describe([string]$path, [string]$stem) {
    $dir = Split-Path -Parent $path
    $leaf = $stem
    $fileName = Split-Path -Leaf $path
    if ($fileName -like 'INSTALL*.cmd') { return "double-click: install / update on Windows" }
    if ($Descriptions.ContainsKey($fileName)) { return $Descriptions[$fileName] }
    if ($Descriptions.ContainsKey($leaf)) {
        $d = $Descriptions[$leaf]
        if ($leaf -eq "Program") { $d = "entry point: " + (Split-Path -Leaf $dir) }
        return $d
    }
    if ($leaf -like "OpenFill.*") { return "project file " + $leaf }
    return ""
}

# ------------------------------------------------------------------ state of the previous release
$prevIndex = Get-ChildItem -Path $Root -Filter "INDEX__*.md" -File -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -Last 1
$state = @{}
$prevPackage = ""
if ($prevIndex) {
    $raw = (Read-Text $prevIndex.FullName).Text
    $m = [regex]::Match($raw, '```json\s*(\{[\s\S]*?\})\s*```')
    if ($m.Success) {
        $obj = $m.Groups[1].Value | ConvertFrom-Json
        $prevPackage = $obj.package
        foreach ($p in $obj.files.PSObject.Properties) { $state[$p.Name] = $p.Value }
    }
}

if (-not $Stamp) { $Stamp = (Get-Date -Format "yyyy-MM-dd_HHmm") }
$display = $Stamp.Substring(0, 10) + " " + $Stamp.Substring(11, 2) + ":" + $Stamp.Substring(13, 2)
if (-not $Version) { if ($prevPackage) { $Version = Bump $prevPackage } else { $Version = "0.1" } }

$problems = New-Object System.Collections.Generic.List[string]
$files = Get-Files | Where-Object { $_ -notmatch '(^|/)INDEX__' -and $_ -notmatch '^dist/' }
$entries = New-Object System.Collections.Generic.List[object]

foreach ($rel in $files) {
    $full = Join-Path $Root $rel
    $name = Split-Path -Leaf $rel
    $dirRel = (Split-Path -Parent $rel).Replace('\', '/')
    $info = Split-Marker $name
    $key = ($dirRel + "/" + $info.Stem + $info.Ext).TrimStart("/")
    $isMandated = $Mandated -contains $name
    $isBinary = $Binary -contains $info.Ext.ToLowerInvariant()

    if ($isBinary) { $hash = Get-FileHashHex $full; $content = $null }
    else { $content = Read-Text $full; $hash = Get-Hash (Normalize $content.Text) }

    $prev = $null
    if ($state.ContainsKey($key)) { $prev = $state[$key] }
    $changed = (-not $prev) -or ($prev.hash -ne $hash)

    if ($Check) {
        # Audit: name, Metadata, agreement with the INDEX.
        if (-not $isMandated -and -not $info.Version) { $problems.Add("no version in the name: $rel") }
        if (-not $isBinary) {
            $dv = Get-DeclaredVersion $content.Text; $dd = Get-DeclaredDate $content.Text
            if (-not $dv) { $problems.Add("no Metadata in the content: $rel") }
            elseif ($info.Version -and $dv -ne $info.Version) { $problems.Add("version in the name ($($info.Version)) != in the content ($dv): $rel") }
            if ($info.Version -and $dd) {
                $nameDate = $info.Date + " " + $info.Time.Substring(0, 2) + ":" + $info.Time.Substring(2, 2)
                if ($dd -ne $nameDate) { $problems.Add("date in the name ($nameDate) != in the content ($dd): $rel") }
            }
        }
        if (-not $prev) { $problems.Add("file is not in the INDEX: $rel") }
        elseif ($prev.hash -ne $hash) { $problems.Add("file changed since release $prevPackage (no new version): $rel") }
        elseif ($prev.name -ne $name) { $problems.Add("file name differs from the INDEX ($($prev.name)): $rel") }
        continue
    }

    $ver = $info.Version; $date = $info.Date; $time = $info.Time
    if (-not $ver -and $prev) { $ver = $prev.version }
    if ($changed) {
        if ($prev) { $ver = Bump $prev.version }
        else {
            $declared = ""
            if ($content) { $declared = Get-DeclaredVersion $content.Text }
            $ver = Max-Version (Max-Version $declared $info.Version) "0.1"
        }
        $date = $Stamp.Substring(0, 10); $time = $Stamp.Substring(11, 4)
    }
    if ($date -and $time.Length -eq 4) { $fileDisplay = $date + " " + $time.Substring(0, 2) + ":" + $time.Substring(2, 2) }
    elseif ($prev -and $prev.display) { $fileDisplay = $prev.display }
    elseif ($content) { $fileDisplay = Get-DeclaredDate $content.Text }
    else { $fileDisplay = $display }

    if ($content -and ($changed -or -not (Get-DeclaredVersion $content.Text))) {
        $newText = Set-Metadata $content.Text $info.Ext $ver $fileDisplay
        if ($newText -ne $content.Text) { Write-Text $full $newText $content.Bom }
    }

    $newName = $name
    if (-not $isMandated) { $newName = $info.Stem + "__v" + $ver + "__" + $date + "_" + $time + $info.Ext }
    $newRel = $rel
    if ($newName -ne $name) {
        $newRel = ($dirRel + "/" + $newName).TrimStart("/")
        if (Test-Path (Join-Path $Root ".git")) { & git -C $Root mv -f -- $rel $newRel | Out-Null }
        else { Move-Item -LiteralPath $full -Destination (Join-Path $Root $newRel) -Force }
    }
    $finalHash = $hash
    if (-not $isBinary) { $finalHash = Get-Hash (Normalize (Read-Text (Join-Path $Root $newRel)).Text) }
    $entries.Add([pscustomobject]@{ Key = $key; Rel = $newRel; Name = $newName; Version = $ver; Display = $fileDisplay; Hash = $finalHash; Changed = $changed; Mandated = $isMandated; Binary = $isBinary; Stem = $info.Stem; OldRel = $rel })
}

if ($Check) {
    $indexed = @{}
    foreach ($k in $state.Keys) { $indexed[$k] = $true }
    foreach ($rel in $files) { $i = Split-Marker (Split-Path -Leaf $rel); $d = (Split-Path -Parent $rel).Replace('\', '/'); $indexed.Remove(($d + "/" + $i.Stem + $i.Ext).TrimStart("/")) }
    foreach ($k in $indexed.Keys) { $problems.Add("in the INDEX but missing from the package: $k") }
    if (-not $prevIndex) { $problems.Add("no INDEX__*.md file") }
    if ($problems.Count -eq 0) { Write-Host "Audit OK: $($files.Count) files, INDEX $($prevIndex.Name)" -ForegroundColor Green }
    else { Write-Host "Audit: $($problems.Count) problems" -ForegroundColor Red; $problems | ForEach-Object { Write-Host "  - $_" } }
    exit $problems.Count
}

# ------------------------------------------------------------------ references to renamed files in the .sln
foreach ($sln in ($entries | Where-Object { $_.Name -like "*.sln" })) {
    $p = Join-Path $Root $sln.Rel
    $c = Read-Text $p
    $t = $c.Text
    foreach ($e in ($entries | Where-Object { $_.Name -like "*.csproj" })) {
        $t = [regex]::Replace($t, '[^"\\/]*' + [regex]::Escape($e.Stem) + '(__v[\d.]+__\d{4}-\d{2}-\d{2}_\d{4})?\.csproj', (Split-Path -Leaf $e.Rel))
    }
    if ($t -ne $c.Text) { Write-Text $p $t $c.Bom; $sln.Hash = Get-Hash (Normalize $t) }
}

# ------------------------------------------------------------------ app version
$props = Join-Path $Root "Directory.Build.props"
if (Test-Path $props) {
    $c = Read-Text $props
    $vd = $Stamp.Substring(0, 10) + "T" + $Stamp.Substring(11, 2) + ":" + $Stamp.Substring(13, 2)
    $t = $c.Text -replace '<OpenFillVersion>[^<]*</OpenFillVersion>', "<OpenFillVersion>$Version</OpenFillVersion>"
    $t = $t -replace '<OpenFillVersionDate>[^<]*</OpenFillVersionDate>', "<OpenFillVersionDate>$vd</OpenFillVersionDate>"
    if ($t -ne $c.Text) {
        Write-Text $props $t $c.Bom
        $e = $entries | Where-Object { $_.Name -eq "Directory.Build.props" } | Select-Object -First 1
        if ($e) { $e.Hash = Get-Hash (Normalize $t) }
    }
}

# ------------------------------------------------------------------ INDEX
foreach ($old in (Get-ChildItem -Path $Root -Filter "INDEX__*.md" -File -ErrorAction SilentlyContinue)) {
    if (Test-Path (Join-Path $Root ".git")) { & git -C $Root rm -q -f -- $old.Name 2>$null | Out-Null }
    if (Test-Path $old.FullName) { Remove-Item $old.FullName -Force }
}
$indexName = "INDEX__v" + $Version + "__" + $Stamp + ".md"
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# OpenFill - package INDEX")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("## Metadata")
[void]$sb.AppendLine("- Document version: $Version")
[void]$sb.AppendLine("- Date: $display")
[void]$sb.AppendLine("- Package and app version: $Version (version date: $display)")
[void]$sb.AppendLine("- Files: $($entries.Count + 1) (including this INDEX)")
$changedCount = ($entries | Where-Object { $_.Changed }).Count
[void]$sb.AppendLine("- Changed in this release: $changedCount")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("Every file has a version in its name and in the Metadata section of its content. Exceptions marked (N) have a name required by tools; (B) are binary files - the version is in the name only. Audit: ``pwsh tools/release*.ps1 -Check``.")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("| File | Version | Date | Changed | Role |")
[void]$sb.AppendLine("|---|---|---|---|---|")
foreach ($e in ($entries | Sort-Object Rel)) {
    $flag = ""
    if ($e.Mandated) { $flag = " (N)" }
    if ($e.Binary) { $flag = " (B)" }
    $chg = ""
    if ($e.Changed) { $chg = "yes" }
    [void]$sb.AppendLine("| ``" + $e.Rel + "``" + $flag + " | " + $e.Version + " | " + $e.Display + " | " + $chg + " | " + (Describe $e.Rel $e.Stem) + " |")
}
[void]$sb.AppendLine("| ``$indexName`` | $Version | $display | yes | index of the package files (this file) |")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("## State for the next release")
[void]$sb.AppendLine("")
[void]$sb.AppendLine('```json')
$filesObj = [ordered]@{}
foreach ($e in ($entries | Sort-Object Key)) { $filesObj[$e.Key] = [ordered]@{ name = $e.Name; version = $e.Version; display = $e.Display; hash = $e.Hash } }
$json = [ordered]@{ package = $Version; stamp = $Stamp; files = $filesObj } | ConvertTo-Json -Depth 5
[void]$sb.AppendLine($json)
[void]$sb.AppendLine('```')
Write-Text (Join-Path $Root $indexName) $sb.ToString() $false
if (Test-Path (Join-Path $Root ".git")) { & git -C $Root add -A | Out-Null }

Write-Host "Release $Version ($display): $($entries.Count + 1) files, $changedCount changed. INDEX: $indexName" -ForegroundColor Green

# ------------------------------------------------------------------ zip
if ($Zip) {
    $dist = Join-Path $Root "dist"
    New-Item -ItemType Directory -Force -Path $dist | Out-Null
    $zipPath = Join-Path $dist ("OpenFill__v" + $Version + "__" + $Stamp + ".zip")
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    $staging = Join-Path ([IO.Path]::GetTempPath()) ("openfill_pkg_" + [Guid]::NewGuid().ToString("N").Substring(0, 6))
    $pkgRoot = Join-Path $staging "OpenFill"
    $all = @($entries | ForEach-Object { $_.Rel }) + @($indexName)
    foreach ($rel in $all) {
        $dst = Join-Path $pkgRoot $rel
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $dst) | Out-Null
        Copy-Item -LiteralPath (Join-Path $Root $rel) -Destination $dst
    }
    # ZipFile instead of Compress-Archive: Compress-Archive skips hidden files (e.g. .gitignore).
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::CreateFromDirectory($pkgRoot, $zipPath, [IO.Compression.CompressionLevel]::Optimal, $true)
    Remove-Item -Recurse -Force $staging
    Write-Host "Package: $zipPath"
}
