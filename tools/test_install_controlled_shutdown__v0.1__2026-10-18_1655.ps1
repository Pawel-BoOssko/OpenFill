# OpenFill - Metadata: wersja 0.1, data 2026-10-18 16:55
# Source-level regression gate: the installer must never kill live tasks after a fixed 3s delay.
$ErrorActionPreference='Stop'
$installer=Join-Path $PSScriptRoot 'install__v0.13__2026-10-18_1650.ps1'
$src=Get-Content -LiteralPath $installer -Raw -Encoding UTF8
$start=$src.IndexOf('if ($running) {', $src.IndexOf('Step "Building OpenFill"'))
$end=$src.IndexOf('Step "Data location"', $start)
if ($start -lt 0 -or $end -le $start) { throw 'FAIL cannot find controlled shutdown block' }
$block=$src.Substring($start, $end-$start)
$checks=0
function Check([string]$name,[bool]$ok){
  if(-not $ok){ throw ('FAIL '+$name) }
  $script:checks++
  Write-Output ('PASS '+$name)
}
Check 'installer requests CloseMainWindow' ($block.Contains('CloseMainWindow()'))
Check 'installer checks graceful-close request result' ($block.Contains('if (-not $process.CloseMainWindow())'))
Check 'installer waits for process exit' ($block.Contains('Get-Process -Id $_.Id'))
Check 'installer allows at least 15 seconds' ($block.Contains('AddSeconds(25)'))
Check 'installer aborts if process does not exit' ($block.Contains('if ($stillRunning.Count -gt 0)') -and $block.Contains('Installation aborted'))
Check 'installer never forces task termination' (-not ($block -match 'Stop-Process\s+-Force'))
Check 'installer never assumes three seconds suffice' (-not ($block -match 'Start-Sleep\s+-Seconds\s+3'))
Check 'installer waits before touching publish' ($end -lt $src.IndexOf('Invoke-DotnetPublish $appProj'))
Check 'installer preserves data directory' ($src.Contains('$dataDir = Join-Path $DataBase $Instance'))
$errors=$null
$tokens=$null
[void][System.Management.Automation.Language.Parser]::ParseFile($installer,[ref]$tokens,[ref]$errors)
Check 'PowerShell syntax is valid' (@($errors).Count -eq 0)
Write-Output ('TOTAL '+$checks+' PASS, 0 FAIL (static; real interactive close not attempted)')