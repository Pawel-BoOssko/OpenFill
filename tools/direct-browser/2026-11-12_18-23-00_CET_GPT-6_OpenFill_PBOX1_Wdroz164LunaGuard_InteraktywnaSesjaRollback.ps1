# GPT-6 | 2026-11-12 18:23 CET | Deploy verified OpenFill 1.64 in the authorized interactive Windows session.
$ErrorActionPreference='Stop'
$root='C:\Users\pboru\Documents\Projects\OpenFill-SelfChatSteering-Current-20261110'
$tool=Join-Path $root 'tools\deploy_safely__v0.1__2026-10-14_2235.ps1'
$base=Join-Path $root 'artifacts\2026-11-12_18-22-00_CET_GPT-6_OpenFill_v1-64_PBOX1_BinariaLunaGuard_DlaBezpiecznegoWdrazania'
$release=Join-Path $base 'publish'
$log=Join-Path $base '2026-11-12_18-23-00_CET_GPT-6_OpenFill_v1-64_PBOX1_WdrozenieLunaGuard_WalidacjaIOpcjaRollback.log'
$env:OPENFILL_MAINTENANCE_OK='1'
try {
  ('DEPLOY_WRAPPER_BEGIN '+(Get-Date -Format o)+' SESSION='+[System.Diagnostics.Process]::GetCurrentProcess().SessionId) |
    Out-File -LiteralPath $log -Encoding UTF8
  & $tool -Version '1.64' -ReleaseDirectory $release *>&1 | Out-File -LiteralPath $log -Append -Encoding UTF8
  ('DEPLOY_WRAPPER_SUCCESS '+(Get-Date -Format o)) | Out-File -LiteralPath $log -Append -Encoding UTF8
} catch {
  ('DEPLOY_WRAPPER_ERROR '+$_.Exception.ToString()) | Out-File -LiteralPath $log -Append -Encoding UTF8
  exit 1
}
