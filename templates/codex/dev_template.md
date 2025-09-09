# Codex 開発運用テンプレート

目的
- C:\\Codex モノレポでの日常開発を標準化（Win/WSL 両対応）。

起動手順（標準）
- Windows PowerShell:
  - `scripts\win\Start-WSLAndCodex.ps1 -Model gpt-5-high -Approval full-access`
  - 省略時の既定: Model=gpt-5-high, Approval=full-access, Cwd=C:\\Codex
- モデル変更: `-Model gpt-5.1-high` 等に差し替え（環境変数 CODEX_MODEL でも可）。

起動時フロー（WSL内）
1) 自動走査を実行し、レポートを `TASKS/autoscan/scan_YYYYMMDD-HHMM.md` に出力
2) Codex CLI を指定モデル/承認で起動

自動走査の確認観点
- Git: ブランチ、未コミット数、直近コミット、リモート
- 構造: ネスト .git の有無（ゼロが理想）
- プロジェクト検出: .csproj / package.json / pyproject.toml
- ignore 健全性: bin/obj の追跡有無

日常チェックリスト
- [ ] 作業ブランチは最新か（`git pull --rebase`）
- [ ] 未コミット変更の整理（コミット/スタッシュ）
- [ ] ビルド/テスト実行方法の確認（README, docs）
- [ ] 配布/パッケージ手順の確認（Packager/CI）
- [ ] 変更の影響範囲の明確化（docs/TASKS にメモ）

運用メモ
- モノレポの一貫性維持のため、配下に独立 .git は置かない
- 外部取得物や検証クローンは `external/` または `workspace/` を使用（Git管理外）

