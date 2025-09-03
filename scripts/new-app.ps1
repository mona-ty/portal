param(
  [Parameter(Mandatory = $true)][ValidateSet('python-cli','ios-swiftui')][string]$Kind,
  [Parameter(Mandatory = $true)][string]$Name,
  [string]$Description = "",
  [string]$Package = "",
  [switch]$WithTests,
  [switch]$Commit,
  [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

function Normalize-Slug([string]$s) {
  $x = $s.ToLowerInvariant()
  $x = ($x -replace '[^a-z0-9\- ]','')
  $x = ($x -replace ' +','-')
  return $x
}

function SlugToPkg([string]$slug) {
  return ($slug -replace '-', '_')
}

function Replace-InFile([string]$path, [hashtable]$map) {
  $text = Get-Content -LiteralPath $path -Raw -ErrorAction Stop
  foreach ($k in $map.Keys) { $text = $text -replace [regex]::Escape($k), [string]$map[$k] }
  Set-Content -LiteralPath $path -Value $text -Encoding UTF8
}

function Ensure-AtRepoRoot() {
  try { git rev-parse --is-inside-work-tree | Out-Null } catch { throw "Not a git repository. Run from repo root." }
}

Ensure-AtRepoRoot

$slug = Normalize-Slug $Name
if (-not $Package -and $Kind -eq 'python-cli') { $Package = SlugToPkg $slug }
if (-not $Description) { $Description = $Name }

$dest = Join-Path 'apps' $slug
if (Test-Path $dest) { throw "Destination already exists: $dest" }
if ($DryRun) {
  Write-Host "[DRY-RUN] Would create: $dest ($Kind)" -ForegroundColor Yellow
  exit 0
}

New-Item -ItemType Directory -Force $dest | Out-Null

switch ($Kind) {
  'python-cli' {
    $tpl = Join-Path 'templates' 'python-cli'
    if (-not (Test-Path $tpl)) { throw "Template not found: $tpl" }
    Copy-Item "$tpl/*" -Destination $dest -Recurse -Force
    # Rename package folder
    Rename-Item -LiteralPath (Join-Path $dest 'package') -NewName $Package
    # Replace placeholders in files
    $map = @{
      '__APP_NAME__' = $Name
      '__DESCRIPTION__' = $Description
      '__PKG_NAME__' = $Package
      '__APP_SLUG__' = $slug
    }
    Get-ChildItem -LiteralPath $dest -Recurse -File | Where-Object { $_.Extension -in '.py','.md','.toml' } | ForEach-Object { Replace-InFile $_.FullName $map }
    if (-not $WithTests) {
      Remove-Item -Recurse -Force (Join-Path $dest 'tests') -ErrorAction SilentlyContinue
    }
  }
  'ios-swiftui' {
    $tpl = Join-Path 'templates' 'ios-swiftui'
    if (-not (Test-Path $tpl)) { throw "Template not found: $tpl" }
    Copy-Item "$tpl/*" -Destination $dest -Recurse -Force
    $map = @{
      '__APP_NAME__' = $Name
      '__DESCRIPTION__' = $Description
      '__APP_SLUG__' = $slug
    }
    Get-ChildItem -LiteralPath $dest -Recurse -File | Where-Object { $_.Extension -in '.md','.swift' } | ForEach-Object { Replace-InFile $_.FullName $map }
  }
}

Write-Host "Created: $dest" -ForegroundColor Green

if ($Commit) {
  git add $dest
  git commit -m "feat(${slug}): scaffold ${Kind} app"
}

Write-Host "Next steps:" -ForegroundColor Cyan
switch ($Kind) {
  'python-cli' {
    Write-Host "  - (optional) Create venv: python -m venv .venv; .venv\\Scripts\\activate" -ForegroundColor DarkGray
    Write-Host "  - Run: python -m ${Package} --help" -ForegroundColor DarkGray
    if ($WithTests) { Write-Host "  - Test: python -m unittest apps/${slug}/tests -v" -ForegroundColor DarkGray }
  }
  'ios-swiftui' {
    Write-Host "  - Xcode > New Project > Add apps/${slug}/iOSApp as folder refs" -ForegroundColor DarkGray
  }
}

