# XIV Submarines Return 実装レビュー要約

## 概要（要点）
- ルート表記の刷新（レター/P番号/原文 切替）と表の列整理（5列化）を実装。
- セクター解決基盤（Excel+JSON）を新設し、Mogship API から全エリアのレター対応を自動取り込み可能に。
- 検証/整備用の `/sv` 系コマンドを追加（test/debug/export/import）。
- 取得物の整理（apps/_external へ集約、.gitignore へ追加）。
- ビルドは `net9.0-windows`（Release/x64）で成功、警告のみ。

---

## 変更点（機能別）

### 1) ルート表示・スナップショット表
- ルート表示の切替（概要タブ）
  - 表示モード: レター優先 / P番号 / 原文（`Configuration.RouteDisplay`）。
  - Mapヒント入力（例: Deep-sea Site＝溺没海）（`Configuration.SectorMapHint`）。
  - 「Alias JSON再読込」ボタンで `AliasIndex.json` を再読み込み。
- スナップショット表の列整理
  - 列構成を5列に変更: スロット / 名前 / ランク / ETA/残り / ルート。
  - 余計な右端列の出現を解消。
- ルート表示ロジック（`BuildRouteDisplay`）
  - Resolver（Excel+Alias）優先 → 手動学習（`Config.RouteNames`）補完 → P番号/原文フォールバック。

対象コード: `src/Plugin.UI.cs`, `src/Configuration.cs`

### 2) セクター解決（Excel + Alias JSON）
- 新規: `Sectors` モジュール
  - `SectorModels.cs`: `SectorEntry`, `ResolveResult`。
  - `AliasIndex.cs`: Map名→{レター→SectorId} のJSONローダ/セーバ（初期は Deep-sea Site: R→18 を内蔵）。
  - `SectorResolver.cs`:
    - Dalamudの `IDataManager` から `SubmarineExploration` を反射で読み込む（`GeneratedSheets`/`Sheets` 両対応、型名スキャンも併用）。
    - Excel未取得時は Alias JSON から最小辞書を構築（フォールバック）。
    - `ResolveCode("18"/"P18"/"R", mapHint)`、`GetAliasForSector(id, mapHint)`、`ReloadAliasIndex()`、`GetDebugReport()` 等を提供。

対象コード: `src/Sectors/SectorModels.cs`, `src/Sectors/AliasIndex.cs`, `src/Sectors/SectorResolver.cs`

### 3) Mogship 連携（完全対応表の自動取込）
- 新規: `MogshipImporter.cs`
  - 取得先: `https://api.mogship.com/submarine/maps` / `.../submarine/sectors`
  - 処理: 全エリアの `lettername_en` を基に、Map名（英語名）→{レター→SectorId} を `AliasIndex.json` へ反映。
  - 反映後は Resolver を再読込し、ルート表示・`/sv` 解決へ即時反映。

対象コード: `src/Sectors/MogshipImporter.cs`

### 4) `/sv` コマンド群（検証・整備）
- `/sv test <code>`: `18`/`P18`/`R` 等を解決し、Map/Alias/SectorId/PlaceName をチャット出力。
- `/sv debug`: Resolverの状態（Aliasパス・更新時刻、Excel型、件数、Map別集計）を出力。
- `/sv export-sectors go`: Excel由来のセクター一覧を `<ConfigDirectory>\Sectors.export.json` に出力。
- `/sv import-alias mogship`: Mogship API から完全対応表を取り込み、`AliasIndex.json` に保存→Resolverを再読込。

対象コード: `src/Commands/SectorCommands.cs`

### 5) プラグイン構成・配線
- `Plugin.cs`
  - コンストラクタへ `IDataManager` を注入。
  - `SectorResolver` と `SectorCommands` を初期化/登録。
  - `AliasIndex.json` 初期生成（未存在時）。
  - 既存のHTTPクライアント/ログをImporterへ受け渡し。

対象コード: `src/Plugin.cs`

### 6) 取得物の整理
- 開発時に取得した Mogship 関連ファイルの集約:
  - `apps/XIVSubmarinesReturn/_external/mogship/` に移動。
  - `.gitignore` に `/apps/XIVSubmarinesReturn/_external/` を追加（追跡対象外）。

対象変更: 直下の `mogship_*.js/html` を移動、`.gitignore` 追記。

---

## ビルド/配置
- TargetFramework: `net9.0-windows`、Platform: x64。
- 依存DLL（Dalamud）は `Local.props` の `DalamudLibPath` で参照。
- ビルド例:
  - `dotnet build apps\XIVSubmarinesReturn\XIVSubmarinesReturn.csproj -c Release -p:Platform=x64`
- 出力: `apps\XIVSubmarinesReturn\bin\x64\Release\net9.0-windows\XIVSubmarinesReturn.dll`
- 警告: Null許容/未使用フィールド等が少数（動作には影響なし）。

---

## 使い方（確認手順）
1) Dalamudでプラグインを読み込み。
2) `/sv import-alias mogship` 実行 → 完全対応表を `AliasIndex.json` へ展開。
3) `/sv debug` で件数やMap一覧を確認。
4) 概要タブ:
   - 「ルート表示」＝レター、「Mapヒント」＝（例）Deep-sea Site を設定。
   - スナップショット表が 5 列で、ルートが M>R>O>… 表記になることを確認。
5) `/sv test 18` / `/sv test R` 等で解決結果を確認。

---

## 既知の注意点/残課題（任意）
- PlaceName（地点名）は Excel が参照できない環境では空になる可能性あり（レター/ID解決自体はAliasで可）。
- ルート表示のMapヒントは正確な表記（英語名）を推奨（例: Deep-sea Site）。
- Notion/Discord/Calendar 連携は既存動作を維持（今回の変更範囲外）。

---

## 変更ファイル一覧（主要）
- 追加
  - `src/Sectors/SectorModels.cs`
  - `src/Sectors/AliasIndex.cs`
  - `src/Sectors/SectorResolver.cs`
  - `src/Sectors/MogshipImporter.cs`
  - `src/Commands/SectorCommands.cs`
- 変更
  - `src/Plugin.cs`, `src/Plugin.UI.cs`, `src/Configuration.cs`
  - `XIVSubmarinesReturn.csproj`（Compile項目の追加）
  - `.gitignore`（外部取得物の除外）

---

## 参考（主なシナリオ例）
- 溺没海（Deep-sea Site）: `/sv test 18` → `Map=Deep-sea Site, Alias=R, SectorId=18, Name=Concealed Bay`（NameはExcel参照環境で表示）。
- 切替効果: 概要タブで「原文」「P番号」「レター」を切替→ルート列の表示が即時反映。

以上。

