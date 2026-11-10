$ErrorActionPreference = 'Stop'
$root = Join-Path $env:USERPROFILE 'OpenFill\shared\captures'
$folder = Join-Path $root 'of027-probe'
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$id = 'capture_' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff') + '_' + [guid]::NewGuid().ToString('N').Substring(0,6)
$source = Join-Path $folder ($id + '.txt')
$manifest = Join-Path $folder ($id + '.manifest.json')
$output = $null
$stderr = $null
try {
    [IO.File]::WriteAllText($source, 'OF027_MCP_NESTED_OK', [Text.UTF8Encoding]::new($false))
    $fileHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    $record = @{
        capture_id = $id
        source_scope = 'fixture_only'
        source_url = 'https://example.test'
        completeness = 'partial'
        files = @(@{ kind = 'text'; path = $source; bytes = (Get-Item -LiteralPath $source).Length; sha256 = $fileHash })
    }
    [IO.File]::WriteAllText($manifest, (ConvertTo-Json -InputObject $record -Depth 8 -Compress), [Text.UTF8Encoding]::new($false))
    $secret = [IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt').Trim()
    $endpoint = 'http://127.0.0.1:48732/mcp/' + $secret
    $body = @{
        jsonrpc = '2.0'; id = 27; method = 'tools/call'
        params = @{
            name = 'openfill_query_capture'
            arguments = @{ capture_id = $id; file_kind = 'text'; operation = 'text_literal'; query = 'OF027_MCP_NESTED_OK' }
        }
    } | ConvertTo-Json -Depth 10 -Compress
    $response = Invoke-RestMethod -Uri $endpoint -Method Post -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json; charset=utf-8' -TimeoutSec 30
    if ($response.error) { throw ('MCP_ERROR_'+$response.error.code) }
    $data = $response.result.content[0].text | ConvertFrom-Json
    $output = $data.output_path
    $stderr = $data.stderr_path
    if ($data.status -ne 'ok' -or -not $data.reused_snapshot -or $data.capture_id -ne $id -or $data.preview[0] -notmatch 'OF027_MCP_NESTED_OK') {
        throw ('OF027_DEV_MCP_PROBE_FAILED ' + (ConvertTo-Json $data -Depth 6 -Compress))
    }
    'OF027_DEV_MCP_NESTED_CAPTURE_OK'
    'CAPTURE=' + $id
    'OUTPUT_LINES=' + $data.output_lines
} finally {
    if ($output) { Remove-Item -LiteralPath $output -Force -ErrorAction SilentlyContinue }
    if ($stderr) { Remove-Item -LiteralPath $stderr -Force -ErrorAction SilentlyContinue }
    Remove-Item -LiteralPath $folder -Recurse -Force -ErrorAction SilentlyContinue
}
