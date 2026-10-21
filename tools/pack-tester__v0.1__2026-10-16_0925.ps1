<#
  OpenFill - tester package: a ready-to-run zip for someone who should not have to set anything up.
  Metadata:
    wersja: 0.1
    data:   2026-10-16 09:25
  What it does:
    1. Copies the repository files (git ls-files) into dist\tester-src - a throwaway copy outside version control.
    2. In that copy only: writes the test OpenAI key into EmbeddedDefaults and turns MCP on by default.
       The repository itself is never changed (checked at the end) and the key never goes into git.
    3. Builds the app self-contained for win-x64 (the tester needs no .NET), then deletes the copy with the key.
    4. Adds the launcher (URUCHOM OpenFill.cmd) and the instructions (INSTRUKCJA.txt) and zips everything into dist\.
  The key ends up inside the built exe, so anyone holding the zip can extract it: use only a test key
  with a low spending limit and a short lifetime, and revoke it when the test is over.
  Usage (on Windows, with .NET SDK 10 installed - e.g. after INSTALL.cmd):
    powershell -ExecutionPolicy Bypass -File tools\pack-tester__v0.1__2026-10-16_0925.ps1
        -> asks for the key (hidden input)
    ... -ApiKey sk-...                  # or pass it (or set OPENAI_TESTER_KEY)
    ... -ValidUntil "2026-10-20"        # printed in the instructions so the tester knows when it stops working
    ... -Name Marek                     # added to the zip name
#>
param(
    [string]$ApiKey = "",
    [string]$ValidUntil = "",
    [string]$Name = ""
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

function Step([string]$text) { Write-Host ""; Write-Host "==> $text" -ForegroundColor Cyan }
function Info([string]$text) { Write-Host "    $text" }
function Fail([string]$text) { Write-Host ""; Write-Host "ERROR: $text" -ForegroundColor Red; exit 1 }
function Newest([string]$dir, [string]$filter) {
    $f = Get-ChildItem -Path (Join-Path $Root $dir) -Filter $filter -File -ErrorAction SilentlyContinue | Sort-Object Name | Select-Object -Last 1
    if (-not $f) { Fail "Missing $dir\$filter" }
    return $f.FullName
}

# ------------------------------------------------------------------ 1. key
Step "Test key"
if (-not $ApiKey) { $ApiKey = [string]$env:OPENAI_TESTER_KEY }
if (-not $ApiKey) {
    $sec = Read-Host "OpenAI test key (sk-..., hidden)" -AsSecureString
    $ApiKey = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($sec))
}
$ApiKey = $ApiKey.Trim()
# Only characters OpenAI keys use - nothing that could break out of the C# string literal.
if ($ApiKey -notmatch '^sk-[A-Za-z0-9_\-]{20,}$') { Fail "That does not look like an OpenAI key (sk-...)." }
Info ("Key: ..." + $ApiKey.Substring($ApiKey.Length - 4))

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) { Fail ".NET SDK 10 not found. Run INSTALL.cmd once on this PC, then try again." }

$embeddedRel = (Get-ChildItem -Path (Join-Path $Root "src\OpenFill.Core\Config") -Filter "EmbeddedDefaults__v*.cs" | Select-Object -First 1)
if (-not $embeddedRel) { Fail "EmbeddedDefaults__v*.cs not found." }
$embeddedRepoHash = (Get-FileHash $embeddedRel.FullName -Algorithm SHA256).Hash

# ------------------------------------------------------------------ 2. throwaway copy
$dist = Join-Path $Root "dist"
$src = Join-Path $dist "tester-src"
$pkgName = "OpenFill-tester"
$stamp = Get-Date -Format "yyyy-MM-dd_HHmm"
$version = ([xml](Get-Content (Join-Path $Root "Directory.Build.props") -Raw)).Project.PropertyGroup.OpenFillVersion | Select-Object -First 1
$zipBase = "OpenFill-tester__v" + $version + "__" + $stamp
if ($Name) { $zipBase += "__" + ($Name -replace '[^A-Za-z0-9_-]', '') }
$pkg = Join-Path $dist $pkgName

Step "Copying sources into $src"
foreach ($d in @($src, $pkg)) { if (Test-Path $d) { Remove-Item $d -Recurse -Force } }
New-Item -ItemType Directory -Path $src -Force | Out-Null
$files = & git -C $Root ls-files --cached --others --exclude-standard
if ($LASTEXITCODE -ne 0 -or -not $files) { Fail "git ls-files failed - run this from a git clone of OpenFill." }
foreach ($rel in $files) {
    $from = Join-Path $Root $rel
    if (-not (Test-Path $from -PathType Leaf)) { continue }
    $to = Join-Path $src $rel
    New-Item -ItemType Directory -Path (Split-Path -Parent $to) -Force | Out-Null
    Copy-Item -LiteralPath $from -Destination $to -Force
}

