# XSR 修正設計（参照: TASKS/codex/review.txt）

## 目的/スコープ
- UIの操作性と視認性を強化し、ルート/セクター機能と連携した一連の操作（取得→表示→連携）が素早く行えるようにする。
- 既存機能（Discord/Notion/アラーム/GCal）の互換性維持。

---

## 対応方針（レビュー項目→設計）

1) トップバー（概要タブ）
- 追加ボタン: 再読込・Mogship取込・Bridgeフォルダ・Configフォルダ
- 実装: `UI/Widgets.IconButton(FontAwesomeIcon, tooltip)` を使用。押下で非同期処理→`UiBuilder.AddNotification`
- 配色/余白: `UI/Theme.UseDensity(UiRowDensity)` を適用、アクセントは `AccentColor`

2) スナップショット表（5列 + 強化）
- 構成: スロット / 名前 / ランク / ETA/残り / ルート
- テーブル機能:
  - 並び替え: ImGui Tables SortSpecs（列ヘッダクリック）対応
  - フィルタ: テキスト（名前/ルート/短縮）とETAしきい値（<= min）
  - コピー: ルート列クリックでクリップボードへ
  - ハイライト: `HighlightSoonMins` 以下は `AccentColor` で強調
- 実装: `UI/SnapshotTable.cs` へ切出し（描画・状態・比較・フィルタを集約）

3) ルート表記切替
- 表示優先: Resolver（Excel+Alias）→ 手動学習（`Config.RouteNames`）→ P番号（ShortIds）/原文（Raw）
- 既存 `BuildRouteDisplay` は `SnapshotTable` 内部のヘルパへ移設（責務集約）

4) Docking対応
- `ImGui.GetIO().ConfigFlags |= DockingEnable`（現状適用済）
- 将来的にビュー分割（表/設定/ログ）を可能にするため、`SnapshotTable` を独立呼び出し可能に

5) 外観設定（概要タブ）
- 行密度: `UiDensity`（Compact/Cozy）→ `Theme.UseDensity` で余白調整
- FontScale: `ImGui.SetWindowFontScale(Config.UiFontScale)` 適用
- ETA強調: `HighlightSoonMins`（分）設定
- アクセントカラー: `AccentColor`（#RRGGBB）設定 + プレビュー（`ImGui.ColorButton`）

6) Mogship取込
- UIボタン + `/sv import-alias mogship` の二系統
- 進捗/結果を通知（`UiBuilder.AddNotification`）
- 取込後は `Resolver.ReloadAliasIndex()` 実行

7) `/sv` コマンド体系
- test: `/sv test <code>` → Map/Alias/SectorId/Name
- debug: `/sv debug` → Alias/Excel/件数/Map集計
- export: `/sv export-sectors go` → JSON出力
- import: `/sv import-alias mogship` → 取込→再読込
- ヘルプ短文の充実（usage 行の見直し）

8) 受入基準（レビュー準拠）
- トップバーの4アイコンの動作と通知
- 表の5列構成・並び替え・フィルタ・コピー
- ETA強調（分）/行密度/FontScale の反映
- ルート表記切替（レター最優先）とMapヒントによる解決
- `/sv` コマンド各種の動作
- 既存連携（Discord/Notion/アラーム）の回帰

---

## 実装タスク（詳細）

A) 設定/モデル
- Configuration.cs
  - 既存追加済: `UiRowDensity`, `UiFontScale`, `HighlightSoonMins`, `AccentColor`
  - 並び替え状態・フィルタ文字列の保存（必要なら）

B) UI基盤
- `UI/Theme.cs`: 密度・色（hex→Vector4）・一時Push/Pop RAII（実装済）
- `UI/Widgets.cs`: IconButton（実装済）

C) スナップショット表
- 新規: `UI/SnapshotTable.cs`
  - プロパティ: `FilterText`, `SortBy`, `SortAscending`
  - API: `Draw(list<SubmarineRecord>, Config, Resolver)`
  - 機能: 並び替え（SortSpecs）/フィルタ/コピー/ハイライト

D) 概要タブ（Plugin.UI.cs）
- トップバー描画（実装済、通知を追補）
- 外観設定UI（密度/FontScale/ETA強調/Accent）
- `SnapshotTable` 呼び出しに置換（現行テーブル描画ロジックの移譲）

E) Mogship取込
- 例外時の通知・ログ（`IPluginLog`）
- 取込後の自動リロードとUI状態更新

F) コマンド
- `/sv` ヘルプメッセージの明確化
- 失敗時の理由を簡潔に出力（例: alias path not found / network error）

---

## 変更ファイル一覧（予定）
- 追加: `src/UI/SnapshotTable.cs`
- 更新: `src/Plugin.UI.cs`（`SnapshotTable`への移行/通知強化）、`src/Configuration.cs`
- 既存: `UI/Theme.cs`, `UI/Widgets.cs`, `Sectors/*`, `Commands/SectorCommands.cs` は最小加筆

---

## テスト/検証
- 単体: `SnapshotTable` 並び替え/フィルタ/コピー/強調の境界値（ETA=0/負/残り文言のみ）
- 結合: 取込→`/sv debug`→表のレター表示（Deep-sea Site, ほか複数海域）
- 回帰: Discord/Notion/アラーム/GCal の主要フロー
- UI: Docking有効環境での挙動、FontScale/Accentの即時反映

---

## リスク/対処
- Excel未読込環境: Aliasのみで解決可能（実装済）
- ネットワーク不良: Importerの失敗時は通知とリトライ手段（UI/コマンド）を残す
- ImGui更新: Tables API の仕様差分→SortSpecsの防御的実装

---

## 作業順序（短期）
1) `UI/SnapshotTable.cs` を新規実装（比較/フィルタ/コピー/強調/SortSpecs）
2) `Plugin.UI.cs` を `SnapshotTable` 呼び出しへ移行、通知を追加
3) `/sv` usage 表示を見直し
4) 受入基準の一括検証→微調整

---

## 成果物
- 変更PR（最小差分・既存互換）
- 確認チェックリスト（受入基準ベース）
- 必要に応じて GIF/スクリーンショット

以上。
