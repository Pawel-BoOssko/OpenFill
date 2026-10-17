<#
  OpenFill - run the development version straight from the sources (instance "dev").
  Metadata:
    wersja: 0.2
    data:   2026-10-05 15:15
  A separate browser profile, logs, notes and settings (%LOCALAPPDATA%\OpenFill\dev),
  so work on the code does not touch the "stable" instance installed by install.ps1.
  Parameters: -Cli (instead of the window: CLI with the panel in a browser), -Mock (scripted model, no key and no network)
#>
param([switch]$Cli, [switch]$Mock)
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
if ($Cli) {
    $cliArgs = @("--instance", "dev", "--headful")
    if ($Mock) { $cliArgs += "--mock" }
    & dotnet run --project (Join-Path $Root "src\OpenFill.Cli") -- @cliArgs
} else {
    & dotnet run --project (Join-Path $Root "src\OpenFill.App") -- --instance dev
}
