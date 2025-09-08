param(
  [string]$ProjectRoot = '',
  [string]$AppName = '',
  [int]$TailLines = 200,
  [string[]]$LogPaths = @(),          # ファイル/ディレクトリ（相対/絶対）
  [string[]]$IncludePaths = @(),      # 追加で含めたいファイル/ディレクトリ
  [string[]]$Commands = @(),          # 実行または記録したいコマンド文字列
  [switch]$ExecuteCommands,           # 指定時は実行して出力を保存
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

function Try-Cmd {
  param([string]$Cmd, [string]$OutFile)
  try { & cmd /c $Cmd 2>&1 | Out-File -Encoding utf8 $OutFile } catch { "ERROR: $_" | Out-File -Encoding utf8 $OutFile }
}

try {
  $here = Split-Path -Parent $MyInvocation.MyCommand.Path
  $repoRoot = Resolve-RepoRoot -Start $here

  $root = if ($ProjectRoot -and $ProjectRoot.Trim()) { Resolve-Path $ProjectRoot } else { $repoRoot }
  $name = if ($AppName -and $AppName.Trim()) { $AppName } else { Split-Path -Leaf $root }
  $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
  $bundleDir = if ($OutDir -and $OutDir.Trim()) { $OutDir } else { Join-Path $root "review_bundle_${name}_$timestamp" }
  New-Item -ItemType Directory -Force -Path $bundleDir | Out-Null

  # 1) Git情報
  & git -C $repoRoot rev-parse HEAD    | Out-File -Encoding utf8 (Join-Path $bundleDir 'git_head.txt')
  & git -C $repoRoot status -uno       | Out-File -Encoding utf8 (Join-Path $bundleDir 'git_status.txt')
  & git -C $repoRoot diff --no-color   | Out-File -Encoding utf8 (Join-Path $bundleDir 'git_diff.txt')

  # 2) 環境情報（可能な範囲）
  $envDir = Join-Path $bundleDir 'env'
  New-Item -ItemType Directory -Force -Path $envDir | Out-Null
  Try-Cmd 'dotnet --info'   (Join-Path $envDir 'dotnet_info.txt')
  Try-Cmd 'node -v && npm -v' (Join-Path $envDir 'node_npm.txt')
  Try-Cmd 'python --version && pip --version' (Join-Path $envDir 'python_pip.txt')
  Try-Cmd 'go version'      (Join-Path $envDir 'go.txt')
  Try-Cmd 'java -version'   (Join-Path $envDir 'java.txt')
  Try-Cmd 'rustc --version && cargo --version' (Join-Path $envDir 'rust.txt')

  # 3) プロジェクトメタ（代表的な定義ファイルを収集）
  $metaDir = Join-Path $bundleDir 'project_meta'
  New-Item -ItemType Directory -Force -Path $metaDir | Out-Null
  $metaGlobs = @('*.sln','*.csproj','package.json','pnpm-lock.yaml','yarn.lock','pyproject.toml','requirements.txt','poetry.lock','go.mod','Cargo.toml','Gemfile','build.gradle','pom.xml')
  foreach ($g in $metaGlobs) {
    Get-ChildItem -Path $root -Recurse -File -Filter $g -ErrorAction SilentlyContinue | ForEach-Object {
      $dst = Join-Path $metaDir ([IO.Path]::GetFileName($_.FullName))
      Copy-Item $_.FullName $dst -Force
    }
  }

  # 4) ログ収集
  if ($LogPaths.Count -gt 0) {
    $logDst = Join-Path $bundleDir 'logs'
    New-Item -ItemType Directory -Force -Path $logDst | Out-Null
    foreach ($lp in $LogPaths) {
      $resolved = $lp
      if (-not (Test-Path $resolved)) {
        $resolved = Join-Path $root $lp
      }
      if (Test-Path $resolved) {
        if ((Get-Item $resolved).PSIsContainer) {
          Get-ChildItem -File -Recurse $resolved | ForEach-Object {
            $rel = [IO.Path]::GetFileName($_.FullName)
            try { Get-Content -Tail $TailLines -LiteralPath $_.FullName | Out-File -Encoding utf8 (Join-Path $logDst $rel) }
            catch { Copy-Item $_.FullName (Join-Path $logDst $rel) -Force }
          }
        } else {
          $rel = [IO.Path]::GetFileName($resolved)
          try { Get-Content -Tail $TailLines -LiteralPath $resolved | Out-File -Encoding utf8 (Join-Path $logDst $rel) }
          catch { Copy-Item $resolved (Join-Path $logDst $rel) -Force }
        }
      }
    }
  }

  # 5) 任意の追加ファイル/ディレクトリ
  if ($IncludePaths.Count -gt 0) {
    $incDst = Join-Path $bundleDir 'include'
    New-Item -ItemType Directory -Force -Path $incDst | Out-Null
    foreach ($ip in $IncludePaths) {
      $resolved = $ip
      if (-not (Test-Path $resolved)) { $resolved = Join-Path $root $ip }
      if (Test-Path $resolved) { Copy-Item $resolved $incDst -Recurse -Force }
    }
  }

  # 6) コマンドの記録/実行
  if ($Commands.Count -gt 0) {
    $cmdDir = Join-Path $bundleDir 'commands'
    New-Item -ItemType Directory -Force -Path $cmdDir | Out-Null
    $i = 0
    foreach ($c in $Commands) {
      $i++
      $cmdFile = Join-Path $cmdDir ("cmd_${i}.txt")
      $outFile = Join-Path $cmdDir ("cmd_${i}_output.txt")
      $c | Out-File -Encoding utf8 $cmdFile
      if ($ExecuteCommands) { Try-Cmd $c $outFile }
    }
  }

  # 7) 圧縮
  $zipPath = Join-Path $root ("review_bundle_${name}_$timestamp.zip")
  if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
  Compress-Archive -Path $bundleDir -DestinationPath $zipPath -Force

  Write-Host "Review bundle created: $zipPath"
  Write-Host "Fill docs/review/ai_code_review_template.md and attach the zip."
}
catch {
  Write-Error $_
  exit 1
}

