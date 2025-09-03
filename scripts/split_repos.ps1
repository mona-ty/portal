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
  @{ name = 'ff14-submarines'; desc = 'FF14 submarine OCR + Google Calendar tool'; path = 'apps/ff14-submarines' },
  @{ name = 'pomodoro-cli'; desc = 'Simple Pomodoro timer CLI in Python'; path = 'apps/pomodoro-cli' },
  @{ name = 'liftlog-ios'; desc = 'iOS SwiftUI strength training log (scaffold)'; path = 'apps/liftlog-ios' },
  @{ name = 'mahjong-scorer-ios'; desc = 'iOS SwiftUI Mahjong scoring app (scaffold)'; path = 'apps/mahjong-scorer-ios' }
)

 # Remember starting ref to restore after temporary checkouts
$initialRef = (git rev-parse --abbrev-ref HEAD)
if (-not $initialRef -or $initialRef -eq 'HEAD') { $initialRef = (git rev-parse HEAD) }

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

  # 2) Subtree split branch (supports paths removed from HEAD)
  $usedWorktree = $false
  $tmpDir = ''
  if (-not (Test-Path $r.path)) {
    # Find a commit where the path existed
    $sourceCommit = ''
    foreach ($c in (git rev-list --all -- $($r.path))) {
      $present = git ls-tree -d --name-only $c -- $($r.path)
      if ($present) { $sourceCommit = $c; break }
    }
    if (-not $sourceCommit) {
      Write-Host "Skip (no history for path): $($r.path)" -ForegroundColor Yellow
      continue
    }
    # Use a temporary worktree at that commit to run subtree split without touching current checkout
    $tmpDir = Join-Path ([System.IO.Path]::GetTempPath()) ("split-src-" + [System.IO.Path]::GetRandomFileName())
    git worktree add --detach "$tmpDir" $sourceCommit | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to create temp worktree for $($r.name)" }
    $usedWorktree = $true
  }
  $branch = "split/$($r.name)"
  git show-ref --verify --quiet "refs/heads/$branch"; if ($LASTEXITCODE -eq 0) { git branch -D $branch | Out-Null }
  if ($usedWorktree) {
    git -C "$tmpDir" subtree split --prefix=$($r.path) -b $branch | Out-Null
  } else {
    git subtree split --prefix=$($r.path) -b $branch | Out-Null
  }
  if ($LASTEXITCODE -ne 0) {
    if ($usedWorktree -and $tmpDir) { git worktree remove --force "$tmpDir" | Out-Null }
    throw "git subtree split failed for $($r.path)"
  }
  Write-Host "Split ready: $branch from $($r.path)" -ForegroundColor Cyan

  # 3) Push to new repo as main
  $url = "https://github.com/$full.git"
  if ($Force) {
    git push $url $branch`:main --force
  } else {
    git push $url $branch`:main
  }
  if ($LASTEXITCODE -ne 0) {
    if ($usedWorktree -and $tmpDir) { git worktree remove --force "$tmpDir" | Out-Null }
    throw "git push failed for $full (try -Force if non-fast-forward)"
  }
  Write-Host "Pushed -> https://github.com/$full" -ForegroundColor Green

  # Clean up temp worktree if used
  if ($usedWorktree -and $tmpDir) {
    git worktree remove --force "$tmpDir" | Out-Null
  }
}

Write-Host "Done. Verify each repo on GitHub." -ForegroundColor Cyan
