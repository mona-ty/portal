@echo off
REM codexとclaudeの結果を比較するバッチファイル
REM 使用方法: compare_codex_claude.bat "プロンプト"

if "%~1"=="" (
    echo 使用方法: compare_codex_claude.bat "プロンプト"
    exit /b 1
)

set PROMPT=%~1

echo ========================================
echo Codex実行中...
codex exec --full-auto "%PROMPT%" > codex_output.txt

echo ========================================
echo Claude実行中...
python claude_exec.py "%PROMPT%" > claude_output.txt

echo ========================================
echo 差分確認:
fc codex_output.txt claude_output.txt

echo ========================================
echo 結果ファイル:
echo - codex_output.txt
echo - claude_output.txt
