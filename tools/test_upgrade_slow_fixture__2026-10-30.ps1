$ErrorActionPreference = 'Stop'
$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add('http://127.0.0.1:60177/')
$listener.Start()
Write-Output 'UPGRADE_FIXTURE_READY port=60177'
try {
    while ($listener.IsListening) {
        $context = $listener.GetContext()
        Write-Output 'UPGRADE_FIXTURE_REQUEST_RECEIVED'
        Start-Sleep -Seconds 90
        $html = '<!doctype html><html><title>Upgrade test fixture</title><body>Safe local test page. No external changes.</body></html>'
        $bytes = [Text.Encoding]::UTF8.GetBytes($html)
        $context.Response.ContentType = 'text/html; charset=utf-8'
        $context.Response.ContentLength64 = $bytes.Length
        $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
        $context.Response.Close()
    }
} finally { $listener.Stop(); $listener.Close() }
