# XIVSubmarinesReturn 改修サマリ (N5/N6/N7/N8/N9/N10)

- 対象: apps/XIVSubmarinesReturn (.NET 9, Dalamud プラグイン)
- 目的: Notionキー安定化、日本語/i18n整備、FC補完、ビルド堅牢化、ログ簡潔化

## 実施内容

- N5: Notion アップサートキー方式の追加
  - 追加: `NotionKeyMode { PerSlot, PerSlotRoute, PerVoyage }`（既定: PerSlot）
  - 変更: `NotionClient.BuildStableId(snap,it)` を KeyMode 切替に対応
    - PerSlot => `Character|World|Slot`（フォールバック: `Name|Slot`）
    - PerSlotRoute => `Character|World|Slot|RouteKey`
    - PerVoyage => 従来 (`Name|EtaUnix|Slot`)
  - UI: 設定の Notion セクションにラジオボタンを追加

- N7: Notion プロパティ検証（最小）
  - 追加: `EnsureDatabasePropsAsync()`（/v1/databases/{id} を取得し schema 確認）
  - UI: 「Validate properties」ボタン追加（結果は `_uiStatus` に表示）

- N8: FC 名の簡易補完
  - 追加: `TryEnrichIdentityFromLines(...)`（UIテキストから「フリーカンパニー / Free Company」を検出し Snapshot.FreeCompany に反映）

- N9: GCal のビルド時ガード
  - `_gcal` 生成・参照を `#if XSR_FEAT_GCAL` で囲い、標準ビルドから除外

- N10: 信頼性・ログ簡潔化
  - 共有 `HttpClient` を導入し User-Agent を `XIVSubmarinesReturn/<version>` に統一
  - Notion/GCal の HTTP エラーログを DebugLogging=true 時のみ本文出力（平常時はステータスのみ）

- N6: 日本語/i18n（進行）
  - Extractors.cs: 日本語トークン集中管理、残り時間/所要時間・xx時間yy分の先行マッチ、ノイズ行スキップ
  - EtaFormatter.cs: RemainingText を UTF-8 の「分/時間」で生成
  - Plugin.cs: 一部のチャット出力を日本語化（ダンプ失敗/成功、メモリ失敗/成功、SelectString 提示、設定UI関連、学習成功/失敗）
  - DiscordNotifier.cs: 送信前サニタイズ（`�c`→`残`、`��`→`分`）

## 変更ファイル
- src/Configuration.cs: NotionKeyMode 追加
- src/Services/NotionClient.cs: BuildStableId 切替、EnsureDatabasePropsAsync、ログ簡潔化
- src/Plugin.UI.cs: Notion KeyMode ラジオ、Validate ボタン
- src/Plugin.cs: 共有 HttpClient、GCal ガード、FC補完、（日本語出力の一部調整）
- src/Services/AlarmScheduler.cs: GCal ガード
- src/Services/GoogleCalendarClient.cs: ログ簡潔化
- src/Services/DiscordNotifier.cs: サニタイズ、送信時の日本語化（残/分）
- src/Extractors.cs: JPトークン・JPパターン強化
- src/Services/EtaFormatter.cs: RemainingText 日本語化

## 動作確認ポイント
- Notion
  - 設定UI → Notion → Upsert key mode（PerSlot推奨）を選択
  - Validate properties 実行で DB スキーマ（Name/Slot/ETA/Route/Rank/ExtId/Remaining/World/Character/FC）が OK 表示になること
- Discord
  - WebhookURL を設定し、Test Discord で送信（本文に「残/分」が表示される）
- 自動取得/アラーム
  - 工房UI表示→自動取得で JSON 出力と日本語チャット（ETA〜(残 n分)）が表示

## ビルド
- 標準: `dotnet build apps\XIVSubmarinesReturn\XIVSubmarinesReturn.csproj -c Release -p:Platform=x64`
  - DLL ビルドは成功。環境によって DalamudPackager の zip 作成でアクセス拒否が発生する場合あり（出力フォルダのロックが原因の可能性）。
  - 回避案: zip 出力先の手動削除、またはパッケージング無効化（環境に合わせて設定）。

## 既知の軽微事項
- Plugin の一部チャット文言は、元コードの文字化け由来で差分一致が難しい箇所があり、段階的に置換中。現状でもサニタイズと残り時間日本語化で可読性は改善済み。

## 参考コマンド例
- ビルド: `dotnet build apps\XIVSubmarinesReturn\XIVSubmarinesReturn.csproj -c Release -p:Platform=x64`
- リリースのみ（zip不問）: `dotnet build -c Release`
- C# 検索: `rg -n "Notion|Discord|GCal|XSR_FEAT_GCAL" apps\\XIVSubmarinesReturn\\src`

