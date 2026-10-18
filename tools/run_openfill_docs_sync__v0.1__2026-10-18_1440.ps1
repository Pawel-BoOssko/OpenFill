# OpenFill - Metadata: wersja 0.1, data 2026-10-18 14:40
# Launcher for an unattended scheduled sync. Captures all output locally; never changes collision policy.
[CmdletBinding()]
param([switch]$DryRun)
$ErrorActionPreference = 'Stop'
$syncScript = Join-Path $PSScriptRoot 'sync_openfill_docs_gdrive__v0.1__2026-10-15_2343.ps1'
$logDir = Join-Path $env:LOCALAPPDATA 'OpenFillDocsSync'
[System.IO.Directory]::CreateDirectory($logDir) | Out-Null
$log = Join-Path $logDir ('docs-sync-' + (Get-Date -Format 'yyyy-MM-dd') + '.log')
$started = Get-Date
Add-Content -LiteralPath $log -Encoding UTF8 -Value ('START ' + $started.ToString('yyyy-MM-dd HH:mm:ss') + ' dryRun=' + $DryRun.IsPresent)
try {
    if ($DryRun) {
        & $syncScript -DryRun *>> $log
    } else {
        & $syncScript *>> $log
    }
    Add-Content -LiteralPath $log -Encoding UTF8 -Value ('DONE ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + ' elapsedSec=' + [math]::Round(((Get-Date)-$started).TotalSeconds, 2))
} catch {
    Add-Content -LiteralPath $log -Encoding UTF8 -Value ('ERROR ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + ' ' + $_.Exception.Message)
    throw
}
