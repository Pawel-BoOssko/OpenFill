# OpenFill - Metadata: wersja 0.1, data 2026-10-18 15:07
# Isolated test of Drive docs backup+update, zero production edits.
$ErrorActionPreference='Stop'
$sync=Join-Path $PSScriptRoot 'sync_openfill_docs_gdrive__v0.1__2026-10-15_2343.ps1'
$testDir=Join-Path $env:TEMP ('openfill_sync_fixture_'+[guid]::NewGuid().ToString('N'))
$source=Join-Path $testDir 'local'
$target=Join-Path $testDir 'remote'
[IO.Directory]::CreateDirectory($source) | Out-Null
[IO.Directory]::CreateDirectory($target) | Out-Null
$checks=0
function Verify([bool]$ok,[string]$description) {
    if(-not $ok) { throw ('FAIL '+$description) }
    Write-Output ('PASS '+$description)
    $script:checks++
}
try {
    [IO.File]::WriteAllText((Join-Path $source 'report.md'),'Newest approved document')
    [IO.File]::WriteAllText((Join-Path $target 'report.md'),'Older approved document')
    [IO.File]::WriteAllText((Join-Path $source 'same.txt'),'Identical')
    [IO.File]::WriteAllText((Join-Path $target 'same.txt'),'Identical')
    [IO.File]::WriteAllText((Join-Path $source 'new.md'),'New file')
    $preview=@(& $sync -DryRun -TestSource $source -TestDestination $target)
    $previewText=$preview -join [Environment]::NewLine
    Verify ($previewText -match 'WOULD_UPDATE report.md') 'dry run detects old document'
    Verify ($previewText -match 'WOULD_COPY new.md') 'dry run detects new document'
    Verify (([IO.File]::ReadAllText((Join-Path $target 'report.md'))) -eq 'Older approved document') 'dry run leaves older copy intact'
    $first=@(& $sync -TestSource $source -TestDestination $target)
    Verify ((($first -join ' ') -match 'updated=1 archived=1')) 'one update with one previous version'
    Verify (([IO.File]::ReadAllText((Join-Path $target 'report.md'))) -eq 'Newest approved document') 'canonical Drive document now current'
    $history=Join-Path $target '_previous_versions\report.md'
    $historyFiles=@(Get-ChildItem -LiteralPath $history -File)
    Verify ($historyFiles.Count -eq 1) 'exactly one older revision archived'
    Verify (([IO.File]::ReadAllText($historyFiles[0].FullName)) -eq 'Older approved document') 'archived file contains previous content'
    $second=@(& $sync -TestSource $source -TestDestination $target)
    Verify ((($second -join ' ') -match 'updated=0 archived=0 unchanged=3')) 'second run idempotent, no repeated conflict'
    [IO.File]::WriteAllText((Join-Path $target 'report.md'),'Other writer changed Drive document')
    $third=@(& $sync -TestSource $source -TestDestination $target)
    Verify ((($third -join ' ') -match 'updated=1 archived=1')) 'second divergent Drive version archived'
    Verify ((@(Get-ChildItem -LiteralPath $history -File)).Count -eq 2) 'both past Drive versions retained'
    Verify (([IO.File]::ReadAllText((Join-Path $target 'report.md'))) -eq 'Newest approved document') 'local authoritative content restored'
    Write-Output ('TOTAL '+$checks+' PASS, 0 FAIL')
} finally {
    if (Test-Path -LiteralPath $testDir) { Remove-Item -LiteralPath $testDir -Force -Recurse -ErrorAction SilentlyContinue }
}