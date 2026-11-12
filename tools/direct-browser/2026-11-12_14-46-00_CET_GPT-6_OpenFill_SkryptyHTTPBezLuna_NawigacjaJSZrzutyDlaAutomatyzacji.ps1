# 2026-11-12 14:46 CET | GPT-6 | OpenFill | deterministic browser scripting client without AI.
[CmdletBinding()]
param(
    [ValidateSet('status','tabs','new_tab','select_tab','current_url','navigate','screenshot','js','html','text','click','type','scroll','wait','batch')]
    [string]$Action = 'status',
    [ValidateSet('stable','dev')]
    [string]$Instance = 'stable',
    [int]$Port = 17873,
    [string]$TabId,
    [string]$Url,
    [string]$Script,
    [string]$Selector,
    [string]$Value,
    [string]$Folder,
    [string]$Filename,
    [int]$X,
    [int]$Y,
    [int]$Ms = 1000,
    [string]$CommandFile
)

$ErrorActionPreference = 'Stop'
$tokenPath = Join-Path $env:USERPROFILE "OpenFill\data\$Instance\runtime\direct-browser-api.token"
if (-not (Test-Path -LiteralPath $tokenPath)) {
    throw "TOKEN_NOT_FOUND: $tokenPath. OpenFill in instance '$Instance' is not running with direct control enabled."
}
$token = [IO.File]::ReadAllText($tokenPath).Trim()
if (-not $token) { throw "TOKEN_EMPTY: $tokenPath" }
if ($CommandFile) {
    if (-not (Test-Path -LiteralPath $CommandFile)) { throw "COMMAND_FILE_NOT_FOUND: $CommandFile" }
    $body = [IO.File]::ReadAllText((Resolve-Path $CommandFile))
} else {
    $command = @{ action = $Action }
    if ($TabId) { $command.tab_id = $TabId }
    if ($Url) { $command.url = $Url }
    if ($Script) { $command.script = $Script }
    if ($Selector) { $command.selector = $Selector }
    if ($PSBoundParameters.ContainsKey('Value')) { $command.value = $Value }
    if ($Folder) { $command.folder = $Folder }
    if ($Filename) { $command.filename = $Filename }
    if ($PSBoundParameters.ContainsKey('X')) { $command.x = $X }
    if ($PSBoundParameters.ContainsKey('Y')) { $command.y = $Y }
    if ($Action -eq 'wait') { $command.ms = $Ms }
    $body = $command | ConvertTo-Json -Depth 40 -Compress
}
$uri = "http://127.0.0.1:$Port/command"
try {
    $result = Invoke-RestMethod -Uri $uri -Method Post -Headers @{ 'X-OpenFill-Token' = $token } -ContentType 'application/json; charset=utf-8' -Body $body -TimeoutSec 65
} catch {
    $details = $_.Exception.Message
    if ($_.ErrorDetails.Message) { $details += ' ' + $_.ErrorDetails.Message }
    throw "DIRECT_API_ERROR: $uri $details"
}
$result | ConvertTo-Json -Depth 60
if ($result.ok -eq $false) { exit 2 }

