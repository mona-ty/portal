# XIV Submarines Return — 現状把握と次のステップ（2025-09-09）

要点
- 現状: v0.1.1。UI/メモリの両経路で取得し JSON 出力、Discord/Notion 連携、オプションで自動取得を実装済み。
- 安定度: 取得/JSON/表UI/自動取得/Discord/Notion は安定。Google Calendar はビルドフラグで既定オフ。
- 残課題: 旧UIコードの整理、メッセージ整合、軽微な文字化け、README/配布手順の更新。

現在の実装状況（抜粋）
- 取得系: Addon走査（SelectString 最適化＋DFS）＋メモリ直読フォールバック。工房パネルからの不足補完あり。
- 自動取得: 工房画面の可視検知→セッション1回通知＋10秒ガードで JSON 更新。
- JSON出力: `%AppData%\\XIVSubmarinesReturn\\bridge\\submarines.json`。差分検知の省出力＋旧互換パスへミラー。
- ルート解決: Lumina Excel(反射)＋AliasIndex.json。`/sv`（test/debug/export-sectors/import-alias）。Mogship 取込で自動更新可。
- UI: 概要/アラーム/デバッグ。表はソート/フィルタ、ETA強調、ルートコピー、表示モード（レター/P番号/原文）。
- 外部連携: Discord（スナップショット/アラーム、レート制御/Embed）、Notion（DB検証＋アップサート、キー方式選択可）。

既知の軽微事項
- `/subcfg` の文言が現状と不一致（「設定UI未実装」→実際は実装済み）。
- 例外時の日本語 `PrintError` に文字化け箇所あり。
- 旧描画経路/旧UI断片が残存（新テーブルへ集約途中）。

提案する次のステップ（優先順）
1) 文言/体験の整合（小修正）
   - `/subcfg` のヘルプ文言修正、`/xsr help` の最新化、文字化けの修正。
2) UIの整理
   - 旧 `DrawSnapshotTable()` と `_showLegacyUi` フラグを段階的に削除し、新実装へ統一。
   - 概要/デバッグの操作ボタン重複を整理。
3) ドキュメント/配布
   - XSR用 README を `docs/` に切り出し、ビルド/導入/連携手順を現状に合わせ更新。
   - Packager運用（SkipPackの整理、簡易パッケージ手順）を記載。
4) 信頼性/利便性（任意）
   - 初期 Alias の導線（UI からの「初期展開」説明強化）。
   - 自動取得時のチャット通知頻度を設定化（無通知/1回通知切替）。
5) 解析/多言語（任意）
   - Extractors の英文/時刻表記のテスト拡充、簡易i18n下地。
6) テスト補助（任意）
   - 単体テスト（Duration解析/ルート表示/比較関数）を別プロジェクトで用意。

確認事項
- 最優先は「体験の整合（文言/UI整理）」で良いか、または配布手順の整備を先行させるか。
- 外部連携で直近重視するのは Discord / Notion / Google のどれか。

以上。

