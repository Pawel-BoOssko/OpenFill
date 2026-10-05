<#
  OpenFill - install and update on Windows (no manual setup).
  Metadata:
    wersja: 0.9
    data:   2026-10-05 19:05
  What it does (each step only when needed):
    1. .NET SDK 10 - via winget, or the official dotnet-install.ps1 into the user profile (no admin needed).
    2. Microsoft Edge WebView2 Runtime - Microsoft's silent installer (usually already present on Windows 11).
    3. Builds the app (OpenFill.exe) and the CLI (openfill.exe) into %USERPROFILE%\OpenFill\app. Data (profile, logs, settings) lives in %USERPROFILE%\OpenFill\data;
       an older copy in %LOCALAPPDATA%\OpenFill is copied there once.
    4. OpenAI key: if OPENAI_API_KEY is set, it is stored encrypted (DPAPI).
       Otherwise the app panel asks for the key on first start.
    5. "OpenFill" shortcuts in the Start menu and on the desktop, then launches the app.
    6. Autostart: a scheduled task starts OpenFill when you log on to Windows and restarts it if it crashes.
  Run: double-click INSTALL.cmd (or: powershell -ExecutionPolicy Bypass -File tools\install.ps1)
  Parameters: -Instance dev (separate profile/logs), -NoLaunch, -NoShortcuts, -NoAutostart, -Minimized (autostart with the window minimized)
