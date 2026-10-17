$ErrorActionPreference = 'Stop'
$path = Join-Path (Split-Path -Parent $PSScriptRoot) 'tests\OpenFill.Tests\Program__v0.21__2026-10-14_2259.cs'
$src = Get-Content -LiteralPath $path -Raw
$names = @('openfill_test_overflow','openfill_test_logs','openfill_panel.png')
$found = 0
foreach ($name in $names) {
    if ($src.Contains('"' + $name + '"')) {
        Write-Output ('OPEN: shared temp name ' + $name)
        $found++
    } else {
        Write-Output ('PASS: no shared temp name ' + $name)
    }
}
Write-Output ('UnisolatedNames={0}; source not modified' -f $found)
