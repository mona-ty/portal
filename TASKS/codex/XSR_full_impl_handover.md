# XIV Submarines Return 実装ハンドオーバー（現状まとめ）

## 要点
- 目的: 潜水艦一覧の抽出→JSONブリッジ、Discord/Notion 連携、アラーム、UI表示強化。
- 主要追加: ルート表記切替、セクター解決（Excel+Alias）、Mogship取込、/svコマンド群、UI拡張（Docking/トップバー/ETA強調/FontScale/Accent）。
- 影響範囲: `apps/XIVSubmarinesReturn` 配下一式（UI/Services/Sectors/Commands/設定）。

---

## 実装内容（網羅）
- ルート表示/スナップショット表
  - 5列（スロット/名前/ランク/ETA/残り/ルート）。右端の不要列解消。
  - ルート表記切替: `Letters`（レター）/`ShortIds`（P番号）/`Raw`（原文）。
  - 表示優先: Resolver（Excel+Alias）→ 手動学習（`Config.RouteNames`）→ P番号/原文。
  - ルート列クリックでコピー（クリップボード）。

- セクター解決と完全対応表
  - `SectorResolver`（反射で Dalamud Excel `SubmarineExploration` を読取）。
    - `AliasIndex.json`（Map名→{レター→SectorId}）をマージし ID↔レター解決。
    - Excel未読込でも Alias のみで動作可能（フォールバック）。
  - `MogshipImporter`（API: `https://api.mogship.com/submarine/maps`/`.../sectors`）。
    - 全エリアのレター→SectorId 対応を取得→`AliasIndex.json` に保存→Resolver再読込。

- `/sv` コマンド群
  - `/sv test <code>`: `18`/`P18`/`R` 等を解決→`Map/Alias/SectorId/Name`出力。
  - `/sv debug`: Resolver状態（Aliasパス・更新時刻・Excel型・件数・Map別集計）出力。
  - `/sv export-sectors go`: Excel由来の一覧を `<ConfigDirectory>\Sectors.export.json` へ。
  - `/sv import-alias mogship`: Mogship API から完全対応表を取り込み更新。

- UI拡張
  - Docking有効化（`ImGui.GetIO().ConfigFlags |= DockingEnable`）。
  - トップバー（概要タブ）: 再読込 / Mogship取込 / Bridgeフォルダ / Configフォルダ。
  - 外観設定（概要タブ）
    - 行密度: `UiDensity`（Compact/Cozy）。
    - FontScale: `ImGui.SetWindowFontScale(Config.UiFontScale)` を適用（0.9〜1.2 推奨）。
    - ETA強調: `HighlightSoonMins` 分以下をアクセント色で強調（`EtaUnix` 無でも `RemainingText` をパース）。
    - アクセントカラー: `#RRGGBB` 入力（既定 `#1E90FF`）＋プレビュー。

- 取得物整理
  - 検証ダウンロード（`mogship_*.js/html` 等）→ `apps/XIVSubmarinesReturn/_external/` へ集約。
  - `.gitignore` に `/apps/XIVSubmarinesReturn/_external/` を追加（リポジトリ外管理）。

- 既存連携
  - Discord/Notion/アラーム機能を維持（GCal は廃止）。

---

## 主要ファイルと責務
- UI
  - `src/Plugin.UI.cs`: 概要タブ、トップバー、表、外観設定、Mogship取込/フォルダ操作。
  - `src/UI/Theme.cs`: 密度/色ユーティリティ（Accent色パース等）。
  - `src/UI/Widgets.cs`: アイコンボタン（`ImGuiComponents.IconButton`）。
- セクター解決
  - `src/Sectors/SectorModels.cs`: `SectorEntry`/`ResolveResult`。
  - `src/Sectors/AliasIndex.cs`: `AliasIndex.json` ロード/保存（初期に `Deep-sea Site: R→18` を内蔵）。
  - `src/Sectors/SectorResolver.cs`: Excel＋Alias 突合、`ResolveCode`、`GetAliasForSector`、`ReloadAliasIndex`、`GetDebugReport`。
  - `src/Sectors/MogshipImporter.cs`: Mogship API から全エリア取り込み→Alias更新。
- コマンド
  - `src/Commands/SectorCommands.cs`: `/sv test|debug|export-sectors|import-alias`。
- サービス（既存）
  - `src/Services/*.cs`: Discord/Notion/EtaFormatter 等。
- 設定/モデル
  - `src/Configuration.cs`: 既存＋UI/Resolver用の新規キー（後述）。
  - `src/Models.cs`: `SubmarineRecord`/`SubmarineSnapshot`。
- ブリッジ
  - `src/BridgeWriter.cs`: JSON出力（`%AppData%\XIVSubmarinesReturn\bridge\submarines.json`）。

---

## 追加/変更した設定キー（`Configuration`）
- 既存補足
  - `RouteDisplay`（Letters/ShortIds/Raw）
  - `SectorMapHint`（例: `Deep-sea Site`）
  - `RouteNames`（手動学習: P番号→レター）
- 新規
  - `UiRowDensity`（Compact/Cozy）
  - `UiFontScale`（0.9〜1.2推奨、Windowに適用）
  - `HighlightSoonMins`（ETA強調の分閾値）
  - `AccentColor`（`#RRGGBB`、既定 `#1E90FF`）

---

## ビルド/実行
- TargetFramework: `net9.0-windows`（x64）
- Dalamud参照: `Local.props` の `DalamudLibPath` に dev DLL パスを設定。
- ビルド: `dotnet build apps\XIVSubmarinesReturn\XIVSubmarinesReturn.csproj -c Release -p:Platform=x64`
- 出力DLL: `apps\XIVSubmarinesReturn\bin\x64\Release\net9.0-windows\XIVSubmarinesReturn.dll`

---

## 使い方（検証手順）
1) `/sv import-alias mogship` 実行 → 完全対応表が `AliasIndex.json` に展開される。
2) `/sv debug` で Map/エイリアス件数・Excel型を確認。
3) 概要タブ:
   - ルート表示=レター、Mapヒント=`Deep-sea Site` を指定。
   - FontScale/ETA強調(分)/Accent を適用。
   - 表でルートクリック→コピー、ETA強調（青）が効くことを確認。
4) `/sv test 18` / `/sv test R` 等で解決値を確認。

---

## 既知の注意/制約
- PlaceNameはExcelが取れない環境では空（解決や表示はレター/IDで可）。
- テーブル本格ソートは今後拡張（比較関数は用意済、UIと連動させるだけ）。
- Docking は Dalamud/環境側のドッキング有効設定に依存。

---

## 今後の候補
- 表ソート/フィルタのUI整備（列毎の適用・保存）。
- Docking前提のレイアウト分割（表/設定/ログ）。
- Accentのプリセット選択（青/緑/橙等）。

---

## 変更差分の主な追加/更新
- 追加
  - `src/Sectors/SectorModels.cs`
  - `src/Sectors/AliasIndex.cs`
  - `src/Sectors/SectorResolver.cs`
  - `src/Sectors/MogshipImporter.cs`
  - `src/Commands/SectorCommands.cs`
  - `src/UI/Theme.cs`
  - `src/UI/Widgets.cs`
- 更新
  - `src/Plugin.cs`（Resolver/Commands 初期化、Importer連携）
  - `src/Plugin.UI.cs`（Docking/トップバー/外観設定/ETA強調/コピー）
  - `src/Configuration.cs`（UIとResolver用キー追加）
  - `XIVSubmarinesReturn.csproj`（Compile 追加）
  - `.gitignore`（外部取得物の除外）

以上（次のCodex向けハンドオーバー）。
