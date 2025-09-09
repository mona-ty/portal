Param(
    # 設定は config.toml を優先。以下は互換用の名残（現状未使用）。
    [string]$Model = $env:CODEX_MODEL,
    [string]$Approval = $env:CODEX_APPROVAL,
    [ValidateSet('low','medium','high')]
    [string]$Effort = $env:CODEX_EFFORT,
    [string]$Cwd = $env:CODEX_CWD
)

if (-not $Cwd -or $Cwd -eq '') { $Cwd = 'C:\\Codex' }

# Convert to WSL path: C:\Codex -> /mnt/c/Codex
$norm = $Cwd
if ($norm -match '^[A-Za-z]:') {
  $drive = $norm.Substring(0,1).ToLower()
  $rest = $norm.Substring(2) # skip 'C:'
  $restUnix = ($rest -replace '\\','/')
  $wslCwd = "/mnt/$drive$restUnix"
} else {
  $wslCwd = ($norm -replace '\\','/')
}

$bashCmd = "export CODEX_CWD='$wslCwd'; cd '$wslCwd'; bash ./scripts/wsl/codex-dev.sh"

Write-Host "[Codex] Launching in WSL (config.toml-driven)..." -ForegroundColor Cyan
Write-Host "  CWD=$Cwd  (モデル/承認/努力度は config.toml を使用)" -ForegroundColor DarkCyan

wsl.exe -e bash -lc $bashCmd

if ($LASTEXITCODE -ne 0) {
  Write-Warning "WSL launch or codex-dev.sh exited with code $LASTEXITCODE"
}
