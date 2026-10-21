# OpenFill - tester package: start the app and show the MCP address. Metadata: wersja 0.1, data 2026-10-16 09:25
# Lives in the package as app\start-tester.ps1 and is run by "URUCHOM OpenFill.cmd".
$ErrorActionPreference = "Stop"
$app = $PSScriptRoot
$exe = Join-Path $app "OpenFill.exe"
$urlFile = Join-Path $env:USERPROFILE "OpenFill\data\stable\runtime\mcp-url.txt"

function Say([string]$t, [string]$c = "Gray") { Write-Host $t -ForegroundColor $c }

if (-not (Test-Path $exe)) { Say "Nie znaleziono $exe - rozpakuj cale archiwum ZIP i uruchom ponownie." Red; exit 1 }

$running = Get-Process -Name "OpenFill" -ErrorAction SilentlyContinue
if ($running) {
    Say "OpenFill juz dziala." Green
} else {
    # The address is written anew at every start (the quick tunnel address changes), so drop the old one first.
    Remove-Item $urlFile -Force -ErrorAction SilentlyContinue
    Say "Uruchamiam OpenFill..." Cyan
    Start-Process -FilePath $exe -WorkingDirectory $app
}

Say "Czekam na adres MCP (zwykle 10-40 sekund, przy pierwszym starcie do 2 minut)..."
$url = $null
for ($i = 0; $i -lt 120; $i++) {
    if (Test-Path $urlFile) {
        $u = (Get-Content $urlFile -Raw).Trim()
        if ($u) { $url = $u; break }
    }
    Start-Sleep -Seconds 1
}

Write-Host ""
if (-not $url) {
    Say "Adres jeszcze sie nie pojawil." Yellow
    Say "Jesli okno OpenFill sie nie otworzylo: zainstaluj WebView2 z https://go.microsoft.com/fwlink/p/?LinkId=2124703 i uruchom ponownie."
    Say "Jesli okno jest otwarte: odczekaj chwile i uruchom ten plik jeszcze raz."
    exit 1
}
if ($url -like "http://127.0.0.1*") {
    Say "UWAGA: tunel internetowy sie nie uruchomil - adres dziala tylko na tym komputerze:" Yellow
    Say "  $url"
    Say "Claude.ai i ChatGPT go nie zobacza. Sprawdz internet/firewall i uruchom OpenFill ponownie (zamknij okno OpenFill, potem ten plik)."
    exit 1
}
try { Set-Clipboard -Value $url } catch { }
Say "Adres MCP (skopiowany do schowka - wklej go w Claude lub ChatGPT):" Green
Write-Host ""
Say "  $url" White
Write-Host ""
Say "Traktuj go jak haslo. Adres zmienia sie po kazdym ponownym uruchomieniu OpenFill -"
Say "wtedy uruchom ten plik jeszcze raz i podmien adres w ustawieniach konektora."
Say "Szczegoly: INSTRUKCJA.txt"
