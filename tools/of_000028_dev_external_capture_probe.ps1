$ErrorActionPreference = 'Stop'
$captures = Join-Path $env:USERPROFILE 'OpenFill\shared\captures'
$external = Join-Path $env:TEMP ('of028_external_' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $captures -Force | Out-Null
New-Item -ItemType Directory -Path $external -Force | Out-Null
$id = 'capture_' + (Get-Date -Format 'yyyyMMdd_HHmmss_fff') + '_' + [guid]::NewGuid().ToString('N').Substring(0,6)
$source = Join-Path $external ($id + '.txt')
$manifest = Join-Path $external ($id + '.manifest.json')
$indexPath = Join-Path $captures ($id + '.external-index.json')
$output = $null
$stderr = $null
try {
    [IO.File]::WriteAllText($source, 'OF028_EXTERNAL_CAPTURE_MCP_OK', [Text.UTF8Encoding]::new($false))
    $sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    $manifestData = @{
        capture_id = $id
        source_scope = 'fixture_only'
        source_url = 'https://example.test/external'
        manifest_path = $manifest
        files = @(@{kind='text';path=$source;bytes=(Get-Item -LiteralPath $source).Length;sha256=$sourceHash})
    }
    [IO.File]::WriteAllText($manifest, (ConvertTo-Json -InputObject $manifestData -Depth 8 -Compress), [Text.UTF8Encoding]::new($false))
    $manifestHash = (Get-FileHash -LiteralPath $manifest -Algorithm SHA256).Hash
    $index = @{
        capture_id=$id;manifest_path=$manifest;manifest_sha256=$manifestHash;source_directory=$external
    }
    [IO.File]::WriteAllText($indexPath, (ConvertTo-Json -InputObject $index -Compress), [Text.UTF8Encoding]::new($false))
    $secret = [IO.File]::ReadAllText('C:\Users\pboru\OpenFill\data\dev\runtime\mcp-secret.txt').Trim()
    $endpoint = 'http://127.0.0.1:48732/mcp/' + $secret
    $body = @{
        jsonrpc='2.0';id=28;method='tools/call'
        params=@{name='openfill_query_capture';arguments=@{
            capture_id=$id;file_kind='text';operation='text_literal';query='OF028_EXTERNAL_CAPTURE_MCP_OK'
        }}
    } | ConvertTo-Json -Depth 10 -Compress
    $response = Invoke-RestMethod -Uri $endpoint -Method Post -Body ([Text.Encoding]::UTF8.GetBytes($body)) -ContentType 'application/json; charset=utf-8' -TimeoutSec 30
    if ($response.error) { throw ('OF028_RPC_ERROR_' + $response.error.code) }
    $data = $response.result.content[0].text | ConvertFrom-Json
    $output = $data.output_path
    $stderr = $data.stderr_path
    if ($data.status -ne 'ok' -or -not $data.reused_snapshot -or $data.capture_id -ne $id -or $data.preview[0] -notmatch 'OF028_EXTERNAL_CAPTURE_MCP_OK') {
        throw ('OF028_RUNTIME_PROBE_FAILED_' + (ConvertTo-Json $data -Compress -Depth 5))
    }
    Write-Output 'OF028_DEV_MCP_EXTERNAL_CAPTURE_OK'
    Write-Output ('CAPTURE_ID=' + $id)
    Write-Output ('MATCHED_LINES=' + $data.output_lines)
} finally {
    if ($output) { Remove-Item -LiteralPath $output -Force -ErrorAction SilentlyContinue }
    if ($stderr) { Remove-Item -LiteralPath $stderr -Force -ErrorAction SilentlyContinue }
    Remove-Item -LiteralPath $indexPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $external -Recurse -Force -ErrorAction SilentlyContinue
}
