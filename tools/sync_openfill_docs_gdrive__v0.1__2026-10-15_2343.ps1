# OpenFill - Metadata: wersja 0.2, data 2026-10-18 15:05
# Canonical direction: local docs -> Google Drive. Divergent Drive versions are archived and verified.
param([switch]$DryRun, [string]$TestSource='', [string]$TestDestination='')
$ErrorActionPreference='Stop'
$root='C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill'
if ($TestSource -or $TestDestination) {
  if (-not ($TestSource -and $TestDestination)) { throw 'Both isolated test paths are required.' }
  $source=$TestSource
  $destination=$TestDestination
} else {
  $source=Join-Path $root 'docs'
  $drive=Get-PSDrive -Name H -PSProvider FileSystem -ErrorAction Stop
  if ($drive.Description -notmatch 'posko666@gmail\.com') { throw 'Unexpected Google Drive account; sync aborted.' }
  $driveHome=Get-ChildItem -LiteralPath 'H:\' -Directory | Where-Object { $_.Name -notlike '.*' -and $_.Name -notlike '$*' } | Select-Object -First 1
  if ($null -eq $driveHome) { throw 'Google Drive My Drive folder missing.' }
  $destination=Join-Path $driveHome.FullName 'OpenFill\docs'
}
if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw 'Local docs source missing; sync aborted.' }
if (-not (Test-Path -LiteralPath $destination -PathType Container)) { throw 'OpenFill/docs destination missing; sync aborted.' }
$copied=0
$updated=0
$archived=0
$unchanged=0
$skipped=0
$files=Get-ChildItem -LiteralPath $source -File | Where-Object { $_.Extension -in '.md','.txt' }
foreach ($file in $files) {
  if ($file.Length -gt 2097152) { $skipped++; continue }
  $content=[System.IO.File]::ReadAllText($file.FullName)
  if ($content -match ('(?i)(?:sk-(?:proj-)?[a-z0-9_-]{20,}|gh[pousr]_[a-z0-9]{20,}|AIza[a-z0-9_-]{25,}|----' + '-BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----|(?:password|api[_-]?key|secret|bearer|token)\s*[:=]\s*[\x22\x27]?[a-z0-9_+\-/=]{18,})')) { $skipped++; continue }
  $target=Join-Path $destination $file.Name
  if (Test-Path -LiteralPath $target) {
    $sourceHash=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    $previousHash=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if ($previousHash -eq $sourceHash) { $unchanged++; continue }
    if ($DryRun) { Write-Output ("WOULD_UPDATE "+$file.Name+" (preserve old SHA256="+$previousHash.Substring(0,12)+")"); continue }
    # Archive the previous Drive document before changing the canonical Drive path.
    $historyDir=Join-Path (Join-Path $destination '_previous_versions') $file.Name
    [IO.Directory]::CreateDirectory($historyDir) | Out-Null
    $historyFile=Join-Path $historyDir ($file.BaseName+'__prior_'+$previousHash.Substring(0,12)+$file.Extension)
    if (-not (Test-Path -LiteralPath $historyFile)) {
      Copy-Item -LiteralPath $target -Destination $historyFile -ErrorAction Stop
      $archived++
    }
    if ((Get-FileHash -LiteralPath $historyFile -Algorithm SHA256).Hash -ne $previousHash) { throw ("Previous version archive failed verification: "+$file.Name) }
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $previousHash) { throw ("Drive document changed during sync: "+$file.Name) }
    if ((Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash -ne $sourceHash) { throw ("Local source changed during sync: "+$file.Name) }
    Copy-Item -LiteralPath $file.FullName -Destination $target -Force -ErrorAction Stop
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $sourceHash) { throw ("Updated document hash mismatch: "+$file.Name) }
    Write-Output ("UPDATED "+$file.Name+" (old version archived)")
    $updated++
    continue
  }
  if ($DryRun) { Write-Output ("WOULD_COPY "+$file.Name); continue }
  Copy-Item -LiteralPath $file.FullName -Destination $target -ErrorAction Stop
  if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash) { throw ("Hash verification failed: "+$file.Name) }
  $copied++
}
Write-Output ("SYNC copied="+$copied+" updated="+$updated+" archived="+$archived+" unchanged="+$unchanged+" skipped="+$skipped+" scanned="+$files.Count+" dryRun="+$DryRun.IsPresent)