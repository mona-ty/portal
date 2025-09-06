# XIVSubmarinesReturn 改修まとめ（レビュー用・リフレッシュ版）

## 概要
- 対象: apps/XIVSubmarinesReturn（Dalamudプラグイン / .NET 9）
- 目的: Notionキー安定化、日本語/i18n整備、FC補完、アラーム安定化、Discord堅牢化、UIタブ分割、パッケージ運用改善
- 状態: 主要タスク適用済み。DLLビルド成功（zipはオプションで制御）。

## 変更点サマリ
- Notion
  - KeyMode: PerSlot(既定)/PerSlotRoute/PerVoyage 切替（StableId生成を切替）
  - Validate: EnsureDatabasePropsAsync() + UIボタンでプロパティ検証
- 日本語/i18n
  - Extractors: JPトークン集中管理、JP先行マッチ、ノイズ行スキップ
  - EtaFormatter: RemainingText を「分/時間」に統一（重複代入除去）
  - Plugin/Discord: 主要メッセージを日本語に統一（残/分）、送信前サニタイズ維持
- アラーム（P2）
  - 発火条件を「prev > lead && now <= lead」（初回のみmins==lead許容）に変更
  - 出力は「(残 n分)」に統一
- Discord（P3）
  - 429: Retry-AfterベースでJitter(0–2s) + 上限(最大30s) を付与し一回再送
- UI（タブ分割 + テーブル整備）
  - タブバー: Overview / Snapshot（暫定）
  - Snapshotテーブル: Slot | Name | Rank | ETA(Local) | Remaining | Route（リサイズ可）
- 基盤
  - 共有HttpClient + User-Agent（XIVSubmarinesReturn/<ver>）
  - GCalを `#if XSR_FEAT_GCAL` でガード（標準ビルド非依存）
- Packaging（P4）
  - `SkipPack`/`MakeZip` でzip生成を制御（ロック回避運用）

## UI構成（暫定タブ）
- Overview: 基本トグル（自動取得/メモリ/SelectString 詳細/フォールバック）、AddonName、SlotAliases、Notion/Discord/Alarm/Debug設定
- Snapshot: スナップショット一覧（Slot/Name/Rank/ETA(Local)/Remaining/Route）
  - 値は EtaLocal/RemainingText/RouteShort を優先して表示

（補足）Tab構成の要望があれば「Notion/Discord/Alarm/Debug」も個別タブに分割可能です。

## 動作確認の観点
- Notion: PerSlotで同一スロット更新、Validateで不足/型不一致が明示
- Discord: 日本語（残/分）で出力、429時にJitter+上限で再送
- Dump/Auto: 取得失敗/成功/例外が日本語表示、Auto時にJSON書き出しメッセージ
- Alarm: lead遷移で一度だけ「ETA HH:mm (残 n分)」を通知

## ビルド/パッケージ
- DLLのみ（zipスキップ）:
  - `dotnet build apps\XIVSubmarinesReturn\XIVSubmarinesReturn.csproj -c Release -p:Platform=x64 -p:SkipPack=true`
- zip有効/無効:
  - 有効（既定）: `-p:MakeZip=true`
  - 無効: `-p:MakeZip=false`

## 変更ファイル一覧（主なもの）
- src/Configuration.cs（NotionKeyMode）
- src/Services/NotionClient.cs（StableId切替、Validate、ログ簡潔化）
- src/Plugin.UI.cs（TabBar追加、Snapshotテーブル拡張）
- src/Plugin.cs（共有HttpClient+UA、GCalガード、ダンプ出力の日本語化）
- src/Services/AlarmScheduler.cs（遷移検出・日本語出力、GCalガード）
- src/Services/DiscordNotifier.cs（429 Jitter/上限、日本語統一）
- src/Services/EtaFormatter.cs（RemainingText 日本語化・重複除去）
- src/Extractors.cs（Parser薄ラッパー化）
- DalamudPackager.targets（SkipPack/MakeZip）

## 既知の軽微事項
- AddHandlerのHelpMessage（Dump/Config/Open）は周辺に文字化けが混在しており、実行時出力の日本語化を優先しました（将来、安全アンカーで差し替え可能）。

## 参考（確認用コマンド）
- Notion Validate: 設定UI → Notion → 「Validate properties」
- Discord Test: 設定UI → Discord → Test（残/分・429再送の挙動）
- Dump: /xsr dump（失敗/成功/例外の日本語表示）
- Alarm: リード分（例: 5,0）でlead遷移時に一度だけ通知
