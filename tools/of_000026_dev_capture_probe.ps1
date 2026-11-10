$ErrorActionPreference='Stop'
$secret=[IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt').Trim()
$endpoint='http://127.0.0.1:48732/mcp/'+$secret
$body=@{jsonrpc='2.0';id=26;method='tools/call';params=@{
    name='openfill_query_capture'
    arguments=@{
        capture_id='capture_20261103_033015_123_abcdef'
        file_kind='text'
        operation='text_literal'
        query='OF026_READONLY_PROBE'
    }
}} | ConvertTo-Json -Compress -Depth 10
$response=Invoke-RestMethod -Uri $endpoint -Method Post -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json; charset=utf-8' -TimeoutSec 15
if ($response.error) { throw ('DEV_RPC_ERROR_'+$response.error.code) }
$response.result.content[0].text
