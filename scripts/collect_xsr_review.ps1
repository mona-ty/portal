param(
  [int]$TailLines = 200,
  [string]$OutDir = ''
)

$ErrorActionPreference = 'Stop'

function Resolve-RepoRoot {
  param([string]$Start)
  $p = Resolve-Path $Start
  while ($p -ne [IO.Path]::GetPathRoot($p)) {
    if (Test-Path (Join-Path $p '.git')) { return $p }
    $p = Split-Path -Parent $p
  }
  throw 'Git repository root not found.'
}

try {
  $scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
  $repoRoot = Resolve-RepoRoot -Start $scriptRoot
  $appPath  = Join-Path $repoRoot 'apps/XIVSubmarinesReturn'
  if (-not (Test-Path $appPath)) { throw "App path not found: $appPath" }

  $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
  $bundleDir = if ($OutDir -and $OutDir.Trim()) { $OutDir } else { Join-Path $appPath "log/review_bundle_$timestamp" }
  New-Item -ItemType Directory -Force -Path $bundleDir | Out-Null

  # 1) Git情報
  & git -C $repoRoot rev-parse HEAD    | Out-File -Encoding utf8 (Join-Path $bundleDir 'git_head.txt')
  & git -C $repoRoot status -uno       | Out-File -Encoding utf8 (Join-Path $bundleDir 'git_status.txt')
  & git -C $repoRoot diff --no-color   | Out-File -Encoding utf8 (Join-Path $bundleDir 'git_diff.txt')

  # 2) dotnet情報
  & dotnet --info                      | Out-File -Encoding utf8 (Join-Path $bundleDir 'dotnet_info.txt')
  "dotnet build apps\\XIVSubmarinesReturn\\XIVSubmarinesReturn.csproj -c Release -p:Platform=x64" |
    Out-File -Encoding utf8 (Join-Path $bundleDir 'build_cmd.txt')

  # 3) プロジェクトの主要ファイル
  $projDir = Join-Path $bundleDir 'project'
  New-Item -ItemType Directory -Force -Path $projDir | Out-Null
  Copy-Item (Join-Path $appPath 'XIVSubmarinesReturn.csproj') $projDir -Force
  Copy-Item (Join-Path $appPath 'manifest.json') $projDir -Force
  Copy-Item (Join-Path $appPath 'plugin.json')   $projDir -Force
  if (Test-Path (Join-Path $appPath 'Local.props.example')) { Copy-Item (Join-Path $appPath 'Local.props.example') $projDir -Force }
  if (Test-Path (Join-Path $appPath 'README.md')) { Copy-Item (Join-Path $appPath 'README.md') $projDir -Force }

  # 4) ログ（apps配下）
  $logSrc = Join-Path $appPath 'log'
  if (Test-Path $logSrc) {
    $logDst = Join-Path $bundleDir 'logs'
    New-Item -ItemType Directory -Force -Path $logDst | Out-Null
    Get-ChildItem -File $logSrc | ForEach-Object {
      $name = $_.Name
      $src  = $_.FullName
      $dst  = Join-Path $logDst $name
      try {
        # TailLines 分だけ抜粋（テキスト想定）
        Get-Content -Tail $TailLines -LiteralPath $src | Out-File -Encoding utf8 $dst
      } catch { Copy-Item $src $dst -Force }
    }
  }

  # 5) 出力JSON（AppData）
  $bridgeDst = Join-Path $bundleDir 'bridge'
  New-Item -ItemType Directory -Force -Path $bridgeDst | Out-Null
  $appData = $env:AppData
  $paths = @(
    Join-Path $appData 'XIVSubmarinesReturn/bridge/submarines.json'),
    (Join-Path $appData 'ff14_submarines_act/bridge/submarines.json')
  $copied = $false
  foreach ($p in $paths) {
    if (Test-Path $p) {
      Copy-Item $p (Join-Path $bridgeDst ([IO.Path]::GetFileName($p))) -Force
      $copied = $true
    }
  }
  if (-not $copied) {
    "not found" | Out-File -Encoding utf8 (Join-Path $bridgeDst 'submarines.json.NOT_FOUND.txt')
  }

  # 6) 圧縮
  $zipPath = Join-Path (Join-Path $appPath 'log') ("xsr_review_bundle_$timestamp.zip")
  if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
  Compress-Archive -Path $bundleDir -DestinationPath $zipPath -Force

  Write-Host "Review bundle created: $zipPath"
  Write-Host "Include this zip and fill docs/review/xsr_review_template.md"
}
catch {
  Write-Error $_
  exit 1
}

