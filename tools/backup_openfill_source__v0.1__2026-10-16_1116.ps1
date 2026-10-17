# OpenFill SOURCE and documentation backup. Does NOT touch production data/profiles.
# Stores private local snapshot, SHA-256 manifest, isolated restore validation.
[CmdletBinding()]
param()
$ErrorActionPreference='Stop'
$repo='C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill'
$base=Join-Path $env:USERPROFILE 'Documents\OpenFill-Backups'
$stamp=Get-Date -Format 'yyyy-MM-dd_HHmmss'
$name='OpenFill-source__v0.1__'+$stamp
$snapshot=Join-Path $base $name
$restore=Join-Path $base ('restore-verification__v0.1__'+$stamp)
if (-not (Test-Path -LiteralPath $repo -PathType Container)) { throw 'Source repository missing' }
[IO.Directory]::CreateDirectory($base) | Out-Null
[IO.Directory]::CreateDirectory($snapshot) | Out-Null
$included=New-Object 'System.Collections.Generic.List[object]'
$allowedDirs=@('src','tests','tools','docs')
foreach($top in $allowedDirs) {
  $directory=Join-Path $repo $top
  if (-not (Test-Path -LiteralPath $directory -PathType Container)) { throw ('Missing required directory: '+$top) }
  foreach($file in (Get-ChildItem -LiteralPath $directory -File -Recurse -ErrorAction Stop)) {
    $rel=$file.FullName.Substring($repo.Length).TrimStart('\')
    if($rel -match '(?i)(^|\\)(bin|obj|backups|cache|profiles|profile|data|stable|dev|node_modules|\.git|\.vs)(\\|$)') { continue }
    if($rel -match '(?i)(^|\\)(backup__v|experiments)([^\\]*)(\\|$)') { continue }
    if($file.Attributes -band [IO.FileAttributes]::ReparsePoint) { continue }
    if($file.Extension -in '.log','.ndjson','.db','.sqlite','.sqlite3','.pfx','.pem','.key','.bak','.zip','.7z','.exe','.dll') { continue }
    if($file.Name -match '(?i)(^\.env($|\.)|secret.*\.json$|token.*\.json$|password.*\.json$|credential.*\.json$)') { continue }
    if($top -eq 'docs' -and $file.Extension -notin '.md','.txt') { continue }
    if($file.Length -gt 20MB) { continue }
    $included.Add([pscustomobject]@{file=$file;rel=$rel})
  }
}
foreach($file in (Get-ChildItem -LiteralPath $repo -File)) {
 if($file.Extension -in '.sln','.props','.targets','.md','.cmd','.txt' -or $file.Name -in '.gitignore','LICENSE') {
   $included.Add([pscustomobject]@{file=$file;rel=$file.Name})
 }
}
$items=New-Object 'System.Collections.Generic.List[object]'
foreach($entry in $included) {
 $source=$entry.file.FullName
 $relative=$entry.rel
 $dest=Join-Path $snapshot $relative
 [IO.Directory]::CreateDirectory((Split-Path -Parent $dest)) | Out-Null
 Copy-Item -LiteralPath $source -Destination $dest -ErrorAction Stop
 $h=(Get-FileHash -LiteralPath $dest -Algorithm SHA256).Hash
 if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $h) { throw ('Concurrent modification or copy error: '+$relative) }
 $items.Add([pscustomobject]@{path=$relative;bytes=(Get-Item -LiteralPath $dest).Length;sha256=$h})
}
$manifest=Join-Path $snapshot ('manifest__v0.1__'+$stamp+'.json')
[pscustomobject]@{
 schema='OpenFillSourceBackup/1'
 createdLocal=(Get-Date -Format o)
 source=$repo
 scope='source + tests + tools + docs; excludes bin/obj, logs, credentials, profiles, runtime, backups'
 itemCount=$items.Count
 items=@($items)
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifest -Encoding utf8
[IO.Directory]::CreateDirectory($restore) | Out-Null
$errors=New-Object 'System.Collections.Generic.List[string]'
foreach($entry in $items) {
 $original=Join-Path $snapshot $entry.path
 $target=Join-Path $restore $entry.path
 [IO.Directory]::CreateDirectory((Split-Path -Parent $target)) | Out-Null
 Copy-Item -LiteralPath $original -Destination $target -ErrorAction Stop
 $restoredHash=(Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
 if($restoredHash -ne $entry.sha256){$errors.Add($entry.path)}
}
if($errors.Count -gt 0){throw ('RESTORE FAILED; mismatches='+$errors.Count+'; restore kept='+$restore)}
Remove-Item -LiteralPath $restore -Recurse -Force -ErrorAction Stop
Write-Output ("BACKUP_OK snapshot="+$snapshot+" files="+$items.Count+" restoreVerified="+$items.Count+" mismatches=0 manifest="+$manifest)
