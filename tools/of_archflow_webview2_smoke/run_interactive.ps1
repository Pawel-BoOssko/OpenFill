$ErrorActionPreference = 'Continue'
$folder = Split-Path -Parent $MyInvocation.MyCommand.Path
$log = Join-Path $folder 'interactive.log'
$exitFile = Join-Path $folder 'interactive.exit'
$dll = Join-Path $folder 'bin\Debug\net10.0-windows\ArchflowWebView2Smoke.dll'
Remove-Item -LiteralPath $exitFile -ErrorAction SilentlyContinue
'OF-000012 WEBVIEW2 INTERACTIVE START ' + (Get-Date -Format o) | Set-Content -LiteralPath $log -Encoding UTF8
'SESSION=' + (Get-Process -Id $PID).SessionId | Add-Content -LiteralPath $log
& 'C:\Program Files\dotnet\dotnet.exe' $dll 2>&1 | Out-File -LiteralPath $log -Append -Encoding UTF8
$exit = $LASTEXITCODE
'EXIT=' + $exit | Add-Content -LiteralPath $log
$exit | Set-Content -LiteralPath $exitFile
