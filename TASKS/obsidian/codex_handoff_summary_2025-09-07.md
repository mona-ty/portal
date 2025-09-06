# Codex ハンドオフサマリ（Obsidian 整理・リポジトリ再編）

## 概要（要点）
- 目的: Obsidian Vault（`notes/MK`）の整理と、リポジトリ全体の配置・生成物の集約を実施。
- 方針: バックアップ → ドライラン → レビュー → 適用。生成レポートは `TASKS/artifacts/` に統一。
- ブランチ: `codex/add-sources-1`（プッシュ済み）。

## 変更サマリ
- Vault 整理（`notes/MK`）
  - 分類・移動（段階適用）: `10_research` 54件、`50_code` 20件、`40_prompts` 11件、`20_notes` 10件、`30_projects` 1件を移動。
  - フロントマター: 不足のみ追記（title/tags/status/created/updated）。
  - タグ: 未タグへ付与 + 不足補完（既存尊重、最大5件）。
  - リンク: 監査 → 自動置換（しきい値/マージン）→ スタブ/プレースホルダ生成 → 正規化置換で not_found=0。
  - 不要フォルダ削除: 空＆未参照の旧系ディレクトリ群、重複（`notes/Dashboard.md`, `notes/70_templates`）を削除。
- スクリプト配置
  - Obsidian 整理系を `scripts/obsidian/` に集約（分類/監査/リンク/タグ/棚卸し ほか）。
  - スクリプトの出力先は `TASKS/artifacts/` に統一（パスの `tasks` → `TASKS` を統一）。
- TASKS の再編
  - ドキュメントを `TASKS/obsidian/`（Vault運用）と `TASKS/codex/`（Codex用途）に分離。
  - 生成物は `TASKS/artifacts/` に集中（CSV/DIFF/PLAN/LOG）。
  - ルート `reports/` と検証HTMLは `TASKS/artifacts/` に移動。
- Git 設定
  - `.gitignore` 補強（`/notes/`, `.venv/`, `node_modules/`, エディタ/一時/カバレッジ など）。
  - `.gitattributes` 追加（LF正規化、Windows系はCRLF、バイナリ指定、lockは merge=ours、Markdownは merge=union）。
  - 改行の再正規化（`git add --renormalize .`）。
- バックアップ
  - 最新バックアップ: `C:\notes_backup_20250907-001712`（今回作成）。

## 現在の標準構成
- Vault（Git追跡外）: `notes/MK`（`00_inbox, 10_research, 20_notes, 30_projects, 40_prompts, 50_code, 60_attachments, 70_templates, 90_archive`）。
- スクリプト: `scripts/obsidian/`（出力は `TASKS/artifacts/`）。
- タスク文書: `TASKS/obsidian/`（運用/方針）、`TASKS/codex/`（レビュー/計画）。
- 生成物: `TASKS/artifacts/`（監査/差分/適用ログ）。
- 保管庫: `archive/`（`archived/` を統合済み）。
- ツール: `tools/`。一時: `tmp/`（Git除外）。

## 代表レポート（`TASKS/artifacts/`）
- リンク: `mk_link_audit.csv`, `mk_link_autoapply_plan.csv`, `mk_link_normalize_plan.csv`。
- フロントマター: `mk_frontmatter_dry_run.csv`, `mk_frontmatter_apply_plan.csv`。
- タグ: `mk_retag_dry_run.csv`, `mk_retag_apply_plan.csv`, `mk_retag_supplement_*`。
- 構成棚卸し: `dir_inventory.csv`, `dir_delete_candidates.csv`, `dir_delete_applied_*.csv`。

## 主要スクリプト（`scripts/obsidian/`）
- 監査: `link_audit_mk.py`, `frontmatter_audit_mk.py`, `dir_inventory_mk.py`。
- タグ: `retag_mk_dry_run.py`, `retag_mk_apply.py`, `retag_mk_supplement_*`。
- リンク: `link_fuzzy_proposals_mk.py`, `link_autoapply_from_fuzzy_mk.py`, `link_normalize_fix_mk.py`, `link_apply_mk.py`。
- 分類/移動: `classify_mk.py`, `apply_classification_mk.py`。
- 添付: `attachments_dry_run_mk.py`。スタブ生成: `create_stubs_mk.py`。

## 運用ルール
- 変更フロー: 1) バックアップ → 2) ドライラン（`artifacts`）→ 3) レビュー → 4) 適用。
- Obsidian 設定: Files & Links の「Automatically update internal links」を ON 推奨。
- ポリシー（AIが解釈可能）: `TASKS/obsidian/ai_obsidian_policy_template.yaml`（本番はリネーム推奨）。

## 今後の推奨
- `ai_obsidian_policy_template.yaml` を本番名に固定（例: `ai_obsidian_policy.yaml`）。
- QuickAdd ホットキー設定、robocopy 差分バックアップの定期化。
- 必要なら Git LFS で大きなバイナリ（.docx 等）を管理。

