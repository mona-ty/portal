# FF14 Submarines ACT Plugin (skeleton)

ACT のログから潜水艦の出港/探索完了イベントを検知する C# プラグインのスケルトンです。

- プロジェクト: `apps/ff14-act-submarines/`
- 対応: JP ログ（`潜水艦「NAME」が出港しました` / `潜水艦「NAME」探索完了 ...`）
- 目的: OCR を使わずに ETA 管理・通知・カレンダー連携に拡張するベース

## Build
- Visual Studio 2022（.NET Framework 4.8 開発）
- 参照設定:
  - `Advanced Combat Tracker.exe` を参照（既定: `C:\\Program Files (x86)\\Advanced Combat Tracker\\Advanced Combat Tracker.exe`）
  - 必要に応じて `FFXIV_ACT_Plugin.dll` も参照（`Plugins` 配下）
- 出力: Class Library（DLL）

## Install (ACT)
1) ACT を起動し、Plugins タブで本 DLL を追加。
2) 初期化に成功すると「FF14 Submarines」タブが現れ、検知ログが流れます。
3) FFXIV_ACT_Plugin を有効にしてプレイ中に、工房エリアで潜水艦を出港/受領してログを発生させてください。

## 仕組み（現状）
- `OnLogLineRead` を購読し、ネットワークログ行（`00|...|0039||<message>|...`）またはプレーン行を解析。
- 正規表現:
  - 出港: `潜水艦「(?<name>.+?)」が出港しました`
  - 完了: `潜水艦「(?<name>.+?)」.*探索完了`
- 試験的にタイムスタンプを `00|<timestamp>|...` から取得。失敗時は `DateTime.Now`。

## 次の拡張
- 所要時間テーブルを設定画面に持ち、出港→ETA を算出して一覧表示。
- 通知（Windows トースト/Discord）、ICS/Google Calendar 連携。
- EN ロケール対応、ネットワークイベントの直接購読（FFXIV_ACT_Plugin API）。

## ファイル
- `FF14SubmarinesActPlugin.csproj`: .NET Framework 4.8 クラスライブラリ
- `PluginMain.cs`: `IActPluginV1` 実装（イベント購読とUI）
- `LogParser.cs`: ログ解析（出港/完了の抽出）
- `Ui/SettingsControl.*`: シンプルなログ表示UI

