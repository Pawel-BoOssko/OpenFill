$ErrorActionPreference = 'Stop'
$dir = Join-Path $env:USERPROFILE 'OpenFill\data\stable\tasks'
$files = @(Get-ChildItem -LiteralPath $dir -Filter '*.json' -File)
$records = New-Object 'System.Collections.Generic.List[object]'
$invalid = 0
foreach ($file in $files) {
    try {
        $r = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
        $records.Add([pscustomobject]@{
            Status = [string]$r.Status
            OwnerKey = [string]$r.Requester
            IdempotencyKey = [string]$r.IdempotencyKey
        })
    } catch { $invalid++ }
}
$stale = @($records | Where-Object { $_.Status -in @('running','needs_input','waiting_for_user') }).Count
$keyed = @($records | Where-Object { -not [string]::IsNullOrWhiteSpace($_.IdempotencyKey) })
$duplicates = @($keyed | Group-Object OwnerKey,IdempotencyKey | Where-Object { $_.Count -gt 1 }).Count
Write-Output ('Files={0}; ValidJSON={1}; InvalidJSON={2}' -f $files.Count,$records.Count,$invalid)
Write-Output ('StaleOpenStatuses={0}; IdempotencyRecords={1}; DuplicateOwnerKeys={2}' -f $stale,$keyed.Count,$duplicates)
Write-Output 'READ-ONLY AUDIT: no task data changed'
if ($invalid -gt 0) { throw 'Invalid task records found' }
