# XIV Submarines Return — 次回Codex向けハンドオーバー（現状すべて）

この文書は、現時点までのすべての実装内容を簡潔かつ網羅的にまとめ、次回のCodexが短時間で追従できるようにするためのハンドオーバーです。

## 1. プロジェクト概要 / 目的
- 目的: ゲーム内の潜水艦一覧を抽出し、JSONブリッジに出力。Discord/Notion（任意でGoogle Calendar）連携やアラーム通知、UI表示を提供。
- 追加の価値: ルート表記（M>R>…）の人間可読化、Mogshipの完全対応表取り込み、/svコマンド群で運用補助、使いやすいUI（Docking対応・ETA強調・アクセント色・FontScale）。

## 2. 現状の主な機能
- ルート表示の切替（Letters/ShortIds/Raw）
- セクター解決（Excel + Alias JSON）＋ Mogship Trip Planner API からの完全対応表取込
- /sv コマンド群（test/debug/export-sectors/import-alias）
- UI拡張（Docking、トップバー、表の強化、外観設定：FontScale/ETA強調/Accent色）
- JSONブリッジ出力（BridgeWriter）／既存のDiscord/Notion/GCal/ゲーム内アラーム連携

## 3. 重要ファイルと責務
- UI
  - `src/Plugin.UI.cs`
    - 概要タブ（トップバー、外観設定、スナップショット表示）、Docking有効化、FontScale適用
    - 新描画 `DrawSnapshotTable2()` により `SnapshotTable` を使用
  - `src/UI/SnapshotTable.cs`（新規）
    - 表描画の中核（フィルタ/ソート/残分強調/クリックコピー/ヘッダ色）
    - ルート表記は Resolver優先→手動学習→P番号/原文
  - `src/UI/Theme.cs`（密度/色ユーティリティ、hex→Vector4 など）
  - `src/UI/Widgets.cs`（IconButton など）
- セクター解決・データ
  - `src/Sectors/SectorModels.cs`（`SectorEntry`, `ResolveResult`）
  - `src/Sectors/AliasIndex.cs`（Map→{レター→SectorId} JSONのLoad/Save。初期に Deep‑sea Site: R→18 を内蔵）
  - `src/Sectors/SectorResolver.cs`（Excel＋Alias 突合、`ResolveCode`/`GetAliasForSector`/`ReloadAliasIndex`/`GetDebugReport`）
  - `src/Sectors/MogshipImporter.cs`（Mogship API から全エリアの完全対応表を取り込み、AliasIndex.json を更新）
- コマンド
  - `src/Commands/SectorCommands.cs`（`/sv test|debug|export-sectors|import-alias`）
- 既存サービス
  - `src/Services/*.cs`（Discord/Notion/GoogleCalendar/EtaFormatter、既存改善を維持）
- モデル/ブリッジ
  - `src/Models.cs`（`SubmarineRecord`/`SubmarineSnapshot`）
  - `src/BridgeWriter.cs`（JSON出力とレガシーパスへのコピー）

## 4. UI（概要タブ）の現在仕様
- トップバー（アイコン）
  - 再読込（スナップショットの再読み込み）
  - Mogship取込（完全対応表の取り込み→Resolver再読み込み）
  - Bridgeフォルダ/Configフォルダを開く
