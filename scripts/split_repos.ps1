param(
  [string]$Owner = "",
  [ValidateSet('public','private')][string]$Visibility = 'public',
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

# Ensure we are at repo root
git rev-parse --is-inside-work-tree | Out-Null

if (-not $Owner) {
  $Owner = gh api user -q .login
}

Write-Host "Using owner: $Owner" -ForegroundColor Cyan

$repos = @(
  @{ name = 'ff14-submarines'; desc = 'FF14 submarine OCR + Calendar tool'; path = 'apps/ff14-submarines' },
  @{ name = 'todo-app'; desc = 'Simple ToDo web app'; path = 'apps/todo-app' },
  @{ name = 'buronto-generator'; desc = 'Japanese style-aware text generator + web'; path = 'tools/bronto-generator' }
)

foreach ($r in $repos) {
  # 1) Create repo if missing
  $full = "$($Owner)/$($r.name)"
  $exists = $true
  try { gh repo view $full 1>$null 2>$null | Out-Null } catch { $exists = $false }
  if (-not $exists) {
    gh repo create $full --$Visibility --description $r.desc -y | Out-Null
    Write-Host "Created: $full" -ForegroundColor Green
  } else {
    Write-Host "Exists: $full" -ForegroundColor Yellow
  }

  # 2) Subtree split branch
  if (-not (Test-Path $r.path)) {
    Write-Host "Skip (path not found): $($r.path)" -ForegroundColor Yellow
    continue
  }
  $branch = "split/$($r.name)"
  git show-ref --verify --quiet "refs/heads/$branch"; if ($LASTEXITCODE -eq 0) { git branch -D $branch | Out-Null }
  git subtree split --prefix=$($r.path) -b $branch | Out-Null
  Write-Host "Split ready: $branch from $($r.path)" -ForegroundColor Cyan

  # 3) Push to new repo as main
  $url = "https://github.com/$full.git"
  if ($Force) {
    git push $url "$branch:main" --force
  } else {
    git push $url "$branch:main"
  }
  Write-Host "Pushed -> https://github.com/$full" -ForegroundColor Green
}

Write-Host "Done. Verify each repo on GitHub." -ForegroundColor Cyan
