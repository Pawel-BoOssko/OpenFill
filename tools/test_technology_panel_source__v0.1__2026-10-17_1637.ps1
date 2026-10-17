$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$atlas = Get-Content -LiteralPath (Join-Path $root 'src\OpenFill.App\TechnologyAtlas__v0.1__2026-10-15_2314.cs') -Raw
$main = Get-Content -LiteralPath (Join-Path $root 'src\OpenFill.App\MainForm__v0.24__2026-10-14_2259.cs') -Raw
if ($atlas -notmatch 'BuildInfo\.Version') { throw 'Missing dynamic version' }
if ($atlas -notmatch 'Environment\.ProcessId') { throw 'Missing dynamic PID' }
if ($atlas -notmatch 'CreateBanner') { throw 'Missing banner factory' }
if ($main -notmatch 'TechnologyAtlas\.CreateBanner') { throw 'Banner not attached to MainForm' }
Write-Output 'PASS: version, PID, banner factory, MainForm attachment'