function Patch([string]$file, [string]$pattern, [string]$replacement, [string]$what) {
    $text = [IO.File]::ReadAllText($file)
    $new = [regex]::Replace($text, $pattern, $replacement)
    if ($new -eq $text) { Fail "Could not set $what in $file (the source changed?)." }
    [IO.File]::WriteAllText($file, $new, (New-Object Text.UTF8Encoding($true)))
}

Step "Tester settings (in the copy only)"
$embeddedCopy = Join-Path $src ($embeddedRel.FullName.Substring($Root.Length + 1))
Patch $embeddedCopy 'public const string ApiKey = "";' ('public const string ApiKey = "' + $ApiKey + '";') "the built-in key"
Info "Built-in key set."
$configCopy = (Get-ChildItem -Path (Join-Path $src "src\OpenFill.Core\Config") -Filter "AppConfig__v*.cs" | Select-Object -First 1).FullName
Patch $configCopy 'public bool McpEnabled \{ get; set; \} = false;' 'public bool McpEnabled { get; set; } = true;' "MCP on by default"
Info "MCP on by default (Cloudflare quick tunnel, no account needed)."

# ------------------------------------------------------------------ 3. build
Step "Building (self-contained win-x64, a few minutes the first time)"
$appProj = (Get-ChildItem -Path (Join-Path $src "src\OpenFill.App") -Filter "*.csproj" | Select-Object -First 1).FullName
$env:DOTNET_NOLOGO = "1"; $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"; $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$prev = $ErrorActionPreference
$ErrorActionPreference = "Continue"
try {
    $out = & dotnet publish $appProj -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=false -o (Join-Path $pkg "app") --nologo -v minimal 2>&1 | ForEach-Object { "$_" }
    $code = $LASTEXITCODE
} finally { $ErrorActionPreference = $prev }
# The copy holds the key in plain text - remove it whatever happened.
Remove-Item $src -Recurse -Force -ErrorAction SilentlyContinue
if ($code -ne 0) { $out | Select-Object -Last 30 | ForEach-Object { Info $_ }; Fail "Build failed (code $code)." }
if (-not (Test-Path (Join-Path $pkg "app\OpenFill.exe"))) { Fail "OpenFill.exe missing after the build." }
Get-ChildItem (Join-Path $pkg "app") -Filter "*.pdb" -Recurse | Remove-Item -Force
Info "Built: $pkg\app\OpenFill.exe"

# ------------------------------------------------------------------ 4. launcher, instructions, zip
Step "Launcher and instructions"
Copy-Item (Newest "tools\tester" "URUCHOM__v*.cmd") (Join-Path $pkg "URUCHOM OpenFill.cmd")
Copy-Item (Newest "tools\tester" "start-tester__v*.ps1") (Join-Path $pkg "app\start-tester.ps1")
$instr = [IO.File]::ReadAllText((Newest "tools\tester" "INSTRUKCJA__v*.md"))
$validText = "kilka dni (zapytaj osobę, która Ci to wysłała)"
if ($ValidUntil) { $validText = "do " + $ValidUntil }
$instr = $instr.Replace("{{VALID_UNTIL}}", $validText).Replace("{{VERSION}}", [string]$version)
$instr = $instr -replace '(?s)<!--.*?-->\r?\n?', ''
[IO.File]::WriteAllText((Join-Path $pkg "INSTRUKCJA.txt"), $instr, (New-Object Text.UTF8Encoding($true)))

$zip = Join-Path $dist ($zipBase + ".zip")
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path $pkg -DestinationPath $zip -CompressionLevel Optimal
Remove-Item $pkg -Recurse -Force

# ------------------------------------------------------------------ safety check
if ((Get-FileHash $embeddedRel.FullName -Algorithm SHA256).Hash -ne $embeddedRepoHash) { Fail "The repository copy of EmbeddedDefaults changed - restore it before committing!" }
$leak = & git -C $Root grep -l -F $ApiKey 2>$null
if ($leak) { Fail ("The key appears in tracked files: " + ($leak -join ", ")) }

Step "Done"
Info "Package: $zip"
Info "Send it to the tester (e.g. via Google Drive). It contains the key - do not publish it."
Info "When the test is over, revoke the key at https://platform.openai.com/api-keys"
