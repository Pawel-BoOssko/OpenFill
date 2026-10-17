# OpenFill private state backup: designed for OFFLINE source only. Synthetic test is safe while app runs.
[CmdletBinding()]
param([switch]$SelfTest,[string]$SourceDirectory,[string]$BackupRoot)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Security
$time=Get-Date -Format 'yyyy-MM-dd_HHmmss'
if($SelfTest) {
 $SourceDirectory=Join-Path $env:TEMP ('OpenFillSyntheticData__v0.1__'+$time)
 $BackupRoot=Join-Path $env:TEMP ('OpenFillSyntheticBackup__v0.1__'+$time)
 [IO.Directory]::CreateDirectory((Join-Path $SourceDirectory 'tasks'))|Out-Null
 [IO.Directory]::CreateDirectory((Join-Path $SourceDirectory 'notes'))|Out-Null
 [IO.File]::WriteAllText((Join-Path $SourceDirectory 'tasks\synthetic.json'),'{"task":"SYNTHETIC-ONLY","status":"finished"}')
 [IO.File]::WriteAllText((Join-Path $SourceDirectory 'notes\synthetic.txt'),'Test note. No real user data.')
 $bytes=New-Object byte[] (2*1024*1024+23); $random=[Security.Cryptography.RandomNumberGenerator]::Create(); $random.GetBytes($bytes); $random.Dispose(); [IO.File]::WriteAllBytes((Join-Path $SourceDirectory 'tasks\synthetic_large.bin'),$bytes)
}
else {
 if(-not $SourceDirectory -or -not $BackupRoot){throw 'Provide explicit SourceDirectory and private BackupRoot while app is OFFLINE. No default production access.'}
 if((Get-Process OpenFill -ErrorAction SilentlyContinue)){throw 'Offline gate: OpenFill.exe is running. No production data accessed.'}
}
$src=[IO.Path]::GetFullPath($SourceDirectory).TrimEnd('\')
$base=[IO.Path]::GetFullPath($BackupRoot).TrimEnd('\')
if(-not (Test-Path -LiteralPath $src -PathType Container)){throw 'Source directory missing'}
if($base.StartsWith($src,[StringComparison]::OrdinalIgnoreCase)){throw 'Backup root cannot be inside its source directory'}
[IO.Directory]::CreateDirectory($base)|Out-Null
$name='OpenFill-state-encrypted__v0.2__'+$time
$out=Join-Path $base $name
[IO.Directory]::CreateDirectory($out)|Out-Null
$records=New-Object 'System.Collections.Generic.List[object]'
$counter=0
foreach($f in (Get-ChildItem -LiteralPath $src -Recurse -File -Force)) {
 if($f.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Refusing reparse-point/symlink file in sensitive state'}
 $relative=$f.FullName.Substring($src.Length).TrimStart('\')
 if($relative -match '(?i)^logs\\'){continue}
 if($relative -match '(?i)^downloads\\'){continue}
 $counter++
 $fileName=('{0:D6}.dpapi' -f $counter)
 $encrypted=Join-Path $out $fileName
 $before=(Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash
 $input=[IO.File]::Open($f.FullName,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
 $output=[IO.File]::Open($encrypted,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
 try {
   $writer=New-Object IO.BinaryWriter($output)
   $writer.Write([Text.Encoding]::ASCII.GetBytes('OFDPA1'))
   $buffer=New-Object byte[] (1024*1024)
   while(($n=$input.Read($buffer,0,$buffer.Length)) -gt 0) {
     $raw=New-Object byte[] $n
     [Buffer]::BlockCopy($buffer,0,$raw,0,$n)
     $protected=[Security.Cryptography.ProtectedData]::Protect($raw,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
     $writer.Write([int]$protected.Length)
     $writer.Write([byte[]]$protected)
   }
   $writer.Flush()
 } finally { $input.Dispose(); $output.Dispose() }
 $after=(Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash
 if($after -ne $before){throw 'Concurrent source change; encrypted snapshot incomplete'}
 $records.Add([pscustomobject]@{ordinal=$fileName;relative=$relative;length=$f.Length;sha256=$before})
}
$manifest=[pscustomobject]@{schema='OpenFillOfflineDPAPI/1';createdLocal=(Get-Date -Format o);scope='Sensitive user data, except logs and downloads. Windows DPAPI CURRENT USER, NOT PORTABLE.';items=$records.ToArray()}
$bytes=[Text.Encoding]::UTF8.GetBytes(($manifest|ConvertTo-Json -Depth 6))
$encryptedManifest=[Security.Cryptography.ProtectedData]::Protect($bytes,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
$manifestFile=Join-Path $out ('manifest__v0.2__'+$time+'.dpapi')
[IO.File]::WriteAllBytes($manifestFile,$encryptedManifest)
$verify=[Text.Encoding]::UTF8.GetString([Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($manifestFile),$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)) | ConvertFrom-Json
if(@($verify.items).Count -ne $records.Count){throw 'Manifest verification failed'}
$verified=0
foreach($item in $verify.items) {
 $file=[IO.File]::OpenRead((Join-Path $out $item.ordinal))
 $sha=[Security.Cryptography.SHA256]::Create()
 try {
   $reader=New-Object IO.BinaryReader($file)
   if([Text.Encoding]::ASCII.GetString($reader.ReadBytes(6)) -ne 'OFDPA1'){throw 'Encrypted file magic mismatch'}
   $digestBytes=0
   while($file.Position -lt $file.Length) {
     $size=$reader.ReadInt32()
     if($size -le 0 -or $size -gt 3MB){throw 'Encrypted chunk invalid'}
     $part=$reader.ReadBytes($size)
     if($part.Length -ne $size){throw 'Encrypted chunk truncated'}
     $raw=[Security.Cryptography.ProtectedData]::Unprotect($part,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser)
     $null=$sha.TransformBlock($raw,0,$raw.Length,$raw,0); $digestBytes+=$raw.Length
   }
   $null=$sha.TransformFinalBlock([byte[]]@(),0,0); $hash=[BitConverter]::ToString($sha.Hash).Replace('-','')
   if($hash -ne $item.sha256){throw 'Restore hash mismatch'}
   $verified++
 } finally { $file.Dispose(); $sha.Dispose() }
}
[IO.File]::WriteAllText((Join-Path $out ('COMPLETE__v0.2__'+$time+'.txt')),'Encrypted, DPAPI current-user, verified items: '+$verified)
Write-Output ("ENCRYPTED_BACKUP_OK synthetic="+$SelfTest.IsPresent+" count="+$records.Count+" verified="+$verified+" destination="+$out+" portability=WINDOWS_SAME_USER_ONLY")
