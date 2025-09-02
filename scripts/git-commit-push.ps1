param(
  [Parameter(Mandatory=$false)] [string]$Message,
  [switch]$All,
  [switch]$Sign
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

& "$PSScriptRoot/git-commit.ps1" -Message $Message @PSBoundParameters
& "$PSScriptRoot/git-push.ps1"
