# OpenFill - Metadata: wersja 1.53, data 2026-10-15 17:25
# Prepare a versioned release outside production. This script does not stop or modify the running app.
$ErrorActionPreference = 'Stop'
$root = 'C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill'
$stage = 'C:\Users\pboru\OpenFill\releases\app__v1.53__2026-10-15_1725'
if (Test-Path -LiteralPath $stage) { throw 'Refusing to overwrite an existing release directory.' }
New-Item -ItemType Directory -Path $stage -Force | Out-Null
$appProject = Join-Path $root 'src\OpenFill.App\OpenFill.App__v0.3__2026-10-12_2034.csproj'
$cliProject = Join-Path $root 'src\OpenFill.Cli\OpenFill.Cli__v0.1__2026-10-04_1911.csproj'
dotnet publish $appProject -c Release -o $stage
if ($LASTEXITCODE -ne 0) { throw 'App publish failed.' }
dotnet publish $cliProject -c Release -o (Join-Path $stage 'cli')
if ($LASTEXITCODE -ne 0) { throw 'CLI publish failed.' }
if (-not (Test-Path (Join-Path $stage 'OpenFill.exe'))) { throw 'Staged OpenFill.exe missing.' }
if (-not (Test-Path (Join-Path $stage 'cli\openfill.dll'))) { throw 'Staged CLI missing.' }
$version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $stage 'OpenFill.exe')).ProductVersion
Write-Output ('STAGE_READY path=' + $stage)
Write-Output ('STAGE_VERSION ' + $version)
