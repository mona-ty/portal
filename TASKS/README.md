# TASKS ディレクトリ ガイド

## 目的（要点）
- 手作業ドキュメントと自動生成物の置き場を分離し、見通しと再現性を確保します。
- 生成レポートは `TASKS/artifacts/` に統一、運用ドキュメントは用途別に集約します。

## フォルダ構成
- `obsidian/`
  - Obsidian Vault 運用・整理の方針と記録（設計/手順/サマリ類）。
  - 主なファイル: `ai_obsidian_policy_template.yaml`, `obsidian_*` ドキュメント、関連 .docx。
- `codex/`
  - Codex/Coding まわりのメモ・レビュー・計画（人が読む資料）。
- `artifacts/`
  - 自動生成レポート/差分/適用ログ（機械出力）を集約。
  - 例: `mk_link_audit.csv`, `mk_frontmatter_*.csv`, `mk_*_apply_dry.diff`, `dir_inventory.csv` など。

## 運用ルール
- 生成物はすべて `artifacts/` に保存（スクリプト既定出力先も同一）。
- ドキュメントは用途で振分: Obsidian → `obsidian/`, Codex → `codex/`。
- Obsidian Vault (`notes/` 配下) は Git 追跡外（`.gitignore` 済）。
- 変更フローは「バックアップ → ドライラン → レビュー → 適用」。
  - バックアップ例: `C:\notes_backup_YYYYMMDD-HHMMSS`。

## ポリシーとスクリプト
- ポリシー（AIが解釈可能な整理方針）
  - `obsidian/ai_obsidian_policy_template.yaml`
  - 本番運用時は内容確定後に `ai_obsidian_policy.yaml` 等へリネーム推奨。
- スクリプト（Obsidian 整理/監査系）
  - 場所: `scripts/obsidian/`
  - 出力: `TASKS/artifacts/`（CSV/DIFF/PLAN など）
  - 例:
    - `python scripts/obsidian/link_audit_mk.py`（リンク監査）
    - `python scripts/obsidian/frontmatter_audit_mk.py`（FM監査）
    - `python scripts/obsidian/retag_mk_dry_run.py`（タグ提案）

## 代表的なレポート（例）
- `mk_link_audit.csv`（リンク監査）
- `mk_link_autoapply_plan.csv` / `mk_link_normalize_plan.csv`（リンク置換/正規化プラン）
- `mk_frontmatter_dry_run.csv` / `mk_frontmatter_apply_plan.csv`（FM不足/適用）
- `mk_retag_dry_run.csv` / `mk_retag_apply_plan.csv`（タグ提案/適用）
- `dir_inventory.csv` / `dir_delete_candidates.csv` / `dir_delete_applied_*.csv`（構成棚卸し/削除提案/適用）

## 注意事項
- 改行・マージ方針は `.gitattributes` で明示（LF標準、Windows系はCRLF）。
- 追跡除外は `.gitignore` を参照（`/notes/`, `.venv/`, `node_modules/`, `tmp/` など）。
- 大きなバイナリ（.docx 等）は必要最小限の追跡。LFS 化は必要に応じ検討。

