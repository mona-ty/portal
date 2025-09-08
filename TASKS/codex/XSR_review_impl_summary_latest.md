# XIV Submarines Return 実装内容まとめ（レビュー用）

## 要点（現状）
- ルート表示の刷新（レター/P番号/原文 切替）と表の列整理（5列化）を実装。
- セクター解決（Excel + Alias JSON）に加え、Mogship Trip Planner API からの完全対応表の自動取込を実装。
- `/sv` コマンド群（test/debug/export-sectors/import-alias）で検証・運用を支援。
- UI体験を拡張：Docking有効化、トップバー追加、アクセントカラー（青）/FontScale/ETA強調を導入。
- 取得物（検証用ダウンロード）を apps 配下に整理し、.gitignore で除外。

---

## 実装詳細

- ルート表示/スナップショット表
  - 表は5列（スロット/名前/ランク/ETA/残り/ルート）。右端の不要列を解消。
  - ルート表示は、Resolver（Excel+Alias）優先→学習（RouteNames）→P番号/原文にフォールバック。
  - ルート列はクリックで文字列をクリップボードにコピー。

- セクター解決と完全対応表
  - `SectorResolver`：DalamudのExcel（SubmarineExploration）を反射で読込み、`AliasIndex.json` をマージ。
    - Excel未読込時は Alias のみで解決可能なフォールバックを実装。
  - `MogshipImporter`：`https://api.mogship.com/submarine/maps`/`.../sectors` から Map名（英）・レター・SectorId を取り込み、`AliasIndex.json` を自動更新。

- `/sv` コマンド（チャット出力）
  - `/sv test <code>`：`18`/`P18`/`R` などを解決し、Map/Alias/SectorId/Name を表示。
  - `/sv debug`：Resolverの状態（Aliasパス/更新時刻、Excel型、件数、Map別集計）を出力。
  - `/sv export-sectors go`：Excel由来のセクター一覧を `<ConfigDirectory>\Sectors.export.json` へ出力。
  - `/sv import-alias mogship`：Mogship API から完全対応表を取り込み、`AliasIndex.json` を更新→Resolver再読込。

- UI 拡張
  - Docking 有効化：ImGui IO の ConfigFlags でドッキングを許可。
  - トップバー（概要タブ）
    - 再読込（スナップショット）/ Mogship取込 / Bridgeフォルダ / Configフォルダを開く（各ボタン）
  - 外観設定（概要タブ）
    - 行密度（Compact/Cozy）、FontScale（ウィンドウ単位の拡大）、ETA強調（分）を追加。
    - アクセントカラー（#RRGGBB、既定 #1E90FF）を設定・プレビュー可能。
  - ETA強調の堅牢化
    - `EtaUnix` が無い場合でも `RemainingText`（「xx分」「yy時間xx分」）から分数を推定して強調判定。

- 取得物の整理
  - `apps/XIVSubmarinesReturn/_external/` に mogship_* などのダウンロード物を集約。
  - `.gitignore` に `/apps/XIVSubmarinesReturn/_external/` を追記（リポジトリ外管理）。

- ビルド
  - TargetFramework: `net9.0-windows`（x64）
  - 出力: `apps\XIVSubmarinesReturn\bin\x64\Release\net9.0-windows\XIVSubmarinesReturn.dll`
  - 警告は Null許容/未使用中心で、機能動作に影響なし。

---

## 使い方（確認手順）
1) Dalamud でプラグイン読込。
2) `/sv import-alias mogship` 実行 → 全エリアのレター表を `AliasIndex.json` へ展開。
3) `/sv debug` で件数やMap一覧を確認。
4) 概要タブ：
   - ルート表示＝レター、Mapヒント（例: Deep-sea Site）を指定。
   - 外観設定の FontScale/ETA強調（分）/アクセント(#RRGGBB)を適用。
   - スナップショット表でルート列のクリックコピー、ETA強調が効いていることを確認。
5) `/sv test 18` / `/sv test R` 等で解決結果を確認。

---

## 受入観点（主なチェック）
- ルート表示が「M>R>O>…」等のレター表記へ切替可能（Mapヒント・Alias整備済み）。
- 表は5列・不要列なし。ルート列クリックでコピーできる。
- Docking 有効時、ウィンドウをドック/分割可能。
- 外観設定で FontScale/ETA強調・Accent の変更が反映される。
- `/sv test`/`/sv debug`/`/sv export-sectors`/`/sv import-alias` が期待通り動作。

---

## 既知の注意点/今後の候補
- PlaceName は Excel 読込不可の環境では空の可能性（解決や表示自体はレター/IDで可）。
- テーブルの本格的な並び替え（UIで指定→実データ並べ替え）はヘルパを実装済みのため、拡張容易。
- Docking 前提のレイアウト分割（表/設定/ログの別窓化）も拡張可能。

---

## 主要な変更ファイル
- 追加
  - `src/Sectors/SectorModels.cs`
  - `src/Sectors/AliasIndex.cs`
  - `src/Sectors/SectorResolver.cs`
  - `src/Sectors/MogshipImporter.cs`
  - `src/Commands/SectorCommands.cs`
  - `src/UI/Theme.cs`
  - `src/UI/Widgets.cs`
- 変更
  - `src/Plugin.cs`
  - `src/Plugin.UI.cs`
  - `src/Configuration.cs`
  - `XIVSubmarinesReturn.csproj`
  - `.gitignore`

以上。
