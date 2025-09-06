# Obsidian セットアップ実施サマリ（MK Vault基準）

- バックアップ（MKのみ）
  - 元: `C:\Codex\notes\MK`
  - 先: `C:\notes_backup\MK`
  - 方法: robocopy `/E /COPY:DAT /DCOPY:DAT`
  - 結果: 成功（BACKUP_MK_OK）

- テンプレート（`notes/MK/70_templates/`）
  - `research-note.md`, `distilled-note.md`, `prompt.md`, `code-snippet.md`, `project-readme.md`
  - Templater対応（`<% %>`）。QuickAddから呼び出し推奨。

- Dataview ダッシュボード（`notes/MK/Dashboard.md`）
  - 対象: MK配下のみ（テンプレ/添付/アーカイブは除外）
  - Untagged / Orphans / Unlinked / Drafts / 最近14日更新

- 今後の推奨
  - ObsidianのVaultルートを `C:\Codex\notes\MK` に設定
  - 添付先の既定を `60_attachments/`（必要ならフォルダ作成）
  - QuickAddコマンドの整備（テンプレと保存先の自動化）

