XIV Submarines Return

Commands (updated)
- /xsr: help, dump, open, addon <name>, version, ui
- /xsr: probe, dumpstage (debug)
- Direct: /subdump, /subopen, /subaddon <name>, /subcfg

Settings UI
- Open with /subcfg or plugin settings
- Addon Name, Auto-capture, Memory fallback, SelectString extraction, Aggressive fallback, Accept default names (memory)

概要
- 工房/潜水艦まわり（艦一覧など）を直接 UI から取得し、外部連携用 JSON を出力する Dalamud プラグイン。
- 出力先: `%AppData%\XIVSubmarinesReturn\bridge\submarines.json`
- 移行互換: 旧 `%AppData%\ff14_submarines_act\bridge\submarines.json` 等へミラー書き込み（初回コピー）。

コマンド（/xsr）
- 基本: `help`, `dump`, `open`, `addon <name>`, `version`
- デバッグ: `probe`, `probeall`, `probeidx <name>`, `dumpraw`, `dumptree`
- 互換: `/subdump`, `/subopen`, `/subaddon <name>`

実装要点（現状）
- Addon の TextNode 列挙（NodeList + Component 配下 DFS）。
- SelectString 専用抽出（新規）: `AddonSelectString -> PopupMenu.List -> ItemRenderer.RowTemplate(TextNode)` を走査し、各項目の文字列を直接取得。
- 自動取得: `IFramework.Update` で対象アドオン可視時に 1 回書き出し（10 秒クールダウン）。

ビルド手順
1) Dalamud SDK パスを確認: `C:\Users\<User>\AppData\Roaming\XIVLauncher\addon\Hooks\dev`
2) `apps/XIVSubmarinesReturn/Local.props` を作成/編集:
   ```xml
   <Project>
     <PropertyGroup>
       <DalamudLibPath>C:\\Users\\<User>\\AppData\\Roaming\\XIVLauncher\\addon\\Hooks\\dev</DalamudLibPath>
     </PropertyGroup>
   </Project>
   ```
3) ビルド（Release/x64）:
   `dotnet build apps\XIVSubmarinesReturn\XIVSubmarinesReturn.csproj -c Release -p:Platform=x64`

配置（devPlugins）
- フォルダ: `%AppData%\XIVLauncher\devPlugins\XIVSubmarinesReturn`
- 配置物: `XIVSubmarinesReturn.dll`（manifest は DLL に埋め込み）
- 反映確認: `/xsr version` が `0.1.1` 表示なら反映済み

メモ
- `dumpraw` はヘッダに加え、SelectString の各項目（Submarine-1〜4）も出力対象になりました。
- `dumptree` は末尾に `-- SelectString items --` として抽出済みの一覧を併記します。

補足（ビルド設定の汎用化）
- `Local.props` が無い場合でも、csproj が以下の順で `DalamudLibPath` を解決します。
  - 環境変数 `DALAMUD_LIB_PATH`
  - `$(APPDATA)\XIVLauncher\addon\Hooks\dev`
- 他PCでもそのままビルド可能です。必要に応じて `Local.props` に `$(APPDATA)` ベースで上書き設定してください。
