# OpenFill portable backup, encrypted with a RECIPIENT PUBLIC KEY only. Source must be offline.
[CmdletBinding()]
param([switch]$SelfTest,[string]$SourceDirectory,[string]$BackupRoot,[string]$Recipient)
$ErrorActionPreference='Stop'
$stamp=Get-Date -Format 'yyyy-MM-dd_HHmmss'
$age=(Get-Command 'age.exe' -ErrorAction Stop).Source
$tar=(Get-Command 'tar.exe' -ErrorAction Stop).Source
$testRoot=$null
if($SelfTest){
    $testRoot=Join-Path $env:TEMP ('OpenFill_age_fixture__v0.1__'+$stamp)
    $SourceDirectory=Join-Path $testRoot 'input'
    $BackupRoot=Join-Path $testRoot 'encrypted'
    [IO.Directory]::CreateDirectory((Join-Path $SourceDirectory 'tasks'))|Out-Null
    [IO.Directory]::CreateDirectory((Join-Path $SourceDirectory 'notes'))|Out-Null
    [IO.File]::WriteAllText((Join-Path $SourceDirectory 'tasks\synthetic.json'),'{"demo":"no secrets"}')
    $bytes=New-Object byte[] (1024*1024+127)
    $rng=[Security.Cryptography.RandomNumberGenerator]::Create(); $rng.GetBytes($bytes); $rng.Dispose()
    [IO.File]::WriteAllBytes((Join-Path $SourceDirectory 'notes\synthetic.bin'),$bytes)
    # Keep the private test key in a directory accessible only to the current Windows identity.
    $acl=Get-Acl -LiteralPath $testRoot
    $acl.SetAccessRuleProtection($true,$false)
    $current=[Security.Principal.WindowsIdentity]::GetCurrent().User
    $rule=New-Object System.Security.AccessControl.FileSystemAccessRule($current,'FullControl','ContainerInherit,ObjectInherit','None','Allow')
    $acl.AddAccessRule($rule)
    Set-Acl -LiteralPath $testRoot -AclObject $acl
    $identity=Join-Path $testRoot 'synthetic_identity.txt'
    $savedEap=$ErrorActionPreference
    $ErrorActionPreference='Continue'
    try { & age-keygen.exe -o $identity 2>$null | Out-Null }
    finally { $ErrorActionPreference=$savedEap }
    if($LASTEXITCODE -ne 0){throw 'Synthetic key generator failed'}
    $Recipient=(& age-keygen.exe -y $identity).Trim()
}
else{
    if([string]::IsNullOrWhiteSpace($SourceDirectory) -or [string]::IsNullOrWhiteSpace($BackupRoot)){throw 'Explicit source and destination required'}
    if((Get-Process -Name OpenFill -ErrorAction SilentlyContinue)){throw 'OpenFill still running; encrypted production backup requires offline state'}
}
if($Recipient -notmatch '^age1[023456789acdefghjklmnpqrstuvwxyz]{20,}$'){throw 'Require valid age public recipient, never a private key'}
$src=[IO.Path]::GetFullPath($SourceDirectory).TrimEnd('\')
$base=[IO.Path]::GetFullPath($BackupRoot).TrimEnd('\')
if(-not (Test-Path -LiteralPath $src -PathType Container)){throw 'Source missing'}
if($base.StartsWith(($src+'\'),[StringComparison]::OrdinalIgnoreCase) -or $base -eq $src){throw 'Destination cannot be inside source'}
$items=@(Get-ChildItem -LiteralPath $src -Recurse -Force)
foreach($node in $items){if($node.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Refusing symlink or junction in backup source'}}
[IO.Directory]::CreateDirectory($base)|Out-Null
$archive=Join-Path $base ('OpenFill-state-portable__v0.1__'+$stamp+'.tar.age')
$manifest=Join-Path $base ('OpenFill-manifest-portable__v0.1__'+$stamp+'.json.age')
$records=New-Object 'System.Collections.Generic.List[object]'
foreach($item in $items){
    if($item.PSIsContainer){continue}
    $relative=$item.FullName.Substring($src.Length).TrimStart('\')
    if($relative -match '^(logs|downloads)\\'){continue}
    $hash=(Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash
    $records.Add([pscustomobject]@{path=$relative;length=$item.Length;sha256=$hash})
}
# Encrypt manifest directly from memory, never put sensitive relative paths in a plaintext temporary file.
$json=[pscustomobject]@{schema='OpenFillPortableAge/1';created=(Get-Date -Format o);files=$records.ToArray()} | ConvertTo-Json -Depth 6
$mpi=[Diagnostics.ProcessStartInfo]::new($age,('-r '+$Recipient+' -o "'+$manifest+'" -'))
$mpi.UseShellExecute=$false
$mpi.RedirectStandardInput=$true
$mpi.RedirectStandardError=$true
$manifestProcess=[Diagnostics.Process]::new()
$manifestProcess.StartInfo=$mpi
try {
    if(-not $manifestProcess.Start()){throw 'Manifest encryption process did not start'}
    $manifestProcess.StandardInput.Write($json)
    $manifestProcess.StandardInput.Close()
    $manifestError=$manifestProcess.StandardError.ReadToEndAsync()
    $manifestProcess.WaitForExit()
    if($manifestProcess.ExitCode -ne 0){throw ('Manifest encryption failed, exit '+$manifestProcess.ExitCode)}
}
finally {$manifestProcess.Dispose()}$tarArgs='-cf - --exclude=./logs --exclude=./downloads -C "'+$src+'" .'
$ageArgs='-r '+$Recipient+' -o "'+$archive+'"'
$tp=[Diagnostics.ProcessStartInfo]::new($tar,$tarArgs)
$tp.UseShellExecute=$false;$tp.RedirectStandardOutput=$true;$tp.RedirectStandardError=$true
$ap=[Diagnostics.ProcessStartInfo]::new($age,$ageArgs)
$ap.UseShellExecute=$false;$ap.RedirectStandardInput=$true;$ap.RedirectStandardError=$true
$t=[Diagnostics.Process]::new();$t.StartInfo=$tp
$a=[Diagnostics.Process]::new();$a.StartInfo=$ap
try {
    if(-not $t.Start()){throw 'tar did not start'}
    if(-not $a.Start()){throw 'age did not start'}
    $tarError=$t.StandardError.ReadToEndAsync()
    $ageError=$a.StandardError.ReadToEndAsync()
    $t.StandardOutput.BaseStream.CopyTo($a.StandardInput.BaseStream)
    $a.StandardInput.Close()
    $t.WaitForExit();$a.WaitForExit()
    if($t.ExitCode -ne 0 -or $a.ExitCode -ne 0){throw ('archive/encryption failed: tar='+$t.ExitCode+' age='+$a.ExitCode)}
}
finally{$t.Dispose();$a.Dispose()}
if((Get-Item -LiteralPath $archive).Length -lt 100){throw 'Encrypted archive is empty'}
if($SelfTest){
    $tarOut=Join-Path $testRoot 'roundtrip.tar'
    $restored=Join-Path $testRoot 'restore'
    [IO.Directory]::CreateDirectory($restored)|Out-Null
    & $age -d -i $identity -o $tarOut $archive
    if($LASTEXITCODE -ne 0){throw 'Synthetic decrypt failed'}
    & $tar -xf $tarOut -C $restored
    if($LASTEXITCODE -ne 0){throw 'Synthetic extract failed'}
    $restoredManifest=Join-Path $testRoot 'manifest_restored.json'
    & $age -d -i $identity -o $restoredManifest $manifest
    if($LASTEXITCODE -ne 0){throw 'Synthetic manifest decrypt failed'}
    $m=Get-Content -LiteralPath $restoredManifest -Raw | ConvertFrom-Json
    if($m.schema -ne 'OpenFillPortableAge/1' -or @($m.files).Count -ne $records.Count) {throw 'Encrypted manifest integrity failed'}    foreach($r in $records){
        $dest=Join-Path $restored $r.path
        if(-not (Test-Path -LiteralPath $dest -PathType Leaf)){throw 'Restore missing synthetic file'}
        if((Get-FileHash -LiteralPath $dest -Algorithm SHA256).Hash -ne $r.sha256){throw 'Synthetic hash mismatch'}
    }
}
if($SelfTest){[IO.Directory]::Delete($testRoot,$true)}
[pscustomobject]@{Status='AGE_BACKUP_OK';Synthetic=$SelfTest.IsPresent;EncryptedArchive=$archive;EncryptedManifest=$manifest;Files=$records.Count;VerifiedRestore=if($SelfTest){$records.Count}else{0};Portability='Requires separately preserved age private identity'} | Format-List