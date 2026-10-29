# One-time import of an existing OPENROUTER_API_KEY from PBOX1, no key printed.
# OpenFill runtime reads only the resulting DPAPI-encrypted file.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security
$target = 'C:\Users\pboru\OpenFill\data\stable\runtime\jev-api-key.bin'
$candidates = @(
  'C:\Users\pboru\zakazanawieza.txt',
  'C:\Users\pboru\Documents\Projects\AI-Radar\config\local\secrets.env',
  'C:\Users\pboru\Documents\Projects\BusinessContactRadar\config\local\secrets.env',
  'C:\Users\pboru\Documents\Projects\Networking-Radar\config\secrets.env'
)
if (Test-Path -LiteralPath $target) {
  Write-Output 'JEV_KEY_ALREADY_PRESENT=True'
  exit 0
}
foreach ($source in $candidates) {
  if (-not (Test-Path -LiteralPath $source)) { continue }
  foreach ($line in [IO.File]::ReadAllLines($source)) {
    if ($line -notmatch '^\s*(?:export\s+)?OPENROUTER_API_KEY\s*=\s*(.+?)\s*$') { continue }
    $key = $Matches[1].Trim().Trim('"', "'")
    if (-not $key.StartsWith('sk-or-') -or $key.Length -lt 35 -or $key -match '\s') { continue }
    $raw = [Text.Encoding]::UTF8.GetBytes($key)
    $sealed = [Security.Cryptography.ProtectedData]::Protect($raw, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
    $unsealed = [Security.Cryptography.ProtectedData]::Unprotect($sealed, $null, [Security.Cryptography.DataProtectionScope]::CurrentUser)
    if (-not [Linq.Enumerable]::SequenceEqual([byte[]]$raw, [byte[]]$unsealed)) { throw 'DPAPI roundtrip mismatch' }
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
    [IO.File]::WriteAllBytes($target, $sealed)
    [Array]::Clear($raw,0,$raw.Length)
    [Array]::Clear($unsealed,0,$unsealed.Length)
    $key = $null
    Write-Output 'JEV_KEY_IMPORT=PASS'
    Write-Output ('SOURCE_FILE='+[IO.Path]::GetFileName([IO.Path]::GetDirectoryName($source)))
    Write-Output ('DPAPI_ENCRYPTED_BYTES='+[IO.FileInfo]::new($target).Length)
    exit 0
  }
}
Write-Output 'JEV_KEY_IMPORT=NO_LOCAL_OPENROUTER_KEY_FOUND'
exit 4
