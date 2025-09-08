AIコードレビュー（XIV Submarines Return）

概要（要点）
- 対象: Dalamud プラグイン「XIV Submarines Return」
- 目的: Discord通知に日付付きETAを表示、ルート取得の安定化、設定UIの自動リサイズ
- 範囲: `apps/XIVSubmarinesReturn` 下の C# ファイルのみ変更（ビルド/配布設定は未変更）

変更点（ファイル/概要）
- src/Configuration.cs
  - 追加: `AutoResizeWindow`（ウィンドウ自動リサイズのON/OFF、既定ON）
  - 追加: `LastRouteBySlot`（スロット別の最終ルートキャッシュ）
- src/Plugin.UI.cs
  - 初回のみ既定サイズへ変更（`FirstUseEver`）+ `AlwaysAutoResize` を設定値で付与
- src/Services/EtaFormatter.cs
  - 追加: `EtaLocalFull`（`yyyy/M/d HH:mm`）を `Extra` に付与
- src/Services/DiscordNotifier.cs
  - Embed/テキスト通知が `EtaLocalFull` を優先する `BuildSnapshotLine2` に切替（何月何日の何時を表示）
- src/Plugin.cs
  - メモリからのルート取得を強化: `CurrentExplorationPoints` が1～2点しか出ない場合、`LastRouteBySlot` と末尾一致（サフィックス）を比較し補完
  - 追加: `ParseRouteNumbers`, `IsSuffix`（補助関数）

動作/リスク
- 互換性: 既存JSONスキーマや保存場所に変更なし。設定に新規キーが増えるのみ
- リスク: ルート補完の誤適用（サフィックス一致の誤判定）⇒ キャッシュはスロット単位・フルルート検出時のみ更新で影響最小化
- パフォーマンス: 変更は軽量な文字列処理のみで影響軽微

ビルド/実行
- ビルドコマンド: `dotnet build apps\XIVSubmarinesReturn\XIVSubmarinesReturn.csproj -c Release -p:Platform=x64`
- 出力: `apps\XIVSubmarinesReturn\bin\x64\Release\net9.0-windows\XIVSubmarinesReturn.dll`

検証観点（手順メモ）
- Discord通知（Embed）: `/xsr dump` 実行 → Webhookに「yyyy/M/d HH:mm」形式のETAが含まれること
- ルート取得（メモリ）: 出航直後に一度フルルートが取得できる状態で `/xsr dumpmem` 実行 → 以降、途中で2点のみになってもキャッシュ補完で M>R>O>J>Z 相当が表示
- UIサイズ: 設定ウィンドウを開くたびに内容に合わせて縦幅が再計算されること

変更差分（ダイジェスト）
- Configuration.cs
  - `+ public bool AutoResizeWindow { get; set; } = true;`
  - `+ public Dictionary<int,string> LastRouteBySlot { get; set; } = new();`
- Plugin.UI.cs
  - `ImGui.SetNextWindowSize(..., FirstUseEver)` に変更
  - `ImGuiWindowFlags.AlwaysAutoResize` を `Config.AutoResizeWindow` で付与
- EtaFormatter.cs
  - `it.Extra["EtaLocalFull"] = eta.ToString("yyyy/M/d HH:mm");`
- DiscordNotifier.cs
  - Embed/テキスト: `BuildSnapshotLine2(it)` を使用（`EtaLocalFull`/`EtaUnix`/`EtaLocal`の順で選択）
- Plugin.cs（メモリ取得）
  - 取得ポイント列（3点以上）→そのまま `RouteKey` 採用しキャッシュ更新
  - 1～2点→キャッシュ末尾一致ならキャッシュを `RouteKey` に採用
  - 0点→キャッシュがあれば使用

ビルドログ（要約）
- 成功: 0 エラー / 11 警告
- 警告一覧:
  - CS8604: NotionClient.cs(46,42) Null 参照引数の可能性（`snap`）
  - CS8604: SectorResolver.cs(133,45) Null 参照引数の可能性（`row`）
  - CS8601: Plugin.cs(515,56) Null 参照代入の可能性
  - CS8602: Plugin.cs(574,35) null 参照の可能性の逆参照
  - CS0169: Plugin._gcal/Plugin._revealGcalRefresh/Plugin._revealGcalSecret/AlarmTab._revealGcalRefresh/AlarmTab._revealGcalSecret（未使用）
  - CS0414: Plugin._sortField/_sortAsc（割当されるが未使用）

実行ログ/デバッグ（変更関連）
- ルート取得時に以下のトレースが `extract_log.txt` に追記されます：
  - `route: off=0x<offset>,stride=1 -> [<point list>]`
- 追加の`XsrDebug.Log`出力：
  - `S{slot} route bytes = <list or (none)>`

テスト観点（最小）
- SnapshotのJSONに影響なし（`Extra`に `EtaLocalFull` が増えるのみ）
- Discord Webhook通知の本文が `yyyy/M/d HH:mm` を含むこと
- ルートが途中で `P15>10` 等に短縮表示されないこと（キャッシュ補完でフル維持）

セキュリティ/コンプライアンス
- Secrets/個人情報の扱い変更なし。Discord Webhook URL 等は既存設定を使用

残課題
- `NotifyAlarmAsync` のメッセージもフル日付ETAに統一するか要検討
- ビルド警告（CS860x/CS0169/CS0414）の解消（別タスク）

付録（ビルド出力）
- 出力DLL: `apps/XIVSubmarinesReturn/bin/x64/Release/net9.0-windows/XIVSubmarinesReturn.dll`
- 日時: 自動ビルド時刻に更新済み

