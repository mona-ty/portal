# Apps Lab Monorepo

個人開発向けの小規模アプリをまとめたモノレポ（実験場）です。各アプリは `apps/` 配下に配置し、必要に応じて独立リポへ昇格できる構成にしています。

## 構成
- `apps/ff14-submarines/`: FF14 サブマリンの帰還時刻を OCR し Google カレンダーに登録するツール（Python）
- `apps/pomodoro-cli/`: シンプルなポモドーロタイマー CLI（Python）
- `apps/liftlog-ios/`: 筋トレ記録アプリの iOS スケルトン（SwiftUI）
- `apps/mahjong-scorer-ios/`: 麻雀点数計算アプリの iOS スケルトン（SwiftUI）
- `docs/`: ドキュメント（ハンドブック/新規アプリ作成手順）
- `scripts/`: 共通スクリプト（新規アプリ生成・リポ分割）
- `templates/`: 生成テンプレート
- `tests/`: 共有テスト（将来は各アプリ配下へ移行）
- `archive/`: 退避用
- `tmp/`: 一時ファイル・生成物（Git管理外）

## ドキュメント
- 開発ハンドブック: `docs/handbook.md`
- 新規アプリ作成ワークフロー: `docs/new_app.md`

## 運用の要点
- 日々の試行錯誤やノートはリポ外ワークスペースへ（例: `C:\\codex-work\\<repo>`）
- アプリが成熟したら `scripts/split_repos.ps1` で独立リポへ昇格
- CI: `.github/workflows/python-tests.yml`（Windows/Ubuntu で unittest 実行）

## フォルダ運用ガイド（C:\\Codex）
- 標準ディレクトリ
  - `apps/`: 各アプリの実体（XIVSubmarinesReturn ほか）
  - `docs/`, `templates/`, `scripts/`, `tests/`, `TASKS/`, `tools/`, `archive/`, `tmp/`
  - `workspace/`, `external/`: 作業場/外部物（Git管理外。`.gitignore` 済み）
- 退避運用
  - アーカイブは `archive/` に移動（履歴保持。基本は閲覧専用）
  - 一時生成物は `tmp/`（定期的に掃除）
- ノート
  - `notes/` は Obsidian 用のため Git 管理外（`.gitignore` 済み）
- Codex 起動（config.toml 駆動）
  - Windows: `scripts\win\Start-WSLAndCodex.ps1`
  - 自動走査レポート: `TASKS/autoscan/scan_YYYYMMDD-HHMM.md`
  - モデル/推論深度は `config.toml` で制御（`model_reasoning_effort = "high"` など）

### apps の重複名（ff14-submarines / ff14_submarines）の扱い
- `apps/ff14-submarines/`（新・多機能）
  - 目的: FF14サブマリンの帰還予定を OCR し Googleカレンダー登録
  - 機能: 自動セットアップ（ネイティブ補助含む）、ホットキー、オーバーレイ、Google連携、設定/ログ等
- `apps/ff14_submarines/`（軽量・旧）
  - 目的: OCRテキストからETA抽出、フォルダ監視でPNG取り込み、通知/Discord簡易送出
  - 規模: 最小構成（`__main__.py`, `ocr.py` 中心）
- 推奨: 前者（ハイフン版）を正式運用。後者（アンダースコア版）はレガシーとして archive 退避を検討（要確認）。

XIV Submarines Return

概要
- FC工房の潜水艦リスト（帰還予定など）をゲーム内UI/メモリから取得し、外部ツール連携向けに JSON を出力する Dalamud プラグインです。
- 出力先: `%AppData%\XIVSubmarinesReturn\bridge\submarines.json`
- 互換: 一部レガシー互換パス（`%AppData%\ff14_submarines_act\bridge\submarines.json` など）にもミラー出力します。

コマンド（/xsr）
- 基本: `help`, `dump`, `open`, `addon <name>`, `version`, `ui`
- デバッグ: `probe`, `dumpstage`
- 直接コマンド: `/subdump`, `/subopen`, `/subaddon <name>`, `/subcfg`

設定UI
- 開き方: `/subcfg` またはプラグイン設定から開く
- 主な項目:
  - Addon 名（監視対象）
  - 工房UIオープン時の自動取得（Auto-capture）
  - メモリフォールバック（UIで取れないときにメモリ参照）
  - SelectString 抽出（高速経路）/詳細抽出（追加走査）
  - 攻撃的フォールバック（周辺パネルから補完）
  - 既定名（`Submarine-<n>`）許可（メモリ）

実装メモ（概要）
- UIテキスト抽出: Addon の TextNode を走査（NodeList と Component を DFS）。
- SelectString 最適化: `AddonSelectString -> PopupMenu.List -> ItemRenderer.RowTemplate(TextNode)` から候補を高速抽出。
- 自動取得: `IFramework.Update` 内で対象 Addon の可視状態を監視し、一定間隔で取得。

ビルド（ローカル）
1) Dalamud 開発用ライブラリのパスを確認: 例 `C:\Users\<User>\AppData\Roaming\XIVLauncher\addon\Hooks\dev`
2) ルートまたは csproj と同じフォルダに `Local.props` を作成（例）:
   ```xml
   <Project>
     <PropertyGroup>
       <DalamudLibPath>$(APPDATA)\XIVLauncher\addon\Hooks\dev</DalamudLibPath>
       <!-- 任意: ビルド後に devPlugins へ自動コピー -->
       <DevPluginsDir>$(APPDATA)\XIVLauncher\devPlugins\XIVSubmarinesReturn</DevPluginsDir>
     </PropertyGroup>
   </Project>
   ```
3) ビルド（x64/Release）:
   `dotnet build -c Release -p:Platform=x64`

開発用配置（devPlugins）
- フォルダ: `%AppData%\XIVLauncher\devPlugins\XIVSubmarinesReturn`
- 配置物: `XIVSubmarinesReturn.dll`, `manifest.json`, `icon.png`（`manifest.json` は DLL にも埋め込み）
- 起動確認: `/xsr version` で `0.1.1` などのバージョン表示を確認

補足（デバッグ）
- `dumpraw`: ヘッダなどを含む生テキストを対象。SelectString の各行（Submarine-1..4 など）は通常出力対象外です。
- `dumptree`: 最後に `-- SelectString items --` として候補一覧を列挙します。

開発ドキュメント
- 詳細なアーキテクチャ、JSON スキーマ、外部連携（Discord/Notion/任意で Google Calendar）については `DEVELOPMENT.md` を参照してください。
