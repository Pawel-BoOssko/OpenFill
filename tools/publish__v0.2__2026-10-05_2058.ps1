# OpenFill - Metadata: wersja 0.2, data 2026-10-05 20:58
# OpenFill - Metadata: version 0.1, date 2026-10-05 20:55
# Publishes the package to GitHub: exports a clean copy, scans it for private data,
# commits it and pushes it. Safe to run again: it updates the repository.
#
# Usage:   tools\publish__*.ps1 [-Repo OpenFill] [-Private] [-Message "text"]
# Needs:   git and the GitHub CLI (gh), logged in (gh auth login).
param(
  [string]$Repo = 'OpenFill',
  [switch]$Private,
  [string]$Message = ''
)
$ErrorActionPreference = 'Stop'
$gh = 'C:\Program Files\GitHub CLI\gh.exe'
if (-not (Test-Path $gh)) { $gh = (Get-Command gh -ErrorAction Stop).Source }

$src  = Split-Path -Parent $PSScriptRoot
$dest = Join-Path $env:USERPROFILE 'OpenFill\publish'

# 1. who is logged in
$login = (& $gh api user --jq .login).Trim()
$uid   = (& $gh api user --jq .id).Trim()
if (-not $login) { throw 'Not logged in to GitHub (run: gh auth login).' }
Write-Host "GitHub account: $login"

# 2. export a clean copy (no build output, no stale screenshot)
New-Item -ItemType Directory -Force -Path $dest | Out-Null
robocopy $src $dest /MIR /XD bin obj .vs dist .git /XF 'panel-preview*.png' /NFL /NDL /NJH /NJS /NP | Out-Null
$readme = Get-ChildItem $src -Filter 'README__*.md' | Sort-Object Name | Select-Object -Last 1
Copy-Item $readme.FullName (Join-Path $dest 'README.md') -Force
$ver = (Get-ChildItem $src -Filter 'INDEX__v*.md' | Sort-Object Name | Select-Object -Last 1).Name
Write-Host "Exported $ver to $dest"

# 3. privacy scan of the exported copy
$patterns = @('sk-[A-Za-z0-9]{20,}', 'gh[pousr]_[A-Za-z0-9]{20,}', 'tskey-', '\.ts\.net', '@gmail\.', '@outlook\.', '@hotmail\.', 'C:\\Users\\', 'pboru', 'Boruta', 'Lupus', 'OPENAI_API_KEY\s*=\s*\S')
$hits = Get-ChildItem $dest -Recurse -File -Force | Where-Object {
  $_.FullName -notmatch '\\\.git\\' -and $_.Extension -in '.cs','.ps1','.md','.html','.json','.csproj','.props','.cmd','.sln','.txt'
} | Select-String -Pattern $patterns | Where-Object { $_.Path -notmatch '\\tests\\OpenFill\.Tests\\Program__|\\tools\\publish__' }
if ($hits) {
  $hits | ForEach-Object { Write-Host ("PRIVATE DATA? {0}:{1}: {2}" -f $_.Path.Replace($dest, ''), $_.LineNumber, $_.Line.Trim()) -ForegroundColor Red }
  throw 'Privacy scan found something. Nothing was published.'
}
Write-Host 'Privacy scan: clean.'

# 4. commit
Set-Location $dest
$ErrorActionPreference = 'Continue'   # git writes warnings to stderr; check exit codes instead
if (-not (Test-Path '.git')) { git init -b main | Out-Null }
git config core.autocrlf false
git config user.name  $login
git config user.email "$uid+$login@users.noreply.github.com"
git add -A
if (-not $Message) { $Message = "OpenFill $ver" }
git diff --cached --quiet
if ($LASTEXITCODE -eq 0) { Write-Host 'No changes to publish.'; return }
git commit -q -m $Message
Write-Host 'Committed.'

# 5. create the repository if needed, then push
$exists = $true
& $gh repo view "$login/$Repo" *> $null
if ($LASTEXITCODE -ne 0) { $exists = $false }
if (-not $exists) {
  $vis = if ($Private) { '--private' } else { '--public' }
  & $gh repo create "$login/$Repo" $vis --description 'Windows app where a small AI model operates a real browser for you, driven from ChatGPT over MCP.' --source . --remote origin --push
  foreach ($t in 'mcp','browser-automation','ai-agent','webview2','csharp','dotnet','openai','chatgpt') { & $gh repo edit "$login/$Repo" --add-topic $t | Out-Null }
} else {
  if (-not (git remote)) { git remote add origin "https://github.com/$login/$Repo.git" }
  git push -u origin main
}
Write-Host "Done: https://github.com/$login/$Repo"
