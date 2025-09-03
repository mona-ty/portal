# Apps Lab Monorepo

このリポジトリは、個人開発向けの小規模アプリをまとめたモノレポ（実験場）です。各アプリは `apps/` 配下に配置し、必要に応じて独立リポに昇格できる構成にしています。

## 構成
- `apps/ff14-submarines/`: FF14 サブマリンの帰還時刻を OCR し Google カレンダーに登録するツール（Python）
- `apps/pomodoro-cli/`: シンプルなポモドーロタイマー CLI（Python）
- `apps/liftlog-ios/`: 筋トレ記録アプリの iOS スケルトン（SwiftUI）
- `apps/mahjong-scorer-ios/`: 麻雀点数計算アプリの iOS スケルトン（SwiftUI）
- `docs/`: リポ全体のドキュメント
- `scripts/`: 共通スクリプト（リポ分割など）
- `tests/`: 共有テスト（将来は各アプリ配下へ移行）
- `tools/`: 共通ユーティリティ

## 運用方針（サマリ）
- 日々の試行錯誤やノートはリポ外のワークスペースへ（例: `C:\codex-work\<repo>`）
- 価値が固まったアプリは `scripts/split_repos.ps1` で独立リポへ昇格
- README/最低限のテスト/起動手順が整ったら昇格の目安

## 既知のメモ
- 一部の日本語 README は文字化け（エンコーディング）修正予定です（UTF-8 化）。
