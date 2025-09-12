# Automate publishing apps/XIVSubmarinesReturn to remote 'xsr' and tag a release.
# Usage: scripts\win\Publish-XSR.ps1 -Version v1.2.3 [-BaseBranch main]

[CmdletBinding()]
param(
  [Parameter(Mandatory=$true)] [string]$Version,
  [string]$BaseBranch = 'main'
)

function Exec($cmd) {
  Write-Host "PS> $cmd" -ForegroundColor Cyan
  & powershell -NoProfile -Command $cmd
  if ($LASTEXITCODE -ne 0) { throw "Command failed: $cmd" }
}

if ($Version -notmatch '^v\d+\.\d+\.\d+$') {
  throw "Version must be like v1.2.3"
}

$repoRoot = (git rev-parse --show-toplevel) 2>$null
if (-not $repoRoot) { throw "Not inside a git repository" }
Set-Location $repoRoot

$appPrefix = 'apps/XIVSubmarinesReturn'
if (-not (Test-Path $appPrefix)) { throw "Path not found: $appPrefix (run from monorepo root)" }

$remoteName = 'xsr'
if (-not (git remote get-url $remoteName 2>$null)) { throw "Remote not configured: $remoteName" }

if (-not (git rev-parse --verify --quiet $BaseBranch 2>$null)) { throw "Base branch not found: $BaseBranch" }

if (-not (git diff --quiet) -or -not (git diff --cached --quiet)) { throw "Working tree has changes. Commit or stash first." }

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$exportBranch = "xsr/export-$stamp"
$backupBranch = "backup/initial-$stamp"

Exec "git checkout $BaseBranch"

# Bump csproj version in monorepo before split
$csproj = Join-Path $appPrefix 'XIVSubmarinesReturn.csproj'
if (Test-Path $csproj) {
  $vNoV = $Version.TrimStart('v')
  $asmV = "$vNoV.0"
  $xml = Get-Content $csproj -Raw
  $xml = $xml -replace '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$asmV</AssemblyVersion>"
  $xml = $xml -replace '<FileVersion>[^<]+</FileVersion>', "<FileVersion>$asmV</FileVersion>"
  $xml = $xml -replace '<Version>[^<]+</Version>', "<Version>$vNoV</Version>"
  Set-Content -Path $csproj -Value $xml -Encoding UTF8
  if (-not (git diff --quiet -- $csproj)) {
    Exec "git add $csproj"
    Exec "git commit -m 'chore(xsr): bump csproj version to $Version'"
  } else {
    Write-Host "csproj already at $vNoV; no commit"
  }
} else {
  Write-Warning "csproj not found at $csproj; skipping bump"
}
Exec "git subtree split --prefix=$appPrefix $BaseBranch -b $exportBranch"

Exec "git fetch $remoteName --quiet"
try { Exec "git branch -f xsr-main-backup $remoteName/main" } catch {}
Exec "git push $remoteName xsr-main-backup:$backupBranch"

Exec "git push -f $remoteName $exportBranch:main"

if (git rev-parse -q --verify "refs/tags/$Version" 2>$null) {
  Write-Host "Tag exists locally; moving to current HEAD" -ForegroundColor Yellow
  Exec "git tag -f $Version"
} else {
  Exec "git tag -a $Version -m 'Release $Version'"
}
Exec "git push -f $remoteName $Version"

$repoPath = (git remote get-url $remoteName) -replace '.*github.com[:/ ]','' -replace '\.git$',''
Write-Host "Done. Check:" -ForegroundColor Green
Write-Host " - https://github.com/$repoPath/actions"
Write-Host " - https://github.com/$repoPath/releases/tag/$Version"
