param([Parameter(Mandatory=$true)][string]$TokenHex,
      [string]$Source='C:\Users\pboru\Documents\OpenFill-Backups\EncryptedState',
      [string]$Peer='100.121.221.23',
      [int]$Port=49372)
$ErrorActionPreference='Stop'
if($TokenHex -notmatch '^[0-9A-Fa-f]{64}$'){throw 'One-time transfer token format invalid'}
$token=New-Object byte[] 32
for($i=0;$i -lt 32;$i++){$token[$i]=[Convert]::ToByte($TokenHex.Substring($i*2,2),16)}
$names=@(
 [pscustomobject]@{name='OpenFill-state-portable__v0.2__2026-10-16_130304.tar.age';bytes=[long]1090191400},
 [pscustomobject]@{name='OpenFill-manifest-portable__v0.2__2026-10-16_130304.json.age';bytes=[long]2640047}
)
foreach($f in $names){
    $item=Get-Item -LiteralPath (Join-Path $Source $f.name) -ErrorAction Stop
    if($item.Length -ne $f.bytes){throw ('Encrypted file has wrong byte count: '+$f.name)}
}
$client=[Net.Sockets.TcpClient]::new()
try{
    $client.SendTimeout=600000
    $client.ReceiveTimeout=600000
    $client.Connect($Peer,$Port)
    $stream=$client.GetStream()
    $stream.Write($token,0,$token.Length)
    foreach($f in $names){
        $in=[IO.File]::OpenRead((Join-Path $Source $f.name))
        try{$in.CopyTo($stream,1024*1024)}
        finally{$in.Dispose()}
    }
    $stream.Flush()
    $ack=New-Object byte[] 2
    $got=0
    while($got -lt 2){
       $n=$stream.Read($ack,$got,2-$got)
       if($n -le 0){throw 'Receiver did not acknowledge encrypted backup'}
       $got+=$n
    }
    if([Text.Encoding]::ASCII.GetString($ack) -ne 'OK'){throw 'Receiver rejected encrypted transfer'}
    [pscustomobject]@{Status='ENCRYPTED_TRANSFER_ACKNOWLEDGED';Destination=$Peer;ArchiveBytes=1090191400;ManifestBytes=2640047} | Format-List
}
finally{$client.Dispose();[Array]::Clear($token,0,$token.Length)}