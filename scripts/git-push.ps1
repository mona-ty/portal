Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not (Test-Path -Path ".git")) {
  throw "This does not appear to be a Git repository."
}

$branch = (git rev-parse --abbrev-ref HEAD).Trim()

# Ensure upstream is set; if not, set to origin/<branch>
$hasUpstream = $false
try {
  $null = git rev-parse --abbrev-ref --symbolic-full-name @{u}
  $hasUpstream = $LASTEXITCODE -eq 0
} catch {}

if (-not $hasUpstream) {
  Write-Host "Setting upstream to origin/$branch" -ForegroundColor Yellow
  git push -u origin $branch
} else {
  git push
}
