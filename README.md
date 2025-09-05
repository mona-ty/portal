# Apps Lab Monorepo

個人開発向けの小規模アプリをまとめたモノレポ（実験場）です。各アプリは `apps/` 配下に配置し、必要に応じて独立リポへ昇格できる構成にしています。

## 構成
- `apps/ff14-submarines/`: FF14 サブマリンの帰還時刻を OCR し Google カレンダーに登録するツール（Python）
- `apps/pomodoro-cli/`: シンプルなポモドーロタイマー CLI（Python）
- `apps/liftlog-ios/`: 筋トレ記録アプリの iOS スケルトン（SwiftUI）
- `apps/mahjong-scorer-ios/`: 麻雀点数計算アプリの iOS スケルトン（SwiftUI）
- `docs/`: ドキュメント（ハンドブック/新規アプリ作成手順）
- `scripts/`: 共通スクリプト（新規アプリ生成・リポ分割）
- `templates/`: 生成テンプレート
- `tests/`: 共有テスト（将来は各アプリ配下へ移行）
- `archive/`: 退避用
- `tmp/`: 一時ファイル・生成物（Git管理外）

## ドキュメント
- 開発ハンドブック: `docs/handbook.md`
- 新規アプリ作成ワークフロー: `docs/new_app.md`

## 運用の要点
- 日々の試行錯誤やノートはリポ外ワークスペースへ（例: `C:\\codex-work\\<repo>`）
- アプリが成熟したら `scripts/split_repos.ps1` で独立リポへ昇格
- CI: `.github/workflows/python-tests.yml`（Windows/Ubuntu で unittest 実行）

