XIV Submarines Return

概要
- 工房/潜水艦の一覧をゲーム内UIから取得し、外部連携用JSONを書き出すDalamudプラグイン。
- 出力先: `%AppData%\XIVSubmarinesReturn\bridge\submarines.json`
- 互換: 旧 `%AppData%\ff14_submarines_act\bridge\submarines.json` へ初回コピー/ミラー対応。

コマンド
- 基本: `/xsr help`, `/xsr dump`, `/xsr open`, `/xsr addon <name>`, `/xsr version`, `/xsr ui`
- デバッグ: `/xsr probe`, `/xsr probeall`, `/xsr probeidx <name>`, `/xsr dumpraw`, `/xsr dumptree`
- 互換: `/subdump`, `/subopen`, `/subaddon <name>`, `/subcfg`

設定UI
- 開き方: `/xsr ui` または `/subcfg`（プラグイン設定からも可）
- 主な項目: Addon Name / Auto-capture / Memory fallback / SelectString extraction / Aggressive fallback / Accept default names (memory)

動作要点
- AddonのTextNode列挙（NodeList + Component配下DFS）でテキストを抽出
- SelectString専用抽出: `AddonSelectString -> PopupMenu.List -> ItemRenderer.RowTemplate(TextNode)` を走査
- 自動取得: 対象アドオン可視時に1回書き出し（10秒クールダウン）

ビルド（モノレポ内）
1) Dalamud SDKパスを確認: `C:\\Users\\<User>\\AppData\\Roaming\\XIVLauncher\\addon\\Hooks\\dev`
2) `apps/XIVSubmarinesReturn/Local.props` を作成/編集（例）:
   ```xml
   <Project>
     <PropertyGroup>
       <DalamudLibPath>$(APPDATA)\\XIVLauncher\\addon\\Hooks\\dev</DalamudLibPath>
     </PropertyGroup>
     <PropertyGroup>
       <!-- 任意: ビルド後に devPlugins へ自動コピー -->
       <DevPluginsDir>$(APPDATA)\\XIVLauncher\\devPlugins\\XIVSubmarinesReturn</DevPluginsDir>
     </PropertyGroup>
   </Project>
   ```
3) ビルド（Release/x64）:
   `dotnet build apps\\XIVSubmarinesReturn\\XIVSubmarinesReturn.csproj -c Release -p:Platform=x64`

配布/リリース
- 手動確認: `XIVLauncher\\devPlugins\\XIVSubmarinesReturn` に DLL/manifest/icon を配置（または自動コピー）
- パッケージ作成（任意）: `-p:MakeZip=true` で `latest.zip` を生成（DalamudPackager）
- バージョン確認: `/xsr version` が `1.0.0` なら反映済み

補足
- `Local.props` が無くても、csproj は `DALAMUD_LIB_PATH` → `$(APPDATA)\\XIVLauncher\\addon\\Hooks\\dev` の順に解決
- 他PCでもそのままビルド可能。必要に応じて `Local.props` でパスを上書き

メモ（デバッグ機能）
- `dumpraw`: ヘッダ + SelectStringの各項目（Submarine-1〜4）を出力
- `dumptree`: 末尾に `-- SelectString items --` として抽出一覧を併記

