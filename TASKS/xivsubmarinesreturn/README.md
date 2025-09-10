# XSR: xivsubmarinesreturn 開発ノート

- 目的: `apps/XIVSubmarinesReturn`（XSR）の開発用ブランチ `xivsubmarinesreturn` の作業メモ置き場。
- スコープ: XSR プラグイン本体・ドキュメント・CI 設定の変更に関わる事項。
- 運用: 軽量にメモ・タスクを積み、PR 時に整理して移管（必要なら `apps/XIVSubmarinesReturn/README.md` 等へ反映）。

## ブランチポリシー（簡略）
- ベース: `main` から分岐。
- コミット: Conventional Commits（例: `feat(xsr): ...`, `fix(xsr): ...`, `chore(xsr): ...`）。
- 単位: 小さく、テスト可能な塊でコミット。
- PR: 原則セルフレビュー → CI パス → マージ。

## 開発メモ
- 実行/ビルドは `apps/XIVSubmarinesReturn/README.md` を参照。
- 変更履歴は `apps/XIVSubmarinesReturn/CHANGELOG.md` の `[Unreleased]` に追記。

## 関連ファイル
- `TASKS/xivsubmarinesreturn/plan.md` — 直近のタスク計画
- `apps/XIVSubmarinesReturn/CHANGELOG.md` — 変更履歴（Unreleased）

