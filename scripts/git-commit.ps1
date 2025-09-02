param(
  [Parameter(Mandatory=$false)] [string]$Message,
  [switch]$All,
  [switch]$Sign
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-DefaultMessage {
  $branch = (git rev-parse --abbrev-ref HEAD).Trim()
  return "chore($branch): update"
}

if (-not (Test-Path -Path ".git")) {
  throw "This does not appear to be a Git repository."
}

if ($All) { git add -A | Out-Null }

if (-not $Message -or [string]::IsNullOrWhiteSpace($Message)) {
  Write-Host "No commit message provided. Using a default." -ForegroundColor Yellow
  $Message = Get-DefaultMessage
}

$argsList = @('commit','-m', $Message)
if ($Sign) { $argsList += '--signoff' }

git @argsList
