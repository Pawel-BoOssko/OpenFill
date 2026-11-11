$ErrorActionPreference='Stop'
$root='C:\Users\pboru\Downloads\OpenFill__v0.1__2026-10-04_1911\OpenFill'
$stage=Join-Path $root 'staging\of_imp_20261031\OpenFill.Core.dll'
$live='C:\Users\pboru\OpenFill\app\OpenFill.Core.dll'
$exe='C:\Users\pboru\OpenFill\app\OpenFill.exe'
$want=(Get-FileHash $stage -Algorithm SHA256).Hash
$actual=(Get-FileHash $live -Algorithm SHA256).Hash
if($want -ne $actual){throw 'E03_INDEPENDENT_STAGE_LIVE_SHA_MISMATCH'}
$proc=@(Get-CimInstance Win32_Process -Filter "name='OpenFill.exe'" | Where-Object {$_.ExecutablePath -ieq $exe})
if($proc.Count -ne 1){throw "E03_INDEPENDENT_LIVE_PID_COUNT_$($proc.Count)"}
$task=Get-ScheduledTask -TaskName 'OpenFill-open'
if($task.State -ne 'Running'){throw "E03_INDEPENDENT_LIVE_PHASE_$($task.State)"}
$secret=[IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\stable\runtime\mcp-secret.txt').Trim()
$body='{"jsonrpc":"2.0","id":20261104,"method":"tools/call","params":{"name":"openfill_info","arguments":{}}}'
$reply=Invoke-RestMethod -Uri ('http://127.0.0.1:48731/mcp/'+$secret) -Method POST -Body $body -ContentType 'application/json' -TimeoutSec 10
if($reply.error){throw "E03_MCP_RPC_ERROR_$($reply.error.code)"}
$info=$reply.result.content[0].text | ConvertFrom-Json
if($null -eq $info.working){throw 'E03_MCP_INFO_INVALID'}
Write-Output "E03_INDEPENDENT_LIVE_OK pid=$($proc[0].ProcessId) exe=$exe phase=$($task.State) core_sha=$actual mcp=True working=$(@($info.working).Count)"
