param(
  [string]$Owner = "",
  [ValidateSet('public','private')][string]$Visibility = 'public',
  [string]$RepoName = 'xiv-submarines-return',
  [switch]$Force
)

$ErrorActionPreference = 'Stop'

function Require-Cmd($name) {
  if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
    throw "Required command not found: $name. Please install it and retry."
  }
}

Require-Cmd git
Require-Cmd gh

git rev-parse --is-inside-work-tree | Out-Null

if (-not $Owner) { $Owner = gh api user -q .login }

$path = 'apps/XIVSubmarinesReturn'
$desc = 'Dalamud plugin: capture company workshop submarine list to JSON; Discord/Notion integration'
$full = "$Owner/$RepoName"

Write-Host "Splitting $path -> $full ($Visibility)" -ForegroundColor Cyan

# Create repo if missing
$exists = $true
try { gh repo view $full 1>$null 2>$null | Out-Null } catch { $exists = $false }
if (-not $exists) {
  gh repo create $full --$Visibility --description $desc -y | Out-Null
  Write-Host "Created: https://github.com/$full" -ForegroundColor Green
} else {
  Write-Host "Exists: https://github.com/$full" -ForegroundColor Yellow
}

# Subtree split branch
$branch = "split/$RepoName"
git show-ref --verify --quiet "refs/heads/$branch"; if ($LASTEXITCODE -eq 0) { git branch -D $branch | Out-Null }
git subtree split --prefix=$path -b $branch | Out-Null
if ($LASTEXITCODE -ne 0) { throw "git subtree split failed" }

# Push
$url = "https://github.com/$full.git"
if ($Force) { git push $url $branch`:main --force }
else { git push $url $branch`:main }

Write-Host "Pushed -> https://github.com/$full" -ForegroundColor Green