#>
param(
    [string]$Instance = "stable",
    [switch]$NoLaunch,
    [switch]$NoShortcuts,
    [switch]$NoAutostart,
    [switch]$Minimized
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Root = Split-Path -Parent $PSScriptRoot
# Outside AppData on purpose: a host such as the Claude desktop app redirects AppData writes into a private copy that Windows itself
# (autostart, shortcuts, a double click) cannot see.
$OpenFillHome = Join-Path $env:USERPROFILE "OpenFill"
$AppDir = Join-Path $OpenFillHome "app"
if ($Instance -ne "stable") { $AppDir = Join-Path $OpenFillHome ("app-" + $Instance) }
$DataBase = Join-Path $OpenFillHome "data"
$LogFile = Join-Path $env:TEMP ("openfill_install_" + (Get-Date -Format "yyyyMMdd_HHmmss") + ".log")

function Step([string]$text) { Write-Host ""; Write-Host "==> $text" -ForegroundColor Cyan; Add-Content -Path $LogFile -Value "==> $text" }
function Info([string]$text) { Write-Host "    $text"; Add-Content -Path $LogFile -Value "    $text" }
function Fail([string]$text) {
    Write-Host ""; Write-Host "ERROR: $text" -ForegroundColor Red
    Write-Host "Details in the log: $LogFile"
    Add-Content -Path $LogFile -Value "ERROR: $text"
    if ($Host.Name -eq "ConsoleHost") { Read-Host "Press Enter to close" | Out-Null }
    exit 1
}

function Find-Dotnet {
    $candidates = @()
    $cmd = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($cmd) { $candidates += $cmd.Source }
    $candidates += (Join-Path $env:ProgramFiles "dotnet\dotnet.exe")
    $candidates += (Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe")
    foreach ($c in $candidates) {
        if ($c -and (Test-Path $c)) {
            $sdks = & $c --list-sdks 2>$null
            if ($sdks | Where-Object { $_ -match "^10\." }) { return $c }
        }
    }
    return $null
}

function Test-WebView2 {
    $id = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
    $keys = @(
        "HKLM:\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\$id",
        "HKLM:\SOFTWARE\Microsoft\EdgeUpdate\Clients\$id",
        "HKCU:\SOFTWARE\Microsoft\EdgeUpdate\Clients\$id"
    )
    foreach ($k in $keys) {
        $pv = (Get-ItemProperty -Path $k -Name pv -ErrorAction SilentlyContinue).pv
        if ($pv -and $pv -ne "0.0.0.0") { return $pv }
    }
    return $null
}

Write-Host "OpenFill - install (instance: $Instance)" -ForegroundColor White
Info "Sources: $Root"
Info "Log:    $LogFile"

# ------------------------------------------------------------------ 1. .NET SDK 10
Step "Checking .NET SDK 10"
$dotnet = Find-Dotnet
if (-not $dotnet) {
    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if ($winget) {
        Info "Installing .NET SDK 10 via winget (a UAC prompt may appear)..."
        & winget install --id Microsoft.DotNet.SDK.10 --exact --silent --accept-source-agreements --accept-package-agreements | Out-Null
        $env:PATH = [Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [Environment]::GetEnvironmentVariable("PATH", "User")
        $dotnet = Find-Dotnet
    }
    if (-not $dotnet) {
        Info "Installing .NET SDK 10 into the user profile (no administrator rights)..."
        $script = Join-Path $env:TEMP "dotnet-install.ps1"
        Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $script -UseBasicParsing
        & $script -Channel 10.0 -InstallDir (Join-Path $env:LOCALAPPDATA "Microsoft\dotnet") | Out-Null
        $dotnet = Find-Dotnet
    }
    if (-not $dotnet) { Fail "Could not install .NET SDK 10." }
}
Info "dotnet: $dotnet"

# ------------------------------------------------------------------ 2. WebView2 Runtime
Step "Checking Microsoft Edge WebView2 Runtime"
$wv = Test-WebView2
if (-not $wv) {
    Info "Installing WebView2 Runtime (Microsoft's silent installer)..."
    $setup = Join-Path $env:TEMP "MicrosoftEdgeWebview2Setup.exe"
    Invoke-WebRequest -Uri "https://go.microsoft.com/fwlink/p/?LinkId=2124703" -OutFile $setup -UseBasicParsing
    $p = Start-Process -FilePath $setup -ArgumentList "/silent", "/install" -Wait -PassThru
    $wv = Test-WebView2
    if (-not $wv) { Fail "WebView2 Runtime installation failed (code $($p.ExitCode))." }
}
Info "WebView2: $wv"

# ------------------------------------------------------------------ 3. Budowanie
function Invoke-DotnetPublish([string]$proj, [string]$outDir, [string]$what) {
    # dotnet pisze bannery pierwszego uruchomienia i ostrzezenia na stderr. Przy $ErrorActionPreference=Stop
    # Windows PowerShell 5.1 turns every such line into a terminating error (NativeCommandError) and hides the real result.
    # So here: Continue mode, banners silenced, and only the dotnet exit code decides about success.
    $env:DOTNET_NOLOGO = "1"
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
    $env:DOTNET_GENERATE_ASPNET_CERTIFICATE = "false"
    $prev = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    try {
        $out = & $dotnet publish $proj -c Release -o $outDir --nologo -v minimal 2>&1 | ForEach-Object { "$_" }
        $code = $LASTEXITCODE
    } finally { $ErrorActionPreference = $prev }
    Add-Content -Path $LogFile -Value $out
    if ($code -ne 0) {
        $out | Select-Object -Last 30 | ForEach-Object { Write-Host "    $_" }
        Fail "Building $what failed (code $code). Full output is in the log."
    }
}

Step "Building OpenFill"
# By process name, not by path: Windows may report a different exe path (e.g. when the app was started by a process with AppData virtualization),
# and a locked file makes the build impossible. Instances other than ours (-Instance) are recognized by the --instance argument on the command line.
$running = @(Get-Process -Name "OpenFill" -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    $cmdLines = @{}
    Get-CimInstance Win32_Process -Filter "Name='OpenFill.exe'" -ErrorAction SilentlyContinue | ForEach-Object { $cmdLines[[int]$_.ProcessId] = [string]$_.CommandLine }
    $running = @($running | Where-Object {
        $cl = $cmdLines[[int]$_.Id]
        if ($Instance -eq "stable") { -not ($cl -match '--instance\s+\S+') } else { $cl -match ('--instance\s+' + [regex]::Escape($Instance)) }
    })
}
if ($running) {
    Info "Closing the running app so the files can be replaced..."
    $running | ForEach-Object { $_.CloseMainWindow() | Out-Null }
    Start-Sleep -Seconds 3
    $running | Where-Object { -not $_.HasExited } | Stop-Process -Force
}
Step "Data location"
$dataDir = Join-Path $DataBase $Instance
if (-not (Test-Path $dataDir)) {
    $cands = @(Join-Path $env:LOCALAPPDATA ("OpenFill\" + $Instance))
    $cands += @(Get-ChildItem -Path (Join-Path $env:LOCALAPPDATA "Packages") -Directory -Filter "Claude_*" -ErrorAction SilentlyContinue | ForEach-Object { Join-Path $_.FullName ("LocalCache\Local\OpenFill\" + $Instance) })
    $src = $cands | Where-Object { Test-Path (Join-Path $_ "config.json") } | Sort-Object { (Get-Item (Join-Path $_ "config.json")).LastWriteTime } -Descending | Select-Object -First 1
    New-Item -ItemType Directory -Path $DataBase -Force | Out-Null
    if ($src) {
        Info "Copying your existing data from $src (one time)..."
        Copy-Item -Path $src -Destination $dataDir -Recurse -Force
        Info "Copied to $dataDir"
    } else {
        New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
        Info "New data folder: $dataDir"
    }
} else { Info "Data folder: $dataDir" }

# Project files have versions in their names, so we look for them by pattern.
$appProj = (Get-ChildItem -Path (Join-Path $Root "src\OpenFill.App") -Filter "*.csproj" | Select-Object -First 1).FullName
$cliProj = (Get-ChildItem -Path (Join-Path $Root "src\OpenFill.Cli") -Filter "*.csproj" | Select-Object -First 1).FullName
if (-not $appProj -or -not $cliProj) { Fail "No project files found in $Root\src." }
Invoke-DotnetPublish $appProj $AppDir "the app"
Invoke-DotnetPublish $cliProj (Join-Path $AppDir "cli") "CLI"
$exe = Join-Path $AppDir "OpenFill.exe"
if (-not (Test-Path $exe)) { Fail "Missing $exe after the build." }
Info "App: $exe"

# ------------------------------------------------------------------ 4. OpenAI key
Step "OpenAI key"
$instArgs = @()
if ($Instance -ne "stable") { $instArgs = @("--instance", $Instance) }
if ($env:OPENAI_API_KEY) {
    # The key does not go on the command line (visible to other processes) - the app reads it itself from the variable.
    $p = Start-Process -FilePath $exe -ArgumentList ($instArgs + @("--import-env-key")) -Wait -PassThru -WindowStyle Hidden
    if ($p.ExitCode -eq 0) { Info "Saved the key from OPENAI_API_KEY (DPAPI-encrypted)." }
    else { Info "Could not save the key - the panel will ask for it at start." }
} else {
    Info "OPENAI_API_KEY is not set - the app panel will ask for the key on first start."
}

# ------------------------------------------------------------------ 5. Shortcuts
function Invoke-Outside([string]$code) {
    # Runs PowerShell code as the current user through Task Scheduler, so that AppData redirection of a host app does not apply to it.
    $tmp = Join-Path $OpenFillHome "tmp"
    New-Item -ItemType Directory -Path $tmp -Force | Out-Null
    $id = [guid]::NewGuid().ToString("N").Substring(0, 6)
    $script = Join-Path $tmp ("do_" + $id + ".ps1")
    $flag = $script + ".done"
    Set-Content -Path $script -Value ($code + "`r`nSet-Content -Path '" + $flag + "' -Value 'ok'") -Encoding ASCII
    $tn = "OpenFill-setup-" + $id
    $user = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
    $act = New-ScheduledTaskAction -Execute "powershell.exe" -Argument ('-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + $script + '"')
    Register-ScheduledTask -TaskName $tn -Action $act -Principal (New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited) -Force | Out-Null
    Start-ScheduledTask -TaskName $tn
    for ($i = 0; $i -lt 40 -and -not (Test-Path $flag); $i++) { Start-Sleep -Milliseconds 500 }
    $ok = Test-Path $flag
    Unregister-ScheduledTask -TaskName $tn -Confirm:$false -ErrorAction SilentlyContinue
    Remove-Item $script, $flag -Force -ErrorAction SilentlyContinue
    return $ok
}

if (-not $NoShortcuts) {
    Step "Creating shortcuts"
    $name = "OpenFill"
    if ($Instance -ne "stable") { $name = "OpenFill ($Instance)" }
    $lnkStart = Join-Path ([Environment]::GetFolderPath("Programs")) "$name.lnk"
    $lnkDesk = Join-Path ([Environment]::GetFolderPath("Desktop")) "$name.lnk"
    $argText = ($instArgs -join " ")
    $code = @"
`$sh = New-Object -ComObject WScript.Shell
foreach (`$p in @('$lnkStart', '$lnkDesk')) {
    `$l = `$sh.CreateShortcut(`$p)
    `$l.TargetPath = '$exe'
    `$l.Arguments = '$argText'
    `$l.WorkingDirectory = '$AppDir'
    `$l.Description = 'OpenFill - lets a model fill in web pages'
    `$l.Save()
}
"@
    try {
        if (Invoke-Outside $code) { Info $lnkStart; Info $lnkDesk } else { Info "The shortcuts could not be created (timeout)." }
    } catch { Info "The shortcuts could not be created: $($_.Exception.Message)" }
}
# ------------------------------------------------------------------ 6. Autostart at logon
if (-not $NoAutostart) {
    Step "Autostart at Windows logon"
    try {
        $taskName = "OpenFill"
        if ($Instance -ne "stable") { $taskName = "OpenFill ($Instance)" }
        $argParts = @($instArgs)
        if ($Minimized) { $argParts += "--minimized" }
        if ($argParts.Count -gt 0) { $action = New-ScheduledTaskAction -Execute $exe -Argument ($argParts -join " ") -WorkingDirectory $AppDir }
        else { $action = New-ScheduledTaskAction -Execute $exe -WorkingDirectory $AppDir }
        $user = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
        $trigger = New-ScheduledTaskTrigger -AtLogOn -User $user
        $principal = New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited
        $settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -RestartCount 5 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit (New-TimeSpan -Seconds 0) -MultipleInstances IgnoreNew
        Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Force | Out-Null
        Info "Task '$taskName': starts OpenFill at logon; restarts it (up to 5 times, 1 minute apart) if it crashes."
        Info "It needs you to be logged in (turn on automatic sign-in for an unattended PC) and the PC not to sleep."
    } catch { Info "Could not set up the autostart task: $($_.Exception.Message)" }
}

Step "Done"
Info "Instance data:  $dataDir"
if (-not $NoLaunch) {
    # Started through Task Scheduler too, so that the app runs the same way as after a logon (not tied to this window or to a host app).
    $started = $false
    try {
        $openName = "OpenFill-open"
        if ($Instance -ne "stable") { $openName = "OpenFill-open ($Instance)" }
        $user = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
        if ($instArgs.Count -gt 0) { $openAct = New-ScheduledTaskAction -Execute $exe -Argument ($instArgs -join " ") -WorkingDirectory $AppDir }
        else { $openAct = New-ScheduledTaskAction -Execute $exe -WorkingDirectory $AppDir }
        Register-ScheduledTask -TaskName $openName -Action $openAct -Principal (New-ScheduledTaskPrincipal -UserId $user -LogonType Interactive -RunLevel Limited) -Force | Out-Null
        Start-ScheduledTask -TaskName $openName
        $started = $true
    } catch { Info "Task Scheduler start failed: $($_.Exception.Message)" }
    if (-not $started) {
        if ($instArgs.Count -gt 0) { Start-Process -FilePath $exe -ArgumentList $instArgs -WorkingDirectory $AppDir }
        else { Start-Process -FilePath $exe -WorkingDirectory $AppDir }
    }
    Info "OpenFill started."
}
