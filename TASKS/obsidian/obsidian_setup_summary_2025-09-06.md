# Obsidian セットアップ実施サマリ（notes運用強化）

- バックアップ
  - 元: `C:\Codex\notes`
  - 先: `C:\notes_backup`
  - 方法: robocopy `/E /COPY:DAT /DCOPY:DAT`（タイムスタンプ保持）
  - 結果: 成功（BACKUP_OK）

- テンプレート（`notes/70_templates/`）
  - `research-note.md`（研究ノート: Summary/Key Points/Quotes/Source/Related）
  - `distilled-note.md`（蒸留ノート: TL;DR/Details/Why/Related）
  - `prompt.md`（生成AI: Task/Model/System/Few-Shots/Input/Output/Eval）
  - `code-snippet.md`（コード: Context/Snippet/Steps/Gotchas/Related）
  - `project-readme.md`（案件: Overview/Goals/Scope/Timeline/Tasks/Links）
  - すべてTemplater対応（`<% %>`）。QuickAddから呼び出し推奨。

- Dataview ダッシュボード（`notes/Dashboard.md`）
  - Untagged（YAML tags 無し/空）: dataviewjsで厳密判定
  - Orphans（inlinks = 0）, Unlinked（outlinks = 0）
  - Drafts（status = draft）
  - Recently Updated（14日）
  - 除外: 70_templates / 60_attachments / 90_archive

- 次の推奨ステップ（ご要望で実施可）
  - QuickAddコマンド作成（保存先/命名/テンプレ適用を自動化）
  - 添付先: Obsidian設定で `60_attachments/` を既定化
  - 週次レビュー運用: Inboxゼロ・未タグ/未リンクの解消
  - タグ基準の微調整（ブラック/ホワイトリスト拡張）

