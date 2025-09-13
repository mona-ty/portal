XIV Submarines Return

概要
- 工房/潜水艦の一覧をゲーム内UIから取得し、外部連携用JSONを書き出すDalamudプラグイン。
- 出力先: `%AppData%\XIVSubmarinesReturn\bridge\submarines.json`
- 互換: 旧 `%AppData%\ff14_submarines_act\bridge\submarines.json` へ初回コピー/ミラー対応。

コマンド（抜粋）
- 基本: `/xsr help`, `/xsr dump`, `/xsr open`, `/xsr addon <name>`, `/xsr version`, `/xsr ui`
- メモリ診断: `/xsr memscan [slot]`（候補をフェーズ付きで表示/ログ）、`/xsr memdump <slot> [offHex] [len]`
- ルート手動操作: `/xsr setroute <slot> <chain>`, `/xsr getroute [slot]`
- 互換: `/subdump`, `/subopen`, `/subaddon <name>`, `/subcfg`

設定UI
- 開き方: `/xsr ui` または `/subcfg`（プラグイン設定からも可）
- Debugタブ（2カラム）
  - メモリ/スキャン: MemoryOnlyMode, MemoryRouteScanEnabled, ScanWindowBytes, ZeroTerminated, ScanPhaseEnabled
  - ゲート/イベント: EnableTerritoryGate, EnableAddonGate, PreferArrayDataFirst（Array採用の基盤）, AddonLifecycle（準備中）
  - 採用: AdoptPreferLonger, AdoptAllowDowngrade(false 推奨), AdoptCachePersist, AdoptTtlHours(12h)
- アラームタブ
  - ゲーム内アラームのタイミング設定（5/10/30/0 分等）
  - Discord: Webhook, 最早のみ, 埋め込み, 通知の最小間隔(分)
  - Notion: Token/DB/プロパティ、Upsertキー方式（スロット/スロット+ルート/航海毎）

動作要点（Memory-First）
- 既定で UI補完なしのメモリ優先。CurrentExplorationPoints の末尾列から、構造体内/Addon周辺の 3–5点列を推定
- スキャン: stride(1/2/4) + 0終端/lenhdr/位相（bytePhase）で候補化、tail 必須（順/逆・連続/末尾一致）
- 採用: AdoptBest（長さ優先/TTL=12h/Confidence）、フル獲得後はTTL内は降格しない
- 診断: memscan で各フェーズの候補とスコアを確認。memdump で近傍Hexを安全に出力

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
- 手動: `XIVLauncher\\devPlugins\\XIVSubmarinesReturn` に DLL/manifest/icon を配置（または自動コピー）
- パッケージ（任意）: `-p:MakeZip=true` で `latest.zip` を生成（DalamudPackager）
- バージョン確認: `/xsr version` が `1.1.0`（日時付き）なら反映済み

Custom Repo（Dalamud）
- 公開レポジトリの Raw URL を Dalamud → Settings → Experimental → Custom Plugin Repositories に追加
  - Raw URL: `https://raw.githubusercontent.com/mona-ty/XIVSubmarinesReturn/main/repo.json`
  - 検索/導入: `/xlplugins` → `XIV Submarines Return`
  - 備考: リポジトリを Public に切替後に有効

補足
- `Local.props` が無くても、csproj は `DALAMUD_LIB_PATH` → `$(APPDATA)\\XIVLauncher\\addon\\Hooks\\dev` の順に解決
- 他PCでもそのままビルド可能。必要に応じて `Local.props` でパスを上書き

メモ（デバッグ機能）
- `dumpraw`: ヘッダ + SelectStringの各項目（Submarine-1〜4）を出力
- `dumptree`: 末尾に `-- SelectString items --` として抽出一覧を併記
