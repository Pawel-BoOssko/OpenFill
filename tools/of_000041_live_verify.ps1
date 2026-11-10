$ErrorActionPreference='Stop'
$live='C:\Users\pboru\OpenFill\app'
$exe=Join-Path $live 'OpenFill.exe'
$core=Join-Path $live 'OpenFill.Core.dll'
$sha=(Get-FileHash -LiteralPath $core -Algorithm SHA256).Hash
$exeSha=(Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
$proc=@(Get-CimInstance Win32_Process -Filter "Name='OpenFill.exe'" | Where-Object { $_.ExecutablePath -ieq $exe })
if($proc.Count -ne 1){throw "LIVE_PROCESS_COUNT_$($proc.Count)"}
$task=Get-ScheduledTask -TaskName 'OpenFill-open' -ErrorAction Stop
$secret=[System.IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\stable\runtime\mcp-secret.txt').Trim()
$body='{"jsonrpc":"2.0","id":41,"method":"tools/call","params":{"name":"openfill_info","arguments":{}}}'
$r=Invoke-RestMethod -Uri ('http://127.0.0.1:48731/mcp/'+$secret) -Method POST -Body $body -ContentType 'application/json' -TimeoutSec 10
if($r.error){throw "LIVE_MCP_ERROR $($r.error.message)"}
$s=$r.result.content[0].text | ConvertFrom-Json
Write-Output "LIVE_PID=$($proc[0].ProcessId)"
Write-Output "LIVE_EXE=$($proc[0].ExecutablePath)"
Write-Output "LIVE_CORE_SHA=$sha"
Write-Output "LIVE_EXE_SHA=$exeSha"
Write-Output "LIVE_PHASE=$($task.State)"
Write-Output "LIVE_MCP_OK=$($null -ne $s)"
Write-Output "LIVE_WORKING_COUNT=$(@($s.working).Count)"
if($sha -ne 'F04AFEE31930B5C4EA517993883BDD5D8956352C7C0CE967EBD2C15D3E7A4482'){throw 'LIVE_SHA_UNEXPECTED'}