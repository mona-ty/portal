# Obsidian 運用計画（MK Vault・再設計）

- 背景（docx要旨）
  - 目的: Capture → Distill → Link → Reuse の循環で検索性・再利用性向上
  - 方針: ローカル優先（Windows/iOS）、必要時のみAI連携（最小限テキストのみ送信）
  - 運用: テンプレ標準化、QuickAddで新規作成を自動化、Dataviewで未整理を可視化

- Vault前提
  - Vault ルート: `C:\Codex\notes\MK`
  - 主要フォルダ（作成済）: `00_inbox/`, `10_research/`, `20_notes/`, `30_projects/`, `40_prompts/`, `50_code/`, `60_attachments/`, `70_templates/`, `90_archive/`

- QuickAdd コマンド（整備済み）
  - 設定ファイル: `MK/.obsidian/plugins/quickadd/data.json`
  - コマンド一覧（Command Palette から実行可能）
    - New Research Note: `10_research/` に `70_templates/research-note.md`
    - New Distilled Note: `20_notes/` に `70_templates/distilled-note.md`
    - New Prompt: `40_prompts/` に `70_templates/prompt.md`
    - New Code Snippet: `50_code/` に `70_templates/code-snippet.md`
    - New Project README: `30_projects/<入力>/README` に `70_templates/project-readme.md`
  - 生成名: `{{DATE}}` と `{{VALUE}}` を組み合わせ（例: `2025-09-06 キーワード`）
  - 補足: プラグインを有効化/再読込後に反映。テンプレは `MK/70_templates/` を参照

- Dataview ダッシュボード（整備済み）
  - ファイル: `MK/Dashboard.md`
  - 可視化: Untagged / Orphans / Unlinked / Drafts / 最近14d更新（テンプレ/添付/アーカイブ除外）

- テンプレ（整備済み）
  - `research-note.md`, `distilled-note.md`, `prompt.md`, `code-snippet.md`, `project-readme.md`
  - Templater対応（`<% %>`）。QuickAddからの新規作成で適用

- 運用フロー
  - Capture: Web/書籍/メモは QuickAdd「New Research Note」（出典URLを `source:` に記録）
  - Distill: 重要点を簡潔に再構成 → QuickAdd「New Distilled Note」へ要約転記
  - Link: `Related` を追記、WikiLink/タグ整備（最大5件）
  - Reuse: Dashboard で未整理を定期消化、プロンプト/コードの再利用

- AI連携（任意）
  - 要約/タグ付けのみ対象ノート断片を送信（Vault全送信なし）
  - 生成結果はノートに「日時/目的/モデル」を明記

- セットアップ確認手順
  - Obsidian で QuickAdd / Dataview / Templater を有効化
  - Command Palette で「QuickAdd: New Research Note」等が出ることを確認
  - `MK/Dashboard.md` を開き Dataviewの一覧を確認

- 今後の拡張
  - QuickAddホットキー割当、Daily Note連携、各種Capture（今日のタスク追記 等）
  - タグ基準の微調整（ブラック/ホワイトリスト拡張）

