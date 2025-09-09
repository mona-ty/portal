# 全プロジェクトの仮想環境を自動設定するスクリプト

Write-Host "=== プロジェクト仮想環境自動設定 ===" -ForegroundColor Green

# Pythonプロジェクトを検索
$pythonProjects = @(
    "apps\ff14-submarines",
    "apps\ff14_submarines", 
    "apps\pomodoro-cli"
)

foreach ($project in $pythonProjects) {
    $projectPath = Join-Path $PWD $project
    
    if (Test-Path $projectPath) {
        Write-Host "`n設定中: $project" -ForegroundColor Yellow
        
        # プロジェクトディレクトリに移動
        Push-Location $projectPath
        
        try {
            # 仮想環境が存在しない場合のみ作成
            if (-not (Test-Path ".venv")) {
                Write-Host "  仮想環境を作成中..." -ForegroundColor Cyan
                python -m venv .venv
                Write-Host "  ✓ 仮想環境を作成しました" -ForegroundColor Green
            } else {
                Write-Host "  ✓ 仮想環境は既に存在します" -ForegroundColor Green
            }
            
            # 仮想環境を有効化
            Write-Host "  仮想環境を有効化中..." -ForegroundColor Cyan
            & ".venv\Scripts\Activate.ps1"
            
            # requirements.txtが存在する場合は依存関係をインストール
            if (Test-Path "requirements.txt") {
                Write-Host "  依存関係をインストール中..." -ForegroundColor Cyan
                pip install -r requirements.txt
                Write-Host "  ✓ 依存関係をインストールしました" -ForegroundColor Green
            }
            
            # 仮想環境を無効化
            deactivate
            
        } catch {
            Write-Host "  ✗ エラー: $($_.Exception.Message)" -ForegroundColor Red
        } finally {
            Pop-Location
        }
    } else {
        Write-Host "  ✗ プロジェクトが見つかりません: $project" -ForegroundColor Red
    }
}

Write-Host "`n=== 設定完了 ===" -ForegroundColor Green
Write-Host "PowerShellプロファイルを再読み込みしてください: . `$PROFILE" -ForegroundColor Yellow
