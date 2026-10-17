param([switch]$DryRun)
$ErrorActionPreference='Stop'
$root='C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill'
$source=Join-Path $root 'docs'
$drive=Get-PSDrive -Name H -PSProvider FileSystem -ErrorAction Stop
if ($drive.Description -notmatch 'posko666@gmail\.com') { throw 'Unexpected Google Drive account; sync aborted.' }
$driveHome=Get-ChildItem -LiteralPath 'H:\' -Directory | Where-Object { $_.Name -notlike '.*' -and $_.Name -notlike '$*' } | Select-Object -First 1
if ($null -eq $driveHome) { throw 'Google Drive My Drive folder missing.' }
$destination=Join-Path $driveHome.FullName 'OpenFill\docs'
if (-not (Test-Path -LiteralPath $destination -PathType Container)) { throw 'OpenFill/docs destination missing; sync aborted.' }
$copied=0
$unchanged=0
$skipped=0
$files=Get-ChildItem -LiteralPath $source -File | Where-Object { $_.Extension -in '.md','.txt' }
foreach ($file in $files) {
  if ($file.Length -gt 2097152) { $skipped++; continue }
  $content=[System.IO.File]::ReadAllText($file.FullName)
  if ($content -match ('(?i)(?:sk-(?:proj-)?[a-z0-9_-]{20,}|gh[pousr]_[a-z0-9]{20,}|AIza[a-z0-9_-]{25,}|----' + '-BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|(?:password|api[_-]?key|secret|bearer|token)\s*[:=]\s*[\x22\x27]?[a-z0-9_+\-/=]{18,})')) { $skipped++; continue }
  $target=Join-Path $destination $file.Name
  if (Test-Path -LiteralPath $target) {
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) { $unchanged++ }
    else { Write-Warning ("File version collision, not overwritten: "+$file.Name); $skipped++ }
    continue
  }
  if ($DryRun) { Write-Output ("WOULD_COPY "+$file.Name); continue }
  Copy-Item -LiteralPath $file.FullName -Destination $target -ErrorAction Stop
  if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) { throw ("Hash verification failed: "+$file.Name) }
  $copied++
}
Write-Output ("SYNC copied="+$copied+" unchanged="+$unchanged+" skipped="+$skipped+" scanned="+$files.Count+" dryRun="+$DryRun.IsPresent)