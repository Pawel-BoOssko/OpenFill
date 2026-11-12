# 2026-11-12 15:53 CET | GPT-6 | OpenFill | start MCP tasks with per-task safeguards.
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$Task,
    [Parameter(Mandatory=$true)][string]$Requester,
    [Parameter(Mandatory=$true)][string]$Chat,
    [ValidateRange(0.001,50)][double]$MaxCostUsd=0.05,
    [ValidateRange(10,36000)][int]$MaxDurationSeconds=300,
    [ValidateRange(15,3600)][int]$MaxIdleSeconds=90,
    [ValidateRange(0,30)][int]$WaitSeconds=0,
    [string]$Title,
    [string]$IdempotencyKey,
    [ValidateSet('stable','dev')][string]$Instance='stable',
    [switch]$ValidateOnly
)
$ErrorActionPreference='Stop'
if ([string]::IsNullOrWhiteSpace($Task) -or
    [string]::IsNullOrWhiteSpace($Requester) -or
    [string]::IsNullOrWhiteSpace($Chat)) {
    throw 'TASK_REQUESTER_CHAT_REQUIRED'
}
$args = @{
    task=$Task
    requester=$Requester
    chat=$Chat
    max_cost_usd=$MaxCostUsd
    max_duration_seconds=$MaxDurationSeconds
    max_idle_seconds=$MaxIdleSeconds
    wait_seconds=$WaitSeconds
}
if ($Title) {$args.title=$Title}
if ($IdempotencyKey) {$args.idempotency_key=$IdempotencyKey}
if ($ValidateOnly) {
    @{validated=$true;task_parameters=$args;estimated_model_cost_before_run=0} |
        ConvertTo-Json -Depth 9
    return
}
$data=Join-Path $env:USERPROFILE ('OpenFill\data\'+$Instance)
$config=Get-Content (Join-Path $data 'config.json') -Raw | ConvertFrom-Json
$port=[int]$config.mcpPort
if ($port -le 0) {throw ('MCP_FIXED_PORT_REQUIRED: '+$port)}
$mcpUrlFile=Join-Path $data 'runtime\mcp-url.txt'
if (-not (Test-Path $mcpUrlFile)) {throw ('MCP_URL_FILE_NOT_FOUND: '+$mcpUrlFile)}
$original=[uri]([IO.File]::ReadAllText($mcpUrlFile).Trim())
$endpoint='http://127.0.0.1:'+$port+$original.AbsolutePath
$rpc=@{jsonrpc='2.0';id=1;method='tools/call';params=@{name='openfill_start_task';arguments=$args}}
$body=$rpc|ConvertTo-Json -Depth 12 -Compress
try {$answer=Invoke-RestMethod -Uri $endpoint -Method Post -ContentType 'application/json' -Body $body -TimeoutSec 45}
catch {throw ('OPENFILL_MCP_REQUEST_FAILED: '+$_.Exception.Message)}
if ($answer.error) {throw ('MCP_RPC_ERROR: '+($answer.error|ConvertTo-Json -Compress))}
$entry=@($answer.result.content|Where-Object {$_.type -eq 'text'})|Select-Object -First 1
if (-not $entry) {throw 'MCP_MISSING_TEXT_RESPONSE'}
$out=$entry.text|ConvertFrom-Json
$out|ConvertTo-Json -Depth 18
if ($out.status -eq 'error') {exit 2}

