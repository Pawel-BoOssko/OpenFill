# OpenFill - Metadata: wersja 1.49, data 2026-10-14 22:59
# OpenFill - deployment v1.47, 2026-10-13 20:22
# Run ONLY after openfill_maintenance reports running_tasks=0.
$ErrorActionPreference = 'Stop'
$root = 'C:\Users\pboru\OpenFill'
$app = Join-Path $root 'app'
$stage = Join-Path $root 'release__v1.47__2026-10-13_2016'
$old = Join-Path $root 'app__rollback__v1.46__2026-10-13_2022'
$failed = Join-Path $root 'app__failed__v1.47__2026-10-13_2022'
if ($env:OPENFILL_MAINTENANCE_OK -ne '1') { throw 'Safety gate missing: maintenance verification required.' }
if (-not (Test-Path (Join-Path $stage 'OpenFill.exe')) -or -not (Test-Path (Join-Path $stage 'cli'))) { throw 'Release staging incomplete' }
if (Test-Path $old) { throw 'Rollback directory already exists' }
Write-Output 'DEPLOY_BEGIN'
Get-Process OpenFill -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction Stop
Start-Sleep -Seconds 2
try {
    Move-Item -LiteralPath $app -Destination $old
    Move-Item -LiteralPath $stage -Destination $app
    Start-Process -FilePath (Join-Path $app 'OpenFill.exe') -WorkingDirectory $app
    $online = $false
    for ($i=0; $i -lt 20; $i++) {
        Start-Sleep -Seconds 1
        if (Get-Process OpenFill -ErrorAction SilentlyContinue) { $online = $true; break }
    }
    if (-not $online) { throw 'New OpenFill process did not start' }
    Write-Output 'DEPLOY_OK version=1.47'
} catch {
    Write-Output ('DEPLOY_FAILED '+ $_.Exception.Message)
    Get-Process OpenFill -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    if (Test-Path $app) { Move-Item -LiteralPath $app -Destination $failed -ErrorAction SilentlyContinue }
    if (Test-Path $old) { Move-Item -LiteralPath $old -Destination $app; Start-Process -FilePath (Join-Path $app 'OpenFill.exe') -WorkingDirectory $app }
    throw
}