- 外観設定
  - 行密度: Compact/Cozy
  - FontScale: ウィンドウ内のフォント拡大
  - ETA強調(分): この分数以下は青で強調
  - Accent(#RRGGBB): アクセント色の設定＋プレビュー
- スナップショット表（SnapshotTable）
  - 列: スロット / 名前 / ランク / 残分（数値）/ ETA/残り / ルート（計6列）
  - フィルタ/ソートUI（上部に表示）
  - 残分の推定: EtaUnixが無い場合でも RemainingText（「xx分」「yy時間xx分」）から分を推定
  - 強調: 残分が閾値以下の場合、青で強調
  - ヘッダ: アクセント色に統一（背景/hover/active）
  - ルート: クリックでコピー、Resolver→学習→P番号/原文の順に表示

## 5. セクター解決（Excel + Alias）と Mogship 取込
- Resolver
  - DalamudのExcel `SubmarineExploration` を反射で読み込み（`GeneratedSheets`/`Sheets` 両対応、型スキャン込み）
  - `AliasIndex.json` をマージ（Map名→{レター→SectorId}）。Excelが取れない場合もAliasのみで可
- MogshipImporter
  - `GET https://api.mogship.com/submarine/maps` / `GET https://api.mogship.com/submarine/sectors`
  - Map英名と `lettername_en`/`id`/`mapId` から完全対応表を構築→AliasIndex.json へ保存→Resolver再読込

## 6. /sv コマンド
- `/sv test <code>`: `18`/`P18`/`R` などの解決（Map/Alias/SectorId/Name）
- `/sv debug`: Aliasパス/更新時刻、Excel型、件数、Map別集計を出力
- `/sv export-sectors go`: Excel由来の一覧を `<ConfigDirectory>\Sectors.export.json` に保存
- `/sv import-alias mogship`: Mogship API から完全対応表取り込み→Resolver再読込

## 7. 設定（Configuration）に追加したキー
- ルート/Resolver関連
  - `RouteDisplay`（Letters/ShortIds/Raw）
  - `SectorMapHint`（例: `Deep-sea Site`）
  - `RouteNames`（手動学習: P番号→レター）
- UI/テーブル関連
  - `UiRowDensity`（Compact/Cozy）
  - `UiFontScale`（0.9〜1.2推奨、ウィンドウに適用）
  - `HighlightSoonMins`（ETA強調の分閾値）
  - `AccentColor`（`#RRGGBB`、既定 `#1E90FF`）
  - `TableSortField`（0=Name/1=Slot/2=Rank/3=ETA）
  - `TableSortAsc`（昇順フラグ）
  - `TableFilterText`（テキストフィルタ）

## 8. ビルド/実行
- TargetFramework: `net9.0-windows`（x64）
- Dalamud参照: `Local.props` に `DalamudLibPath` を設定
- ビルド: `dotnet build apps\XIVSubmarinesReturn\XIVSubmarinesReturn.csproj -c Release -p:Platform=x64`
- 出力DLL: `apps\XIVSubmarinesReturn\bin\x64\Release\net9.0-windows\XIVSubmarinesReturn.dll`

## 9. 確認手順（クイック）
1) `/sv import-alias mogship` → 完全対応表を取り込み
2) `/sv debug` → Map/alias件数、Excel型の確認
3) 概要タブ
   - ルート表示＝レター、Mapヒント＝（例）Deep‑sea Site
   - FontScale/ETA強調（分）/Accent を調整
   - 表で残分が閾値以下の強調、ルートクリックコピーを確認
4) `/sv test 18` / `/sv test R` で解決値を確認

## 10. 既知の注意点
- PlaceName はExcelが読めない環境では空の可能性（レター/ID解決は可能）
- テーブルのヘッダクリック（SortSpecs）での並び替えは未実装（UIコンボ/Ascで代替中）
- Docking はDalamud側の設定に依存

## 11. 次にやると良いこと（TODO）
- SortSpecs対応（ヘッダクリックでの並び替えと昇降の保持）
- 旧 `DrawSnapshotTable()` の整理（`DrawSnapshotTable2()`へ完全移行）
- Docking前提のビュー分割（表/設定/ログ）、状態保存
- Accentのプリセット選択、テーマ整備

## 12. 外部取得物の整理
- 検証用ダウンロード（`mogship_*.js/html` など）は `apps/XIVSubmarinesReturn/_external/` に集約
- `.gitignore` に `/apps/XIVSubmarinesReturn/_external/` を追加し追跡外

---

以上。これをベースに、テーブルSortSpecsの実装や旧描画の整理、Dockingレイアウトの強化を進めるとスムーズです。
