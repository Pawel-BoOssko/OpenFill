# Restore a portable OpenFill state archive to an EMPTY, ISOLATED destination.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$Archive,[Parameter(Mandatory)][string]$Manifest,
      [Parameter(Mandatory)][string]$Identity,[Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference='Stop'
$age=(Get-Command 'age.exe' -ErrorAction Stop).Source
$tar=(Get-Command 'tar.exe' -ErrorAction Stop).Source
foreach($f in @($Archive,$Manifest,$Identity)){
    if(-not (Test-Path -LiteralPath $f -PathType Leaf)){throw 'Encrypted archive, manifest or identity file missing'}
}
$dest=[IO.Path]::GetFullPath($Destination).TrimEnd('\')
if(Test-Path -LiteralPath $dest){throw 'Destination must not exist; refuse to overwrite profiles'}
$work=Join-Path $env:TEMP ('OpenFillRestoreWork__v0.1__'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($work)|Out-Null
$acl=Get-Acl -LiteralPath $work
$acl.SetAccessRuleProtection($true,$false)
$current=[Security.Principal.WindowsIdentity]::GetCurrent().User
$rule=New-Object System.Security.AccessControl.FileSystemAccessRule($current,'FullControl','ContainerInherit,ObjectInherit','None','Allow')
$acl.AddAccessRule($rule)
Set-Acl -LiteralPath $work -AclObject $acl
$manifestPlain=Join-Path $work 'manifest.json'
$tarPlain=Join-Path $work 'data.tar'
$verified=0
try{
    & $age -d -i $Identity -o $manifestPlain $Manifest
    if($LASTEXITCODE -ne 0){throw 'Manifest authentication/decryption failed'}
    $m=Get-Content -LiteralPath $manifestPlain -Raw | ConvertFrom-Json
    if($m.schema -ne 'OpenFillPortableAge/1'){throw 'Unknown backup manifest schema'}
    $expected=@{}
    foreach($entry in $m.files){
        $rel=[string]$entry.path
        if([string]::IsNullOrWhiteSpace($rel) -or [IO.Path]::IsPathRooted($rel) -or $rel -match '(^|[\\/])\.\.([\\/]|$)|:|^[\\/]'){throw 'Unsafe path in backup manifest'}
        if($expected.ContainsKey($rel)){throw 'Duplicate backup manifest path'}
        $expected.Add($rel,$entry)
    }
    & $age -d -i $Identity -o $tarPlain $Archive
    if($LASTEXITCODE -ne 0){throw 'Archive authentication/decryption failed'}
    $members=@(& $tar -tf $tarPlain)
    if($LASTEXITCODE -ne 0){throw 'Archive listing failed'}
    foreach($member in $members){
        $name=[string]$member
        if([IO.Path]::IsPathRooted($name) -or $name -match '(^|[\\/])\.\.([\\/]|$)|:|^[\\/]'){throw 'Unsafe path in tar'}
    }
    [IO.Directory]::CreateDirectory($dest)|Out-Null
    $dacl=Get-Acl -LiteralPath $dest
    $dacl.SetAccessRuleProtection($true,$false)
    $dacl.AddAccessRule($rule)
    Set-Acl -LiteralPath $dest -AclObject $dacl
    & $tar -xf $tarPlain -C $dest
    if($LASTEXITCODE -ne 0){throw 'Tar extraction failed'}
    $actual=@(Get-ChildItem -LiteralPath $dest -Recurse -File -Force)
    if($actual.Count -ne $expected.Count){throw 'Restore file count mismatch'}
    foreach($item in $actual){
        if($item.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Unsafe symlink after extraction'}
        $rel=$item.FullName.Substring($dest.Length).TrimStart('\')
        if(-not $expected.ContainsKey($rel)){throw 'Unexpected file in restored archive'}
        $want=$expected[$rel]
        if($item.Length -ne [long]$want.length){throw 'Restore length mismatch'}
        if((Get-FileHash -LiteralPath $item.FullName -Algorithm SHA256).Hash -ne $want.sha256){throw 'Restore SHA256 mismatch'}
        $verified++
    }
    [IO.File]::WriteAllText((Join-Path $dest 'RESTORE_VERIFIED.txt'),
        ('Verified '+$verified+' files. This is an isolated recovery copy, not a live OpenFill profile.'))
    [pscustomobject]@{Status='RESTORE_VERIFIED';Files=$verified;Destination=$dest;SourceSchema=$m.schema} | Format-List
}
finally{
    if(Test-Path -LiteralPath $work){Remove-Item -LiteralPath $work -Recurse -Force}
}